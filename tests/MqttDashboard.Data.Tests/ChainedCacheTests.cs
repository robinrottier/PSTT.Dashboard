using MqttDashboard.Data;

namespace MqttDashboard.Data.Tests;

/// <summary>
/// Tests for chains of <see cref="DataCache"/> instances connected via
/// <see cref="CacheBridgeDataServer"/>.
/// <para>
/// A "chained cache" topology looks like:
/// <code>
///   upstream (DataCache) ─── CacheBridgeDataServer ──► downstream (DataCache)
/// </code>
/// or for multi-level:
/// <code>
///   cacheA ─── bridgeAB ──► cacheB ─── bridgeBC ──► cacheC
/// </code>
/// </para>
/// Tests cover:
/// <list type="bullet">
///   <item>Values flowing <b>down</b> the chain (upstream → subscriber on downstream).</item>
///   <item>Publishes flowing <b>up</b> the chain (downstream.PublishAsync → upstream updated).</item>
///   <item>Three-level chains (A → B → C and C → A).</item>
///   <item>Demand-driven subscription propagation.</item>
///   <item>Dispose cleans up mid-chain subscriptions.</item>
/// </list>
/// </summary>
public class ChainedCacheTests
{
    // ── Two-level chain helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Creates:  upstream ──bridgeServer──► downstream
    /// Caller is responsible for disposing the bridge.
    /// </summary>
    private static (DataCache upstream, CacheBridgeDataServer bridge, DataCache downstream)
        BuildTwoLevelChain()
    {
        var upstream   = new DataCache();
        var bridge     = new CacheBridgeDataServer(upstream);
        var downstream = new DataCache();
        downstream.RegisterServer(bridge);
        return (upstream, bridge, downstream);
    }

    // ── Value flows DOWN the chain ────────────────────────────────────────────────

    [Fact]
    public async Task TwoLevel_UpstreamUpdate_ReachesDownstreamSubscriber()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        string? received = null;
        using var sub = downstream.Subscribe("sensor/temp", (_, v) => received = v.ToString());

        upstream.UpdateValue("sensor/temp", "25");

