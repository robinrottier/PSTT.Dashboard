using Microsoft.AspNetCore.SignalR.Client;
using MQTTnet;
using MQTTnet.Protocol;

namespace MqttDashboard.IntegrationTests;

/// <summary>
/// Tier B integration tests — real MQTT message flow.
/// An in-process MQTTnet broker is started by <see cref="InProcessMqttBrokerFixture"/>;
/// the real <see cref="MqttDashboard.Server.Services.MqttClientService"/> connects to it.
/// Tests publish messages via a separate MQTT client and verify they arrive at
/// the SignalR <c>HubConnection</c>, exercising the full production code path.
/// </summary>
public class MqttFlowIntegrationTests : IClassFixture<InProcessMqttBrokerFixture>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly InProcessMqttBrokerFixture _broker;
    private MqttBrokerIntegrationFactory? _factory;
    private IMqttClient? _publisherClient;

    public MqttFlowIntegrationTests(InProcessMqttBrokerFixture broker)
    {
        _broker = broker;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private async Task<(MqttBrokerIntegrationFactory factory, HubConnection conn)> StartAsync()
    {
        _factory = new MqttBrokerIntegrationFactory(_broker.Port);

        var conn = HubConnectionHelper.Create(_factory);

        // Register the status handler BEFORE StartAsync so we never miss the
        // OnConnectedAsync broadcast that the hub sends immediately on connect.
        // (On subsequent tests the broker is already up and MqttClientService
        // connects in <5 ms — far faster than any post-StartAsync registration.)
        var connectedTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.On<string, int>("MqttConnectionStatus", (status, _) =>
        {
            if (status == "Connected") connectedTcs.TrySetResult(status);
        });

        await conn.StartAsync();

        // Wait for MqttClientService to report it is connected to the broker.
        await WaitForConnectedAsync(connectedTcs);

        return (_factory, conn);
    }

    private static async Task WaitForConnectedAsync(TaskCompletionSource<string> tcs)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!tcs.Task.IsCompleted && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        if (!tcs.Task.IsCompleted)
            throw new TimeoutException("MqttClientService did not connect to the in-process broker within the timeout.");
    }

    private async Task<IMqttClient> GetPublisherAsync()
    {
        if (_publisherClient?.IsConnected == true) return _publisherClient;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", _broker.Port)
            .WithClientId("IntegrationTestPublisher_" + Guid.NewGuid().ToString("N")[..8])
            .Build();

        _publisherClient = new MqttClientFactory().CreateMqttClient();
        await _publisherClient.ConnectAsync(options);
        return _publisherClient;
    }

    private static Task PublishAsync(IMqttClient client, string topic, string payload,
        bool retain = false, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .WithQualityOfServiceLevel(qos)
            .Build();
        return client.PublishAsync(message);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_Via_Broker_ClientReceivesData()
    {
        var (_, conn) = await StartAsync();
        await using var _ = conn;

        // Subscribe via SignalR hub
        var subConfirmed = HubConnectionHelper.WaitForAsync<string>(conn, "SubscriptionConfirmed", Timeout);
        await conn.InvokeAsync("SubscribeToTopic", "flow/test");
        await subConfirmed;

        // Wait for data
        var dataReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(
            conn, "ReceiveMqttData", Timeout);

        // Publish via real MQTT client → broker → MqttClientService → SignalR
        var publisher = await GetPublisherAsync();
        await PublishAsync(publisher, "flow/test", "hello-from-broker");

        var (topic, payload, _) = await dataReceived;
        Assert.Equal("flow/test", topic);
        Assert.Equal("hello-from-broker", payload);
    }

    [Fact]
    public async Task WildcardSubscription_MatchesMultipleTopics()
    {
        var (_, conn) = await StartAsync();
        await using var _ = conn;

        var subConfirmed = HubConnectionHelper.WaitForAsync<string>(conn, "SubscriptionConfirmed", Timeout);
        await conn.InvokeAsync("SubscribeToTopic", "wildcard/#");
        await subConfirmed;

        // Collect multiple messages
        var received = new List<(string topic, string payload)>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.On<string, string, DateTime>("ReceiveMqttData", (t, p, _) =>
        {
            lock (received) { received.Add((t, p)); }
            if (received.Count >= 2) tcs.TrySetResult();
        });

        var publisher = await GetPublisherAsync();
        await PublishAsync(publisher, "wildcard/a", "payload-a");
        await PublishAsync(publisher, "wildcard/b", "payload-b");

        await tcs.Task.WaitAsync(Timeout);

        Assert.Contains(received, r => r.topic == "wildcard/a" && r.payload == "payload-a");
        Assert.Contains(received, r => r.topic == "wildcard/b" && r.payload == "payload-b");
    }

    [Fact]
    public async Task RetainedMessage_DeliveredOnSubscribe()
    {
        // Publish a retained message BEFORE the SignalR client subscribes
        var publisher = await GetPublisherAsync();
        var retainedTopic = $"retained/{Guid.NewGuid():N}";
        await PublishAsync(publisher, retainedTopic, "retained-value",
            retain: true, qos: MqttQualityOfServiceLevel.AtLeastOnce);

        // Give the broker a moment to store it
        await Task.Delay(100);

        var (_, conn) = await StartAsync();
        await using var _ = conn;

        var dataReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(
            conn, "ReceiveMqttData", Timeout);

        // Subscribe — broker should immediately deliver the retained message
        await conn.InvokeAsync("SubscribeToTopic", retainedTopic);

        var (topic, payload, _) = await dataReceived;
        Assert.Equal(retainedTopic, topic);
        Assert.Equal("retained-value", payload);
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisherClient != null)
        {
            if (_publisherClient.IsConnected)
                await _publisherClient.DisconnectAsync();
            _publisherClient.Dispose();
        }
        _factory?.Dispose();
    }
}
