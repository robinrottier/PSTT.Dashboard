using PSTT.Data;
using PSTT.Remote;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// Tier A integration tests for <c>CacheHub</c>.
/// Uses <see cref="IntegrationWebApplicationFactory"/> — no real MQTT broker needed.
/// Values are injected via <see cref="FakeMqttClientService.TriggerIncomingMessageAsync"/>.
/// Clients connect via <see cref="RemoteCache{TValue}"/> using the PSTT SignalR transport.
/// </summary>
public class CacheHubTests : IClassFixture<IntegrationWebApplicationFactory>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IntegrationWebApplicationFactory _factory;
    private readonly List<RemoteCache<string>> _caches = new();

    public CacheHubTests(IntegrationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<RemoteCache<string>> ConnectedCacheAsync()
    {
        var cache = HubConnectionHelper.CreateRemoteCache(_factory);
        _caches.Add(cache);
        await cache.ConnectAsync();
        return cache;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_ReceivesValue_WhenPublished()
    {
        var cache = await ConnectedCacheAsync();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        cache.Subscribe("sensors/temperature", sub =>
        {
            tcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        await _factory.FakeMqttService.TriggerIncomingMessageAsync("sensors/temperature", "23.5");

        var value = await tcs.Task.WaitAsync(Timeout);
        Assert.Equal("23.5", value);
    }

    [Fact]
    public async Task Subscribe_ReceivesCurrentValue_WhenAlreadyCached()
    {
        await _factory.FakeMqttService.SeedLastKnownValueAsync("snapshot/sensor", "42.0");
        await _factory.FakeMqttService.SeedLastKnownValueAsync("snapshot/other", "on");

        var cache = await ConnectedCacheAsync();

        var received = new Dictionary<string, string>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        cache.Subscribe("snapshot/sensor", sub =>
        {
            lock (received) { received[sub.Key] = sub.Value; }
            if (received.Count >= 2) tcs.TrySetResult();
            return Task.CompletedTask;
        });
        cache.Subscribe("snapshot/other", sub =>
        {
            lock (received) { received[sub.Key] = sub.Value; }
            if (received.Count >= 2) tcs.TrySetResult();
            return Task.CompletedTask;
        });

        await tcs.Task.WaitAsync(Timeout);

        Assert.True(received.ContainsKey("snapshot/sensor"), "Should have snapshot/sensor");
        Assert.Equal("42.0", received["snapshot/sensor"]);
        Assert.True(received.ContainsKey("snapshot/other"), "Should have snapshot/other");
        Assert.Equal("on", received["snapshot/other"]);
    }

    [Fact]
    public async Task Unsubscribe_StopsReceivingData()
    {
        var cache = await ConnectedCacheAsync();

        var callCount = 0;
        var firstCallTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = cache.Subscribe("unsub/topic", _ =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
                firstCallTcs.TrySetResult();
            return Task.CompletedTask;
        });

        // Publish once and wait for confirmed delivery
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("unsub/topic", "before");
        await firstCallTcs.Task.WaitAsync(Timeout);

        // Dispose — no further callbacks should fire
        sub.Dispose();

        await _factory.FakeMqttService.TriggerIncomingMessageAsync("unsub/topic", "after");
        await Task.Delay(300);

        Assert.Equal(1, Volatile.Read(ref callCount));
    }

    [Fact]
    public async Task MultipleClients_EachOnlyReceivesOwnSubscribedTopics()
    {
        var cacheA = await ConnectedCacheAsync();
        var cacheB = await ConnectedCacheAsync();

        var aTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Each cache subscribes only to its own topic
        cacheA.Subscribe("isolation2/topic-a", sub =>
        {
            aTcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });
        cacheB.Subscribe("isolation2/topic-b", sub =>
        {
            bTcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        await _factory.FakeMqttService.TriggerIncomingMessageAsync("isolation2/topic-a", "for-a");
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("isolation2/topic-b", "for-b");

        var aValue = await aTcs.Task.WaitAsync(Timeout);
        var bValue = await bTcs.Task.WaitAsync(Timeout);

        Assert.Equal("for-a", aValue);
        Assert.Equal("for-b", bValue);
    }

    [Fact]
    public async Task WildcardSubscription_MatchesMultipleTopics()
    {
        var cache = await ConnectedCacheAsync();

        var received = new List<(string key, string value)>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        cache.Subscribe("wildcard2/#", sub =>
        {
            lock (received)
            {
                received.RemoveAll(r => r.key == sub.Key); // keep only latest per key
                received.Add((sub.Key, sub.Value));
                if (received.Any(r => r.key == "wildcard2/a") && received.Any(r => r.key == "wildcard2/b"))
                    tcs.TrySetResult();
            }
            return Task.CompletedTask;
        });

        // Give the async subscribe message time to reach the server before publishing.
        await Task.Delay(200);

        await _factory.FakeMqttService.TriggerIncomingMessageAsync("wildcard2/a", "payload-a");
        await _factory.FakeMqttService.TriggerIncomingMessageAsync("wildcard2/b", "payload-b");

        await tcs.Task.WaitAsync(Timeout);

        List<(string key, string value)> snapshot;
        lock (received) { snapshot = received.ToList(); }

        Assert.Contains(snapshot, r => r.key == "wildcard2/a" && r.value == "payload-a");
        Assert.Contains(snapshot, r => r.key == "wildcard2/b" && r.value == "payload-b");
    }

    [Fact]
    public async Task DashboardTopics_ClientCountPublishedToCache()
    {
        var cache = await ConnectedCacheAsync();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        cache.Subscribe(DashboardTopics.ClientCount, sub =>
        {
            tcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        var payload = await tcs.Task.WaitAsync(Timeout);
        Assert.True(int.TryParse(payload, out _), $"Expected integer payload, got '{payload}'");
    }

    [Fact]
    public async Task DashboardTopics_BrokerInfoPublishedToCache()
    {
        var cache = await ConnectedCacheAsync();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        cache.Subscribe(DashboardTopics.MqttBroker, sub =>
        {
            tcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        var payload = await tcs.Task.WaitAsync(Timeout);
        Assert.False(string.IsNullOrEmpty(payload));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cache in _caches)
            await cache.DisposeAsync();
    }
}
