using Microsoft.AspNetCore.SignalR.Client;

namespace MqttDashboard.IntegrationTests;

/// <summary>
/// Tier A integration tests for <c>DataHub</c>.
/// Use <see cref="IntegrationWebApplicationFactory"/> — no real MQTT broker needed.
/// Messages are injected via <see cref="FakeMqttClientService.TriggerIncomingMessageAsync"/>.
/// </summary>
public class MqttDataHubTests : IClassFixture<IntegrationWebApplicationFactory>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IntegrationWebApplicationFactory _factory;

    public MqttDataHubTests(IntegrationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HubConnection CreateConnection() => HubConnectionHelper.Create(_factory);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnConnect_ReceivesMqttConnectionStatus()
    {
        await using var conn = CreateConnection();

        // Register handler BEFORE StartAsync so we don't miss the push from OnConnectedAsync.
        var received = HubConnectionHelper.WaitForAsync<string, int>(conn, "MqttConnectionStatus", Timeout);

        await conn.StartAsync();

        var (state, _) = await received;
        Assert.False(string.IsNullOrEmpty(state));
    }

    [Fact]
    public async Task SubscribeToTopic_ConfirmationReceived()
    {
        await using var conn = CreateConnection();
        await conn.StartAsync();

        var confirmed = HubConnectionHelper.WaitForAsync<string>(conn, "SubscriptionConfirmed", Timeout);
        await conn.InvokeAsync("SubscribeToTopic", "test/topic");

        var topic = await confirmed;
        Assert.Equal("test/topic", topic);
    }

    [Fact]
    public async Task SubscribeToTopic_Then_TriggerMessage_ClientReceivesData()
    {
        await using var conn = CreateConnection();
        await conn.StartAsync();

        // Subscribe first
        var confirmed = HubConnectionHelper.WaitForAsync<string>(conn, "SubscriptionConfirmed", Timeout);
        await conn.InvokeAsync("SubscribeToTopic", "sensors/temperature");
        await confirmed;

        // Set up listener for data
        var dataReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(
            conn, "ReceiveMqttData", Timeout);

        // Inject a fake MQTT message
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("sensors/temperature", "23.5");

        var (topic, payload, _) = await dataReceived;
        Assert.Equal("sensors/temperature", topic);
        Assert.Equal("23.5", payload);
    }

    [Fact]
    public async Task UnsubscribedClient_DoesNotReceiveData()
    {
        await using var subscribedConn = CreateConnection();
        await using var unsubscribedConn = CreateConnection();

        await subscribedConn.StartAsync();
        await unsubscribedConn.StartAsync();

        // Only subscribedConn subscribes
        var confirmed = HubConnectionHelper.WaitForAsync<string>(subscribedConn, "SubscriptionConfirmed", Timeout);
        await subscribedConn.InvokeAsync("SubscribeToTopic", "only/subscribed");
        await confirmed;

        // Track whether unsubscribedConn receives anything
        var unexpectedData = false;
        unsubscribedConn.On<string, string, DateTime>("ReceiveMqttData", (_, __, ___) =>
            unexpectedData = true);

        await _factory.FakeMqttService.TriggerIncomingMessageAsync("only/subscribed", "hello");

        // Give a moment for any erroneous delivery
        await Task.Delay(200);
        Assert.False(unexpectedData, "Unsubscribed client should not receive data");
    }

    [Fact]
    public async Task UnsubscribeFromTopic_ConfirmationReceived_And_NoFurtherData()
    {
        await using var conn = CreateConnection();
        await conn.StartAsync();

        // Subscribe
        var subConfirmed = HubConnectionHelper.WaitForAsync<string>(conn, "SubscriptionConfirmed", Timeout);
        await conn.InvokeAsync("SubscribeToTopic", "unsubtest/topic");
        await subConfirmed;

        // Unsubscribe
        var unsubConfirmed = HubConnectionHelper.WaitForAsync<string>(conn, "UnsubscriptionConfirmed", Timeout);
        await conn.InvokeAsync("UnsubscribeFromTopic", "unsubtest/topic");
        var unsubTopic = await unsubConfirmed;
        Assert.Equal("unsubtest/topic", unsubTopic);

        // Subsequent message should NOT arrive
        var unexpectedData = false;
        conn.On<string, string, DateTime>("ReceiveMqttData", (_, __, ___) => unexpectedData = true);
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("unsubtest/topic", "ignored");
        await Task.Delay(200);
        Assert.False(unexpectedData);
    }

    [Fact]
    public async Task GetCurrentValuesForTopics_ReturnsCachedValues()
    {
        await _factory.FakeMqttService.SeedLastKnownValueAsync("cached/sensor", "42.0");
        await _factory.FakeMqttService.SeedLastKnownValueAsync("cached/other", "on");

        await using var conn = CreateConnection();
        await conn.StartAsync();

        // Subscribe so the filter matches
        await conn.InvokeAsync("SubscribeToTopic", "cached/#");

        var result = await conn.InvokeAsync<Dictionary<string, string>>(
            "GetCurrentValuesForTopics", new List<string> { "cached/#" });

        Assert.True(result.ContainsKey("cached/sensor"), "Should return cached/sensor");
        Assert.Equal("42.0", result["cached/sensor"]);
        Assert.True(result.ContainsKey("cached/other"), "Should return cached/other");
    }

    [Fact]
    public async Task DashboardTopics_ClientCountPublishedToCache()
    {
        await using var conn1 = CreateConnection();
        await using var conn2 = CreateConnection();
        await conn1.StartAsync();
        await conn2.StartAsync();

        // Register listener before subscribing so the immediate cache-seed fires it
        var dataReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(
            conn1, "ReceiveMqttData", Timeout);

        await conn1.InvokeAsync("SubscribeToTopic", "$DASHBOARD/CLIENTS/COUNT");

        var (topic, payload, _) = await dataReceived;

        Assert.Equal("$DASHBOARD/CLIENTS/COUNT", topic);
        // Just verify it's a valid integer — exact count is non-deterministic in tests
        // (the publisher tick may have fired before or after connections were established)
        Assert.True(int.TryParse(payload, out _), $"Expected integer payload, got '{payload}'");
    }

    [Fact]
    public async Task DashboardTopics_BrokerInfoPublishedToCache()
    {
        await using var conn = CreateConnection();
        await conn.StartAsync();

        // Register listener before subscribing so the immediate cache-seed fires it
        var dataReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(
            conn, "ReceiveMqttData", Timeout);

        await conn.InvokeAsync("SubscribeToTopic", "$DASHBOARD/MQTT/BROKER");

        var (topic, payload, _) = await dataReceived;

        Assert.Equal("$DASHBOARD/MQTT/BROKER", topic);
        Assert.False(string.IsNullOrEmpty(payload));
        // In production the format is "host:port"; in tests with a fake service it may be "unknown"
    }

    [Fact]
    public async Task MultipleClients_EachOnlyReceivesOwnSubscribedTopics()
    {
        await using var connA = CreateConnection();
        await using var connB = CreateConnection();
        await connA.StartAsync();
        await connB.StartAsync();

        // A subscribes to topic-a, B subscribes to topic-b
        var aConfirmed = HubConnectionHelper.WaitForAsync<string>(connA, "SubscriptionConfirmed", Timeout);
        var bConfirmed = HubConnectionHelper.WaitForAsync<string>(connB, "SubscriptionConfirmed", Timeout);
        await connA.InvokeAsync("SubscribeToTopic", "isolation/topic-a");
        await connB.InvokeAsync("SubscribeToTopic", "isolation/topic-b");
        await aConfirmed;
        await bConfirmed;

        var aReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(connA, "ReceiveMqttData", Timeout);
        var bReceived = HubConnectionHelper.WaitForAsync<string, string, DateTime>(connB, "ReceiveMqttData", Timeout);

        // Trigger BOTH topics
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("isolation/topic-a", "for-a");
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("isolation/topic-b", "for-b");

        var (aTopic, aPayload, _) = await aReceived;
        var (bTopic, bPayload, _) = await bReceived;

        Assert.Equal("isolation/topic-a", aTopic);
        Assert.Equal("for-a", aPayload);
        Assert.Equal("isolation/topic-b", bTopic);
        Assert.Equal("for-b", bPayload);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
