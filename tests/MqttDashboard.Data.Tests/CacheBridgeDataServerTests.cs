using MqttDashboard.Data;
using Moq;

namespace MqttDashboard.Data.Tests;

public class CacheBridgeDataServerTests
{
    // ── SubscribeAsync / UnsubscribeAsync ────────────────────────────────────────

    [Fact]
    public async Task SubscribeAsync_CallsUpstreamSubscribe()
    {
        var upstreamCache = new DataCache();
        string? received = null;
        await using var bridge = new CacheBridgeDataServer(upstreamCache);

        await bridge.SubscribeAsync("sensor/temp");

        // Upstream cache now has a subscriber; updating it should fire the bridge's ValueUpdated.
        bridge.ValueUpdated += (t, v) => received = v?.ToString();
        upstreamCache.UpdateValue("sensor/temp", "42");

        Assert.Equal("42", received);
    }

    [Fact]
    public async Task SubscribeAsync_IsIdempotent()
    {
        var upstreamCache = new DataCache();
        await using var bridge = new CacheBridgeDataServer(upstreamCache);

        // Subscribe twice — should not throw and should not duplicate handles.
        await bridge.SubscribeAsync("sensor/temp");
        await bridge.SubscribeAsync("sensor/temp"); // idempotent

        var count = 0;
        bridge.ValueUpdated += (_, _) => count++;
        upstreamCache.UpdateValue("sensor/temp", "1");

        Assert.Equal(1, count); // only one notification
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesHandle_NoMoreNotifications()
    {
        var upstreamCache = new DataCache();
        await using var bridge = new CacheBridgeDataServer(upstreamCache);

        var count = 0;
        bridge.ValueUpdated += (_, _) => count++;

        await bridge.SubscribeAsync("sensor/temp");
        upstreamCache.UpdateValue("sensor/temp", "1");

        await bridge.UnsubscribeAsync("sensor/temp");
        upstreamCache.UpdateValue("sensor/temp", "2");

        Assert.Equal(1, count); // second update not received
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllHandles()
    {
        var upstreamCache = new DataCache();
        await using var bridge = new CacheBridgeDataServer(upstreamCache);

        await bridge.SubscribeAsync("a");
        await bridge.SubscribeAsync("b");

        var count = 0;
        bridge.ValueUpdated += (_, _) => count++;

        await bridge.DisposeAsync();

        upstreamCache.UpdateValue("a", "x");
        upstreamCache.UpdateValue("b", "y");

        Assert.Equal(0, count);
    }

    // ── ValueUpdated forwarding ──────────────────────────────────────────────────

    [Fact]
    public async Task ValueUpdated_FiredWhenUpstreamChanges()
    {
        var upstreamCache = new DataCache();
        await using var bridge = new CacheBridgeDataServer(upstreamCache);

        string? receivedTopic = null;
        string? receivedValue = null;
        bridge.ValueUpdated += (t, v) => { receivedTopic = t; receivedValue = v?.ToString(); };

        await bridge.SubscribeAsync("home/temp");
        upstreamCache.UpdateValue("home/temp", "21.5");

        Assert.Equal("home/temp", receivedTopic);
        Assert.Equal("21.5", receivedValue);
    }

    // ── StatusChanged / Reconnected forwarding ───────────────────────────────────

    [Fact]
    public async Task StartAsync_ForwardsStatusChangedFromStatusSource()
    {
        var upstreamCache = new DataCache();
        var mockStatusSource = new Mock<IDataServer>();
        await using var bridge = new CacheBridgeDataServer(upstreamCache, mockStatusSource.Object);

        string? receivedStatus = null;
        bridge.StatusChanged += (s, _) => receivedStatus = s;

        await bridge.StartAsync();

        // Simulate status change from the source
        mockStatusSource.Raise(s => s.StatusChanged += null, "MQTT Connected (broker:1883)", true);

        Assert.Equal("MQTT Connected (broker:1883)", receivedStatus);
    }

    [Fact]
    public async Task StartAsync_ForwardsReconnectedFromStatusSource()
    {
        var upstreamCache = new DataCache();
        var mockStatusSource = new Mock<IDataServer>();
        await using var bridge = new CacheBridgeDataServer(upstreamCache, mockStatusSource.Object);

        var reconnected = false;
        bridge.Reconnected += () => reconnected = true;

        await bridge.StartAsync();

        mockStatusSource.Raise(s => s.Reconnected += null);

        Assert.True(reconnected);
    }

    [Fact]
    public async Task StartAsync_CallsStatusSourceStartAsync_ForInitialStatus()
    {
        var upstreamCache = new DataCache();
        var mockStatusSource = new Mock<IDataServer>();
        await using var bridge = new CacheBridgeDataServer(upstreamCache, mockStatusSource.Object);

        await bridge.StartAsync("hub-url");

        // Should forward the serverUrl to the status source's StartAsync
        mockStatusSource.Verify(s => s.StartAsync("hub-url"), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_UnwiresStatusEvents()
    {
        var upstreamCache = new DataCache();
        var mockStatusSource = new Mock<IDataServer>();
        await using var bridge = new CacheBridgeDataServer(upstreamCache, mockStatusSource.Object);

        await bridge.StartAsync();

        var count = 0;
        bridge.StatusChanged += (_, _) => count++;

        await bridge.DisposeAsync();

        // After dispose, raising the event on the source should not trigger the bridge handler.
        mockStatusSource.Raise(s => s.StatusChanged += null, "MQTT Connected", true);

        Assert.Equal(0, count);
    }

    // ── No statusSource ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_WithNoStatusSource_DoesNotThrow()
    {
        var upstreamCache = new DataCache();
        await using var bridge = new CacheBridgeDataServer(upstreamCache);

        var ex = await Record.ExceptionAsync(() => bridge.StartAsync());
        Assert.Null(ex);
    }

    // ── Integration: round-trip via DataCache.RegisterServer ─────────────────────

    [Fact]
    public async Task RegisterServer_WithBridge_DataFlowsFromUpstreamToDownstream()
    {
        // upstream (acts like ServerDataCache)
        var upstream = new DataCache();

        // bridge is the IDataServer for the downstream circuit cache
        await using var bridge = new CacheBridgeDataServer(upstream);

        // downstream is the per-circuit cache
        var downstream = new DataCache();
        downstream.RegisterServer(bridge);

        string? received = null;
        using var _ = downstream.Subscribe("room/humidity", (_, v) => received = v?.ToString());

        // The Subscribe call triggers bridge.SubscribeAsync → upstream.Subscribe
        // Simulate data arriving in the upstream cache (from MqttDataServer)
        upstream.UpdateValue("room/humidity", "65");

        Assert.Equal("65", received);
    }
}
