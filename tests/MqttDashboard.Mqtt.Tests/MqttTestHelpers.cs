using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;

namespace MqttDashboard.Mqtt.Tests;

/// <summary>
/// Helpers shared across MQTT test classes.
/// </summary>
internal static class MqttTestHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates, starts, and waits for a <see cref="MqttClientService"/> to connect to the
    /// given <paramref name="brokerPort"/>. Returns a tuple of the service and its support
    /// objects so tests can interact with them directly.
    /// </summary>
    public static async Task<(MqttClientService service,
                               MqttTopicSubscriptionManager subscriptionManager,
                               MqttConnectionMonitor connectionMonitor)>
        StartServiceAsync(int brokerPort, CancellationToken ct = default)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MqttSettings:Broker"] = "127.0.0.1",
                ["MqttSettings:Port"]   = brokerPort.ToString(),
            })
            .Build();

        var subscriptionManager = new MqttTopicSubscriptionManager(gracePeriodMs: 0);
        var connectionMonitor   = new MqttConnectionMonitor();
        var logger              = NullLogger<MqttClientService>.Instance;
        var service             = new MqttClientService(logger, config, subscriptionManager, connectionMonitor);

        // Start the background service (non-blocking — runs in a background task).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = service.StartAsync(cts.Token);

        // Wait until actually connected.
        var deadline = DateTime.UtcNow + DefaultTimeout;
        while (connectionMonitor.State != MqttConnectionState.Connected)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"MqttClientService did not connect to broker on port {brokerPort} within {DefaultTimeout.TotalSeconds}s.");
            await Task.Delay(50, ct);
        }

        return (service, subscriptionManager, connectionMonitor);
    }

    /// <summary>
    /// Creates and connects an independent MQTT client to the broker — used as a publisher or
    /// external observer in tests.
    /// </summary>
    public static async Task<IMqttClient> ConnectExternalClientAsync(int brokerPort,
        string? clientId = null)
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", brokerPort)
            .WithClientId(clientId ?? "ExternalClient_" + Guid.NewGuid().ToString("N")[..6])
            .Build();

        var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(options);
        return client;
    }

    /// <summary>
    /// Publishes an MQTT message from the given client and returns immediately.
    /// </summary>
    public static Task PublishAsync(IMqttClient client, string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();
        return client.PublishAsync(message);
    }

    /// <summary>
    /// Returns a <see cref="Task{T}"/> that resolves to the first message received on
    /// <paramref name="client"/> matching <paramref name="topic"/>, or throws
    /// <see cref="TimeoutException"/> if nothing arrives within <paramref name="timeout"/>.
    /// </summary>
    public static Task<string> WaitForMessageAsync(IMqttClient client, string topic,
        TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.Topic == topic)
                tcs.TrySetResult(e.ApplicationMessage.ConvertPayloadToString());
            return Task.CompletedTask;
        };
        return tcs.Task.WaitAsync(timeout ?? DefaultTimeout);
    }
}
