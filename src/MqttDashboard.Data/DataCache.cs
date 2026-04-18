using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MqttDashboard.Data;

/// <summary>
/// Thread-safe in-memory topic cache. Implements <see cref="IDataCache"/>.
/// <para>
/// String values are sanitized to strip invalid XML characters before storage so that
/// widgets can safely inject values into SVG / DOM text nodes.
/// </para>
/// <para>
/// When an <see cref="IDataServer"/> is registered, the cache tracks the first and last
/// subscriber for each topic. On first <see cref="Subscribe"/> for a topic that has no
/// cached value, <see cref="IDataServer.SubscribeAsync"/> is called. When the last
/// subscriber for a topic disposes, <see cref="IDataServer.UnsubscribeAsync"/> is called.
/// </para>
/// </summary>
public class DataCache : IDataCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    // Exact-topic callbacks
    private readonly Dictionary<string, List<Action<string, object>>> _watchers = new();
    // Wildcard callbacks (regex + callback pairs)
    private readonly List<(Regex pattern, Action<string, object> callback)> _wildcardWatchers = new();
    // Subscriber ref-counts per topic (for server notification)
    private readonly Dictionary<string, int> _subscriberCounts = new();
    private readonly object _watcherLock = new();

    private IDataServer? _server;

    // ── IDataCache ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0)
    {
        // Update locally immediately so subscribers see the new value without waiting
        // for the upstream echo (broker may not echo depending on settings).
        UpdateValue(topic, payload);
        if (_server != null)
            await _server.PublishAsync(topic, payload, retain, qos);
    }

    /// <inheritdoc/>
    public void UpdateValue(string topic, object value)
    {
        if (value is string s)
            value = XmlPayloadHelper.StripInvalidXmlChars(s);
        _cache[topic] = value;
        NotifyWatchers(topic, value);
    }

    /// <inheritdoc/>
    public object? GetValue(string topic) =>
        _cache.TryGetValue(topic, out var v) ? v : null;

    /// <inheritdoc/>
    public bool TryGetValue<T>(string topic, out T? value)
    {
        if (_cache.TryGetValue(topic, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(string topic, Action<string, object> callback)
    {
        bool isFirstSubscriber = false;
        bool hasValue = _cache.ContainsKey(topic);

        if (topic.Contains('#') || topic.Contains('+'))
        {
            var regex = new Regex(TopicMatcher.ToRegexPattern(topic));
            lock (_watcherLock)
            {
                _wildcardWatchers.Add((regex, callback));
                if (!_subscriberCounts.TryGetValue(topic, out var cnt) || cnt == 0)
                {
                    _subscriberCounts[topic] = 1;
                    isFirstSubscriber = true;
                }
                else
                {
                    _subscriberCounts[topic] = cnt + 1;
                }
            }
            if (isFirstSubscriber && !hasValue)
                _ = NotifyServerSubscribeAsync(topic);
            return new WildcardSubscribeHandle(this, regex, callback, topic);
        }

        lock (_watcherLock)
        {
            if (!_watchers.TryGetValue(topic, out var list))
                _watchers[topic] = list = new List<Action<string, object>>();
            list.Add(callback);

            if (!_subscriberCounts.TryGetValue(topic, out var cnt) || cnt == 0)
            {
                _subscriberCounts[topic] = 1;
                isFirstSubscriber = true;
            }
            else
            {
                _subscriberCounts[topic] = cnt + 1;
            }
        }

        // Seed immediately from cache if value already present
        var existing = GetValue(topic);
        if (existing != null)
            try { callback(topic, existing); } catch { }

        if (isFirstSubscriber && !hasValue)
            _ = NotifyServerSubscribeAsync(topic);

        return new SubscribeHandle(this, topic, callback);
    }

    /// <inheritdoc/>
    public bool HasServer => _server != null;

    /// <inheritdoc/>
    public void RegisterServer(IDataServer server)
    {
        _server = server;
        server.ValueUpdated += (topic, value) => UpdateValue(topic, value);
        server.Reconnected += ResubscribeAll;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetAllTopics() => _cache.Keys;

    /// <inheritdoc/>
    public Dictionary<string, object> GetValuesByPattern(string pattern)
    {
        var regex = new Regex(TopicMatcher.ToRegexPattern(pattern));
        var result = new Dictionary<string, object>();
        foreach (var kvp in _cache)
            if (regex.IsMatch(kvp.Key))
                result[kvp.Key] = kvp.Value;
        return result;
    }

    /// <inheritdoc/>
    public void Clear() => _cache.Clear();

    // ── Internal helpers ─────────────────────────────────────────────────────────

    private async Task NotifyServerSubscribeAsync(string topic)
    {
        if (_server != null)
            try { await _server.SubscribeAsync(topic); } catch { }
    }

    private async Task NotifyServerUnsubscribeAsync(string topic)
    {
        if (_server != null)
            try { await _server.UnsubscribeAsync(topic); } catch { }
    }

    private void ResubscribeAll()
    {
        List<string> activeTopics;
        lock (_watcherLock)
            activeTopics = _subscriberCounts.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();

        foreach (var topic in activeTopics)
            _ = NotifyServerSubscribeAsync(topic);
    }

    private void NotifyWatchers(string topic, object value)
    {
        List<Action<string, object>>? exact = null;
        List<Action<string, object>>? wildcards = null;

        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(topic, out var list))
                exact = new List<Action<string, object>>(list);

            wildcards = _wildcardWatchers
                .Where(w => w.pattern.IsMatch(topic))
                .Select(w => w.callback)
                .ToList();
        }

        foreach (var cb in exact ?? [])
            try { cb(topic, value); } catch { }

        foreach (var cb in wildcards ?? [])
            try { cb(topic, value); } catch { }
    }

    private void RemoveSubscriber(string topic, Action<string, object> callback)
    {
        bool isLast = false;
        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(topic, out var list))
            {
                list.Remove(callback);
                if (list.Count == 0) _watchers.Remove(topic);
            }
            if (_subscriberCounts.TryGetValue(topic, out var cnt))
            {
                var newCnt = Math.Max(0, cnt - 1);
                _subscriberCounts[topic] = newCnt;
                isLast = newCnt == 0;
            }
        }
        if (isLast)
            _ = NotifyServerUnsubscribeAsync(topic);
    }

    private void RemoveWildcardSubscriber(Regex pattern, Action<string, object> callback, string topic)
    {
        bool isLast = false;
        lock (_watcherLock)
        {
            var idx = _wildcardWatchers.FindIndex(
                w => ReferenceEquals(w.pattern, pattern) && ReferenceEquals(w.callback, callback));
            if (idx >= 0) _wildcardWatchers.RemoveAt(idx);

            if (_subscriberCounts.TryGetValue(topic, out var cnt))
            {
                var newCnt = Math.Max(0, cnt - 1);
                _subscriberCounts[topic] = newCnt;
                isLast = newCnt == 0;
            }
        }
        if (isLast)
            _ = NotifyServerUnsubscribeAsync(topic);
    }

    // ── Inner handle classes ──────────────────────────────────────────────────────

    private sealed class SubscribeHandle(DataCache cache, string topic, Action<string, object> callback) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            cache.RemoveSubscriber(topic, callback);
            _disposed = true;
        }
    }

    private sealed class WildcardSubscribeHandle(DataCache cache, Regex pattern, Action<string, object> callback, string topic) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            cache.RemoveWildcardSubscriber(pattern, callback, topic);
            _disposed = true;
        }
    }
}
