namespace MqttDashboard.Data;

/// <summary>
/// An <see cref="IDataServer"/> that bridges data from an upstream <see cref="IDataCache"/>
/// into a downstream cache. Subscribing/unsubscribing on the upstream cache drives demand
/// just like a normal server, but the data source is another in-process cache rather than
/// an external transport (MQTT broker, SignalR hub, etc.).
/// <para>
/// Typical use: Blazor Server circuits each have a per-circuit <see cref="DataCache"/>;
/// a <c>CacheBridgeDataServer</c> backed by the singleton <c>ServerDataCache</c> is
/// registered with each circuit cache via <see cref="IDataCache.RegisterServer"/>.
/// </para>
/// <para>
/// Status and reconnect events are optionally forwarded from a <paramref name="statusSource"/>
/// <see cref="IDataServer"/>. When <see cref="StartAsync"/> is called it subscribes to
/// those events <em>and</em> calls <c>statusSource.StartAsync()</c>, which causes the
/// status source to re-fire its current status — seeding the circuit UI without needing
/// a separate status-query mechanism.
/// </para>
/// </summary>
public sealed class CacheBridgeDataServer : IDataServer
{
    private readonly IDataCache _upstream;
    private readonly IDataServer? _statusSource;

    private readonly Dictionary<string, IDisposable> _handles = new();
    private readonly object _lock = new();

    private Action<string, bool>? _statusChangedHandler;
    private Action? _reconnectedHandler;

    public event Action<string, object>? ValueUpdated;
    public event Action? Reconnected;
    public event Action<string, bool>? StatusChanged;

    /// <param name="upstream">The upstream <see cref="IDataCache"/> that acts as the data source.</param>
    /// <param name="statusSource">
    ///   Optional <see cref="IDataServer"/> whose <see cref="IDataServer.StatusChanged"/> and
    ///   <see cref="IDataServer.Reconnected"/> events are forwarded to this bridge's subscribers.
    ///   Typically the singleton <c>MqttDataServer</c>.
    /// </param>
    public CacheBridgeDataServer(IDataCache upstream, IDataServer? statusSource = null)
    {
        _upstream = upstream;
        _statusSource = statusSource;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Wires status/reconnect event forwarding from the <c>statusSource</c> provided at construction,
    /// then calls <c>statusSource.StartAsync()</c> so the source re-fires its current status to the
    /// newly registered handler — seeding the circuit's MQTT connection indicator immediately.
    /// </remarks>
    public async Task StartAsync(string serverUrl = "")
    {
        if (_statusSource != null)
        {
            _statusChangedHandler = (s, c) => StatusChanged?.Invoke(s, c);
            _reconnectedHandler = () => Reconnected?.Invoke();
            _statusSource.StatusChanged += _statusChangedHandler;
            _statusSource.Reconnected += _reconnectedHandler;

            // Ask the status source to re-fire its current status so the new circuit
            // gets the current MQTT connection state without waiting for the next change.
            await _statusSource.StartAsync(serverUrl);
        }
    }

    /// <inheritdoc/>
    public Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0)
        => _upstream.PublishAsync(topic, payload, retain, qos);

    /// <inheritdoc/>
    /// <remarks>Idempotent: subscribing the same topic twice is a no-op on the second call.</remarks>
    public Task SubscribeAsync(string topic)
    {
        lock (_lock)
        {
            if (_handles.ContainsKey(topic)) return Task.CompletedTask;
            var handle = _upstream.Subscribe(topic, (t, v) => ValueUpdated?.Invoke(t, v));
            _handles[topic] = handle;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsubscribeAsync(string topic)
    {
        lock (_lock)
        {
            if (_handles.TryGetValue(topic, out var h))
            {
                h.Dispose();
                _handles.Remove(topic);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_statusSource != null)
        {
            if (_statusChangedHandler != null) _statusSource.StatusChanged -= _statusChangedHandler;
            if (_reconnectedHandler != null) _statusSource.Reconnected -= _reconnectedHandler;
        }

        lock (_lock)
        {
            foreach (var h in _handles.Values) h.Dispose();
            _handles.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
