namespace MqttDashboard.Data;

/// <summary>
/// In-memory pub/sub data cache keyed by topic strings.
/// Stores the most recent value per topic and notifies registered subscribers on every update.
/// Supports MQTT-style wildcard patterns (<c>+</c> single-level, <c>#</c> multi-level) in <see cref="Subscribe"/>.
/// </summary>
public interface IDataCache
{
    /// <summary>
    /// Publish a value to the upstream data source (e.g. MQTT broker or SignalR hub).
    /// Also updates the local cache immediately so subscribers see the change without
    /// waiting for the upstream echo.
    /// </summary>
    Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0);

    /// <summary>Store a value and notify all matching subscribers.</summary>
    void UpdateValue(string topic, object value);

    /// <summary>Retrieve the last cached value for a topic, or <c>null</c> if not yet received.
    /// This never triggers upstream data requests — use <see cref="Subscribe"/> for reactive updates.</summary>
    object? GetValue(string topic);

    /// <summary>Type-safe retrieval. Returns <c>false</c> if the topic is absent or has the wrong type.</summary>
    bool TryGetValue<T>(string topic, out T? value);

    /// <summary>
    /// Subscribe to value changes for <paramref name="topic"/>.
    /// Supports MQTT wildcards: <c>+</c> (single level) and <c>#</c> (multi-level).
    /// If a registered <see cref="IDataServer"/> has no cached value for the topic, it will be notified
    /// to start providing data (demand-driven upstream subscription).
    /// Dispose the returned handle to unsubscribe.
    /// </summary>
    IDisposable Subscribe(string topic, Action<string, object> callback);

    /// <summary>Register an <see cref="IDataServer"/> that provides upstream data for this cache.
    /// The server will be notified when new topics need data and when topics are no longer watched.</summary>
    void RegisterServer(IDataServer server);

    /// <summary>All topics currently in the cache.</summary>
    IEnumerable<string> GetAllTopics();

    /// <summary>All cached values whose topic matches the given MQTT wildcard pattern.</summary>
    Dictionary<string, object> GetValuesByPattern(string pattern);

    /// <summary>Remove all cached values.</summary>
    void Clear();

    /// <summary>Returns <c>true</c> if an <see cref="IDataServer"/> has already been registered
    /// via <see cref="RegisterServer"/>. Use this to avoid double-registration in same-process hosts
    /// where the cache is pre-wired (e.g. <c>ServerDataCache</c> registers <c>MqttDataServer</c>
    /// in its constructor).</summary>
    bool HasServer { get; }
}
