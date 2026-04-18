using MqttDashboard.Data;
using MQTTnet;

namespace MqttDashboard.Mqtt.Tests;

/// <summary>
/// Integration tests that wire a <see cref="DataCache"/> to a real <see cref="MqttClientService"/>
/// connected to an in-process broker.
/// <para>
/// These tests verify two directions of data flow and a full round-trip:
/// <list type="bullet">
///   <item>Broker → DataCache: a message published to the broker arrives in a cache subscriber.</item>
///   <item>DataCache → Broker: publishing via the cache sends a message to the broker.</item>
///   <item>Round-trip: publishing from one cache travels through the broker and is received
///         by a subscriber on a second cache — no shared state, real MQTT echo.</item>
/// </list>
/// </para>
/// </summary>
public class MqttDataCacheIntegrationTests : IClassFixture<InProcessMqttBrokerFixture>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly InProcessMqttBrokerFixture _broker;
    private readonly List<MqttClientService> _services = new();

    public MqttDataCacheIntegrationTests(InProcessMqttBrokerFixture broker)
    {
        _broker = broker;
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(MqttClientService service, MqttTopicSubscriptionManager manager)> StartServiceAsync()
    {
        var (svc, mgr, _) = await MqttTestHelpers.StartServiceAsync(_broker.Port);
        _services.Add(svc);
        return (svc, mgr);
    }

    /// <summary>
    /// Wires <paramref name="service"/> so that every incoming MQTT message is pushed into
    /// <paramref name="cache"/> via <see cref="IDataCache.UpdateValue"/>.
    /// This mirrors what <c>MqttDataServer</c> does in the server project without the
    /// SignalR / subscription-manager complexity.
    /// </summary>
    private static void WireServiceToCache(MqttClientService service, IDataCache cache)
    {
        service.OnMessagePublished += (topic, payload, _) =>
        {
            cache.UpdateValue(topic, payload);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Minimal <see cref="IDataServer"/> that forwards <see cref="IDataCache.PublishAsync"/>
    /// calls up to the MQTT broker via <see cref="MqttClientService.PublishMessageAsync"/>.
    /// Incoming values are injected back via <see cref="ValueUpdated"/> which the cache wires
    /// to its own <see cref="IDataCache.UpdateValue"/> on registration.
    /// </summary>
    private sealed class MqttDataServerStub : IDataServer
    {
        private readonly MqttClientService _mqttService;
        private readonly MqttTopicSubscriptionManager _subscriptionManager;
        private const string StubClientId = "stub-data-server";

        public event Action<string, object>? ValueUpdated;
#pragma warning disable CS0067 // Events required by IDataServer but unused in this test stub
        public event Action? Reconnected;
        public event Action<string, bool>? StatusChanged;
#pragma warning restore CS0067

        public MqttDataServerStub(MqttClientService mqttService,
                                   MqttTopicSubscriptionManager subscriptionManager)
        {
            _mqttService = mqttService;
            _subscriptionManager = subscriptionManager;

            // Forward all incoming MQTT messages as ValueUpdated events so the
            // registered cache can call UpdateValue on them.
            _mqttService.OnMessagePublished += (topic, payload, _) =>
            {
                ValueUpdated?.Invoke(topic, payload);
                return Task.CompletedTask;
            };
        }

        public Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0)
            => _mqttService.PublishMessageAsync(topic, payload, retain, qos);

        public Task StartAsync(string serverUrl = "") => Task.CompletedTask;

        public Task SubscribeAsync(string topic)
            => _subscriptionManager.SubscribeClientToTopicAsync(StubClientId, topic);

        public Task UnsubscribeAsync(string topic)
            => _subscriptionManager.UnsubscribeClientFromTopicAsync(StubClientId, topic);

        public ValueTask DisposeAsync()
        {
            _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(StubClientId).GetAwaiter().GetResult();
            return ValueTask.CompletedTask;
        }
    }

    // ── Tests: Broker → DataCache (downstream) ────────────────────────────────────

    [Fact]
    public async Task BrokerMessage_ArriveInDataCacheSubscriber()
    {
        var (svc, mgr) = await StartServiceAsync();
        var cache = new DataCache();
        WireServiceToCache(svc, cache);

        // Subscribe at broker level so messages are delivered to MqttClientService.
        await mgr.SubscribeClientToTopicAsync("test", "home/temperature");

        string? receivedTopic = null;
        string? receivedValue = null;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cache.Subscribe("home/temperature", (t, v) =>
        {
            receivedTopic = t;
            receivedValue = v.ToString();
            received.TrySetResult();
        });

        using var publisher = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port);
        await MqttTestHelpers.PublishAsync(publisher, "home/temperature", "21.5");

        await received.Task.WaitAsync(Timeout);
        Assert.Equal("home/temperature", receivedTopic);
        Assert.Equal("21.5", receivedValue);
    }

    [Fact]
    public async Task BrokerMessage_WildcardSubscription_MultipleCacheUpdates()
    {
        var (svc, mgr) = await StartServiceAsync();
        var cache = new DataCache();
        WireServiceToCache(svc, cache);

        await mgr.SubscribeClientToTopicAsync("test", "sensors/#");

        var topics = new List<string>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cache.Subscribe("sensors/#", (t, _) =>
        {
            lock (topics) { topics.Add(t); if (topics.Count >= 2) tcs.TrySetResult(); }
        });

        using var publisher = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port);
        await MqttTestHelpers.PublishAsync(publisher, "sensors/humidity", "55");
        await MqttTestHelpers.PublishAsync(publisher, "sensors/co2", "400");

        await tcs.Task.WaitAsync(Timeout);
        Assert.Contains("sensors/humidity", topics);
        Assert.Contains("sensors/co2", topics);
    }

    // ── Tests: DataCache → Broker (upstream, publish direction) ──────────────────

    [Fact]
    public async Task DataCache_PublishAsync_SendsMessageToBroker()
    {
        var (svc, mgr) = await StartServiceAsync();

        // Create a cache backed by the stub server so PublishAsync flows to MQTT.
        await using var stub = new MqttDataServerStub(svc, mgr);
        var cache = new DataCache();
        cache.RegisterServer(stub);

        // External subscriber receives the message.
        using var subscriber = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port, "ext-recv");
        await subscriber.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("cmd/light")
            .Build());
        var receivedTask = MqttTestHelpers.WaitForMessageAsync(subscriber, "cmd/light", Timeout);

        // Publish via the cache — flows through stub → MqttClientService → broker.
        await cache.PublishAsync("cmd/light", "ON");

        var payload = await receivedTask;
        Assert.Equal("ON", payload);
    }

    // ── Tests: Full round-trip ─────────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_PublishFromCacheA_ReceivedByCacheB()
    {
        // Two independent services connected to the same broker simulate two different
        // application instances (or a loopback through the broker).
        var (svcA, mgrA) = await StartServiceAsync();
        var (svcB, mgrB) = await StartServiceAsync();

        // Cache A: publisher side.  Cache B: subscriber side.
        await using var stubA = new MqttDataServerStub(svcA, mgrA);
        var cacheA = new DataCache();
        cacheA.RegisterServer(stubA);

        // Cache B receives incoming broker messages (demand-driven via stubB).
        await using var stubB = new MqttDataServerStub(svcB, mgrB);
        var cacheB = new DataCache();
        cacheB.RegisterServer(stubB);

        // Subscribing on cacheB triggers stubB.SubscribeAsync → broker subscription.
        string? receivedValue = null;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cacheB.Subscribe("roundtrip/value", (_, v) =>
        {
            // Ignore the immediate local seed (only fires if there is a cached value).
            receivedValue = v.ToString();
            received.TrySetResult();
        });

        // Publish from cache A.  The flow is:
        //   cacheA.PublishAsync → stubA.PublishAsync → MqttClientService A → broker
        //     → MqttClientService B → stubB.ValueUpdated → cacheB.UpdateValue → subscriber
        await cacheA.PublishAsync("roundtrip/value", "42");

        await received.Task.WaitAsync(Timeout);
        Assert.Equal("42", receivedValue);
    }

    [Fact]
    public async Task RoundTrip_PublishFromCacheB_ReceivedByCacheA()
    {
        // Reverse direction: Cache B publishes, Cache A receives.
        var (svcA, mgrA) = await StartServiceAsync();
        var (svcB, mgrB) = await StartServiceAsync();

        await using var stubA = new MqttDataServerStub(svcA, mgrA);
        var cacheA = new DataCache();
        cacheA.RegisterServer(stubA);

        await using var stubB = new MqttDataServerStub(svcB, mgrB);
        var cacheB = new DataCache();
        cacheB.RegisterServer(stubB);

        // Subscribe on cache A first.
        string? receivedValue = null;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cacheA.Subscribe("roundtrip/reverse", (_, v) =>
        {
            receivedValue = v.ToString();
            received.TrySetResult();
        });

        // Publish from cache B.
        await cacheB.PublishAsync("roundtrip/reverse", "reverse-99");

        await received.Task.WaitAsync(Timeout);
        Assert.Equal("reverse-99", receivedValue);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var svc in _services)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await svc.StopAsync(cts.Token);
            svc.Dispose();
        }
    }
}
