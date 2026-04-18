using MqttDashboard.Data;
//using MqttDashboard.Models;
using MqttDashboard.Mqtt;
//using MqttDashboard.Server.Hubs;
//using MqttDashboard.Services;
using Microsoft.Extensions.Logging;

//namespace MqttDashboard.Server.Services;
namespace MqttDashboard.Mqtt;

/// <summary>
/// Singleton <see cref="IDataServer"/> that bridges the MQTT broker into the server-side
/// data layer. Feeds all incoming MQTT messages into the <see cref="ServerDataCache"/> via
/// the <see cref="IDataServer.ValueUpdated"/> event.
/// <para>
/// Unlike the old <c>InProcessDataServer</c> this class has <b>no per-circuit state</b>.
/// Topic filtering is handled by subscriber ref-counting inside the upstream caches.
/// It uses <see cref="MqttTopicSubscriptionManager"/> to drive broker-level subscribe /
/// unsubscribe, so hub browser-clients and the <see cref="ServerDataCache"/> correctly
/// share the same broker-side ref-count.
/// </para>
/// <para>
/// Implements <see cref="IDataServer.PublishAsync"/> by forwarding to <see cref="MqttClientService"/>
/// so the rest of the application publishes via the cache without taking a direct dependency
/// on the MQTT client.
/// </para>
/// <para>
/// <b>Lifecycle:</b> event handlers are wired in the constructor because this is a singleton
/// that lives for the entire application lifetime. <see cref="StartAsync"/> is designed to be
/// called multiple times (once per new circuit via <see cref="CacheBridgeDataServer"/>); each
/// call re-fires the current MQTT status so the new circuit UI is seeded correctly.
/// </para>
/// </summary>
public sealed class MqttDataServer : IDataServer, IAsyncDisposable
{
    private readonly MqttClientService _mqttClientService;
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly MqttConnectionMonitor _connectionMonitor;
    //private readonly HubConnectionTracker _connectionTracker;
    private readonly ILogger<MqttDataServer>? _logger;

    /// <summary>Stable connection-ID used when registering with <see cref="MqttTopicSubscriptionManager"/>.</summary>
    private const string ConnectionId = "server-data-cache";

    private bool _wasConnected;

    public event Action<string, object>? ValueUpdated;
    public event Action? Reconnected;
    public event Action<string, bool>? StatusChanged;

    public MqttDataServer(
        MqttClientService mqttClientService,
        MqttTopicSubscriptionManager subscriptionManager,
        MqttConnectionMonitor connectionMonitor,
        //HubConnectionTracker connectionTracker,
        ILogger<MqttDataServer>? logger = null)
    {
        _mqttClientService = mqttClientService;
        _subscriptionManager = subscriptionManager;
        _connectionMonitor = connectionMonitor;
        //_connectionTracker = connectionTracker;
        _logger = logger;

        // Wire events in ctor — safe for a singleton that lives for the app lifetime.
        _mqttClientService.OnMessagePublished += HandleMessagePublished;
        _connectionMonitor.OnStateChanged += HandleConnectionStateChanged;

        _wasConnected = connectionMonitor.State == MqttConnectionState.Connected;
    }

    /// <summary>
    /// Re-fires the current MQTT connection status. Called once per new circuit via
    /// <see cref="CacheBridgeDataServer.StartAsync"/> so each circuit's UI is seeded
    /// without waiting for the next status-change event.
    /// </summary>
    public Task StartAsync(string serverUrl = "")
    {
        var connected = _connectionMonitor.State == MqttConnectionState.Connected;
        var broker = _connectionMonitor.Broker ?? "unknown";
        var status = connected
            ? $"MQTT Connected ({broker})"
            : $"MQTT {_connectionMonitor.State}";
        StatusChanged?.Invoke(status, connected);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers interest in <paramref name="topic"/> with the <see cref="MqttTopicSubscriptionManager"/>
    /// so the broker-level subscription is ref-counted correctly alongside hub browser-clients.
    /// </summary>
    public async Task SubscribeAsync(string topic)
    {
        await _subscriptionManager.SubscribeClientToTopicAsync(ConnectionId, topic);
        _logger?.LogDebug("[MqttDataServer] Subscribed to {Topic}", topic);
    }

    /// <summary>
    /// Releases interest in <paramref name="topic"/> from the <see cref="MqttTopicSubscriptionManager"/>.
    /// If no other clients (hub or server) are subscribed the broker-level subscription is dropped.
    /// </summary>
    public async Task UnsubscribeAsync(string topic)
    {
        await _subscriptionManager.UnsubscribeClientFromTopicAsync(ConnectionId, topic);
        _logger?.LogDebug("[MqttDataServer] Unsubscribed from {Topic}", topic);
    }

    // ── IDataServer.PublishAsync ─────────────────────────────────────────────────

    public async Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0)
    {
        await _mqttClientService.PublishMessageAsync(topic, payload, retain, qos);
        _logger?.LogDebug("[MqttDataServer] Published to {Topic}", topic);
    }

    // ── Private handlers ─────────────────────────────────────────────────────────

    private Task HandleMessagePublished(string topic, string payload, DateTime timestamp)
    {
        _logger?.LogTrace("[MqttDataServer] Dispatching data on {Topic}", topic);
        ValueUpdated?.Invoke(topic, payload);
        return Task.CompletedTask;
    }

    private Task HandleConnectionStateChanged(MqttConnectionState state, int reconnectAttempts)
    {
        var connected = state == MqttConnectionState.Connected;
        var broker = _connectionMonitor.Broker ?? "unknown";
        var status = connected
            ? $"MQTT Connected ({broker})"
            : reconnectAttempts > 0
                ? $"MQTT reconnecting (attempt {reconnectAttempts})..."
                : $"MQTT {state}";
        StatusChanged?.Invoke(status, connected);

        if (connected && _wasConnected)
            Reconnected?.Invoke();

        if (connected)
            _wasConnected = true;

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _mqttClientService.OnMessagePublished -= HandleMessagePublished;
        _connectionMonitor.OnStateChanged -= HandleConnectionStateChanged;
        await _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(ConnectionId);
        _logger?.LogDebug("[MqttDataServer] Disposed");
    }
}
