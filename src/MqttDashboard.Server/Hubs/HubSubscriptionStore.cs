using System.Collections.Concurrent;

namespace MqttDashboard.Server.Hubs;

/// <summary>
/// Singleton store for per-connection <see cref="MqttDashboard.Data.IDataCache"/> subscription handles
/// in <see cref="DataHub"/>. Persists across hub method invocations (hubs are
/// transient — one instance per invocation).
/// </summary>
public sealed class HubSubscriptionStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IDisposable>> _subs = new();

    public bool IsSubscribed(string connectionId, string topic) =>
        _subs.TryGetValue(connectionId, out var topics) && topics.ContainsKey(topic);

    public bool TryAdd(string connectionId, string topic, IDisposable handle)
    {
        var topics = _subs.GetOrAdd(connectionId, _ => new ConcurrentDictionary<string, IDisposable>());
        return topics.TryAdd(topic, handle);
    }

    public bool TryRemove(string connectionId, string topic, out IDisposable? handle)
    {
        handle = null;
        return _subs.TryGetValue(connectionId, out var topics) && topics.TryRemove(topic, out handle);
    }

    public IEnumerable<IDisposable> RemoveAll(string connectionId)
    {
        if (_subs.TryRemove(connectionId, out var topics))
            return topics.Values;
        return [];
    }
}
