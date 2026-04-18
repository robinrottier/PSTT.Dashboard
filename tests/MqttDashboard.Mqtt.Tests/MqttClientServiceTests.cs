using MQTTnet;

namespace MqttDashboard.Mqtt.Tests;

/// <summary>
/// Tests for <see cref="MqttClientService"/> against an in-process MQTT broker.
/// Verifies that the service connects, subscribes to topics via the
/// <see cref="MqttTopicSubscriptionManager"/>, receives messages from the broker,
/// and can publish messages back.
/// </summary>
public class MqttClientServiceTests : IClassFixture<InProcessMqttBrokerFixture>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly InProcessMqttBrokerFixture _broker;
    private MqttClientService? _service;
    private MqttTopicSubscriptionManager? _subscriptionManager;

    public MqttClientServiceTests(InProcessMqttBrokerFixture broker)
    {
        _broker = broker;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<(MqttClientService service, MqttTopicSubscriptionManager manager)> StartAsync()
    {
        var (svc, mgr, _) = await MqttTestHelpers.StartServiceAsync(_broker.Port);
        _service = svc;
        _subscriptionManager = mgr;
        return (svc, mgr);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Service_ConnectsToInProcessBroker()
    {
        var (_, _, monitor) = await MqttTestHelpers.StartServiceAsync(_broker.Port);
        // StartServiceAsync already waits for Connected; if we got here the connection succeeded.
        Assert.Equal(MqttConnectionState.Connected, monitor.State);
    }

    [Fact]
    public async Task Subscribe_ThenPublishFromExternalClient_MessageReceived()
    {
        var (svc, mgr) = await StartAsync();

        // Subscribe via the subscription manager (same path as production code).
        await mgr.SubscribeClientToTopicAsync("test-client", "mqtt/test/receive");

        // Set up a receiver on OnMessagePublished.
        var received = new TaskCompletionSource<(string topic, string payload)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        svc.OnMessagePublished += (t, p, _) =>
        {
            received.TrySetResult((t, p));
            return Task.CompletedTask;
        };

        // Publish from a separate MQTT client → broker → MqttClientService.
        using var publisher = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port);
        await MqttTestHelpers.PublishAsync(publisher, "mqtt/test/receive", "hello-from-broker");

        var (topic, payload) = await received.Task.WaitAsync(Timeout);
        Assert.Equal("mqtt/test/receive", topic);
        Assert.Equal("hello-from-broker", payload);
    }

    [Fact]
    public async Task Subscribe_WildcardTopic_MatchingMessagesReceived()
    {
        var (svc, mgr) = await StartAsync();

        await mgr.SubscribeClientToTopicAsync("test-client", "sensors/+/temp");

        var messages = new List<string>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.OnMessagePublished += (t, p, _) =>
        {
            lock (messages)
            {
                messages.Add(t);
                if (messages.Count >= 2) tcs.TrySetResult();
            }
            return Task.CompletedTask;
        };

        using var publisher = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port);
        await MqttTestHelpers.PublishAsync(publisher, "sensors/room1/temp", "22");
        await MqttTestHelpers.PublishAsync(publisher, "sensors/room2/temp", "23");
        // This should NOT match and must not increment the counter.
        await MqttTestHelpers.PublishAsync(publisher, "sensors/room1/humidity", "60");

        await tcs.Task.WaitAsync(Timeout);

        Assert.Contains("sensors/room1/temp", messages);
        Assert.Contains("sensors/room2/temp", messages);
        Assert.DoesNotContain("sensors/room1/humidity", messages);
    }

    [Fact]
    public async Task Publish_MessageDeliveredToBrokerSubscriber()
    {
        var (svc, _) = await StartAsync();

        // Set up an external subscriber that will receive what MqttClientService publishes.
        using var subscriber = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port, "ext-sub");
        await subscriber.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("mqtt/publish/test")
            .Build());
        var received = MqttTestHelpers.WaitForMessageAsync(subscriber, "mqtt/publish/test", Timeout);

        // Publish through MqttClientService.
        await svc.PublishMessageAsync("mqtt/publish/test", "sent-from-service");

        var payload = await received;
        Assert.Equal("sent-from-service", payload);
    }

    [Fact]
    public async Task UnsubscribeClient_NoMoreMessagesDelivered()
    {
        var (svc, mgr) = await StartAsync();

        await mgr.SubscribeClientToTopicAsync("test-client", "mqtt/unsub/test");

        var receivedCount = 0;
        svc.OnMessagePublished += (_, _, _) => { receivedCount++; return Task.CompletedTask; };

        using var publisher = await MqttTestHelpers.ConnectExternalClientAsync(_broker.Port);

        // Deliver one message while subscribed.
        await MqttTestHelpers.PublishAsync(publisher, "mqtt/unsub/test", "msg1");
        await Task.Delay(300); // let it arrive

        // Unsubscribe — gracePeriodMs is 0 so the broker subscription drops immediately.
        await mgr.UnsubscribeClientFromTopicAsync("test-client", "mqtt/unsub/test");
        await Task.Delay(200);

        var countAfterUnsub = receivedCount;

        // Publish again — should not be received.
        await MqttTestHelpers.PublishAsync(publisher, "mqtt/unsub/test", "msg2");
        await Task.Delay(300);

        Assert.Equal(1, countAfterUnsub);
        Assert.Equal(1, receivedCount); // no second message
    }

    public async ValueTask DisposeAsync()
    {
        if (_service != null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _service.StopAsync(cts.Token);
            _service.Dispose();
        }
    }
}