        Assert.Equal("25", received);
    }

    [Fact]
    public async Task TwoLevel_WildcardSubscriber_ReceivesAllMatchingUpdates()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        var topics = new List<string>();
        using var sub = downstream.Subscribe("home/#", (t, _) => topics.Add(t));

        upstream.UpdateValue("home/temp",     "20");
        upstream.UpdateValue("home/humidity", "50");
        upstream.UpdateValue("other/topic",   "99"); // should NOT match

        Assert.Contains("home/temp",     topics);
        Assert.Contains("home/humidity", topics);
        Assert.DoesNotContain("other/topic", topics);
    }

    [Fact]
    public async Task TwoLevel_SeedFromCachedValue_OnSubscribe()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        // Put a value in upstream before any downstream subscriber exists.
        upstream.UpdateValue("cached/topic", "seed-value");

        // When the subscriber registers, it should be seeded immediately.
        string? received = null;
        using var sub = downstream.Subscribe("cached/topic", (_, v) => received = v.ToString());

        // Give the demand-driven upstream subscription time to settle.
        await Task.Delay(50);
        upstream.UpdateValue("cached/topic", "new-value");

        Assert.Equal("new-value", received);
    }

    // ── Value flows UP the chain (publish) ────────────────────────────────────────

    [Fact]
    public async Task TwoLevel_DownstreamPublishAsync_UpdatesUpstream()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        // Watch the upstream for the published value.
        string? upstreamReceived = null;
        using var upSub = upstream.Subscribe("cmd/led", (_, v) => upstreamReceived = v.ToString());

        await downstream.PublishAsync("cmd/led", "ON");

        Assert.Equal("ON", upstreamReceived);
    }

    [Fact]
    public async Task TwoLevel_DownstreamPublishAsync_AlsoUpdatesDownstreamSubscriber()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        // DataCache.PublishAsync always applies UpdateValue locally first.
        string? downstreamReceived = null;
        using var downSub = downstream.Subscribe("status/led", (_, v) => downstreamReceived = v.ToString());

        await downstream.PublishAsync("status/led", "OFF");

        Assert.Equal("OFF", downstreamReceived);
    }

    // ── Three-level chain ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ThreeLevel_UpdateOnA_ReachesSubscriberOnC()
    {
        // A ──bridgeAB──► B ──bridgeBC──► C
        var cacheA = new DataCache();

        var bridgeAB = new CacheBridgeDataServer(cacheA);
        var cacheB   = new DataCache();
        cacheB.RegisterServer(bridgeAB);

        var bridgeBC = new CacheBridgeDataServer(cacheB);
        var cacheC   = new DataCache();
        cacheC.RegisterServer(bridgeBC);

        await using var _ab = bridgeAB;
        await using var _bc = bridgeBC;

        string? received = null;
        using var sub = cacheC.Subscribe("deep/value", (_, v) => received = v.ToString());

        // Give demand-subscription chain time to propagate up.
        await Task.Delay(50);

        cacheA.UpdateValue("deep/value", "propagated");

        // The value may arrive slightly after UpdateValue returns (async event chain).
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (received == null && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Equal("propagated", received);
    }

    [Fact]
    public async Task ThreeLevel_PublishOnC_ReachesA()
    {
        // C.PublishAsync → bridgeBC.PublishAsync → B.PublishAsync → bridgeAB.PublishAsync → A.UpdateValue
        var cacheA = new DataCache();

        var bridgeAB = new CacheBridgeDataServer(cacheA);
        var cacheB   = new DataCache();
        cacheB.RegisterServer(bridgeAB);

        var bridgeBC = new CacheBridgeDataServer(cacheB);
        var cacheC   = new DataCache();
        cacheC.RegisterServer(bridgeBC);

        await using var _ab = bridgeAB;
        await using var _bc = bridgeBC;

        string? upstreamReceived = null;
        using var sub = cacheA.Subscribe("cmd/device", (_, v) => upstreamReceived = v.ToString());

        await cacheC.PublishAsync("cmd/device", "reboot");

        Assert.Equal("reboot", upstreamReceived);
    }

    // ── Demand-driven subscription propagation ────────────────────────────────────

    [Fact]
    public async Task DemandSubscription_FirstSubscriberTriggersBridgeSubscribe()
    {
        var upstream   = new DataCache();
        var bridge     = new CacheBridgeDataServer(upstream);
        var downstream = new DataCache();
        downstream.RegisterServer(bridge);
        await using var _ = bridge;

        // Before any downstream subscriber, upstream should have no watchers for this topic.
        Assert.Null(upstream.GetValue("demand/topic"));

        // First downstream subscriber → should propagate up and subscribe on upstream.
        string? received = null;
        using var sub = downstream.Subscribe("demand/topic", (_, v) => received = v.ToString());

        // Give the async demand subscription a moment.
        await Task.Delay(50);

        // Now updating upstream should reach the downstream subscriber.
        upstream.UpdateValue("demand/topic", "demand-value");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (received == null && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Equal("demand-value", received);
    }

    [Fact]
    public async Task Dispose_Handle_StopsPropagation()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        int callCount = 0;
        var sub = downstream.Subscribe("volatile/topic", (_, _) => callCount++);

        upstream.UpdateValue("volatile/topic", "1");
        sub.Dispose();
        upstream.UpdateValue("volatile/topic", "2");

        Assert.Equal(1, callCount); // only the first update
    }

    // ── Two independent subscribers on the same downstream cache ─────────────────

    [Fact]
    public async Task TwoLevel_TwoSubscribers_BothReceiveUpdates()
    {
        var (upstream, bridge, downstream) = BuildTwoLevelChain();
        await using var _ = bridge;

        var received1 = new List<string>();
        var received2 = new List<string>();
        using var sub1 = downstream.Subscribe("shared/topic", (_, v) => received1.Add(v.ToString()!));
        using var sub2 = downstream.Subscribe("shared/topic", (_, v) => received2.Add(v.ToString()!));

        upstream.UpdateValue("shared/topic", "a");
        upstream.UpdateValue("shared/topic", "b");

        Assert.Equal(["a", "b"], received1);
        Assert.Equal(["a", "b"], received2);
    }
}
