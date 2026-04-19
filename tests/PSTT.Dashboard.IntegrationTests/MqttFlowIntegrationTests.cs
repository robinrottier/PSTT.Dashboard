using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;
using PSTT.Mqtt;
using PSTT.Remote;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// Tier B integration tests — real MQTT message flow.
/// An in-process MQTTnet broker is started by <see cref="InProcessMqttBrokerFixture"/>;
/// the real <see cref="PSTT.Dashboard.Server.Services.MqttHostedService"/> connects to it.
/// Tests publish messages via a separate MQTT client and verify they arrive via
/// a PSTT <see cref="RemoteCache{TValue}"/> client, exercising the full production code path.
/// </summary>
public class MqttFlowIntegrationTests : IClassFixture<InProcessMqttBrokerFixture>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly InProcessMqttBrokerFixture _broker;
    private MqttBrokerIntegrationFactory? _factory;
    private IMqttClient? _publisherClient;
    private readonly List<RemoteCache<string>> _caches = new();

    public MqttFlowIntegrationTests(InProcessMqttBrokerFixture broker)
    {
        _broker = broker;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private async Task<(MqttBrokerIntegrationFactory factory, RemoteCache<string> cache)> StartAsync()
    {
        _factory = new MqttBrokerIntegrationFactory(_broker.Port);
        var cache = HubConnectionHelper.CreateRemoteCache(_factory);
        _caches.Add(cache);
        await cache.ConnectAsync();

        // Wait for MqttHostedService to connect to the in-process broker.
        await WaitForMqttConnectedAsync(_factory);

        return (_factory, cache);
    }

    private static async Task WaitForMqttConnectedAsync(MqttBrokerIntegrationFactory factory)
    {
        var mqttCache = factory.Services.GetRequiredService<MqttCache<string>>();
        var deadline = DateTime.UtcNow + Timeout;
        while (!mqttCache.IsConnected && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        if (!mqttCache.IsConnected)
            throw new TimeoutException("MqttHostedService did not connect to the in-process broker within the timeout.");
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
        var (_, cache) = await StartAsync();

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.Subscribe("flow/test", sub =>
        {
            tcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        var publisher = await GetPublisherAsync();
        await PublishAsync(publisher, "flow/test", "hello-from-broker");

        var value = await tcs.Task.WaitAsync(Timeout);
        Assert.Equal("hello-from-broker", value);
    }

    [Fact]
    public async Task WildcardSubscription_MatchesMultipleTopics()
    {
        var (_, cache) = await StartAsync();

        var received = new List<(string key, string value)>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        cache.Subscribe("wildcard/#", sub =>
        {
            lock (received) { received.Add((sub.Key, sub.Value)); }
            if (received.Count >= 2) tcs.TrySetResult();
            return Task.CompletedTask;
        });

        var publisher = await GetPublisherAsync();
        await PublishAsync(publisher, "wildcard/a", "payload-a");
        await PublishAsync(publisher, "wildcard/b", "payload-b");

        await tcs.Task.WaitAsync(Timeout);

        Assert.Contains(received, r => r.key == "wildcard/a" && r.value == "payload-a");
        Assert.Contains(received, r => r.key == "wildcard/b" && r.value == "payload-b");
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

        var (_, cache) = await StartAsync();

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.Subscribe(retainedTopic, sub =>
        {
            tcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        var value = await tcs.Task.WaitAsync(Timeout);
        Assert.Equal("retained-value", value);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cache in _caches)
            await cache.DisposeAsync();

        if (_publisherClient != null)
        {
            if (_publisherClient.IsConnected)
                await _publisherClient.DisconnectAsync();
            _publisherClient.Dispose();
        }
        _factory?.Dispose();
    }
}
