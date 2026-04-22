using PSTT.Data;
using PSTT.Mqtt;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Singleton server-side cache that accumulates all MQTT values.
/// Backed by <see cref="MqttCache{TValue}"/> as upstream; publishes from UI widgets are
/// forwarded to the broker via <c>forwardPublish: true</c>.
/// <para>
/// <c>$</c>-prefixed topics (e.g. <c>$DASHBOARD/*</c>) are internal virtual metrics published
/// by <see cref="DashboardMetricsPublisher"/>. They are intentionally <em>not</em> forwarded to
/// the broker — only stored locally so all connected clients see them via SignalR.
/// </para>
/// <para>
/// All Blazor Server circuits and the PSTT <see cref="PSTT.Remote.RemoteCacheServer{TValue}"/>
/// use this as their upstream so every client sees the same broker data.
/// </para>
/// </summary>
public sealed class ServerDataCache : CacheWithWildcards<string, string>
{
    public ServerDataCache(MqttCache<string> mqttCache)
    {
        SetUpstream(mqttCache, supportsWildcards: true, forwardPublish: true);
    }

    /// <summary>
    /// Prevents <c>$</c>-prefixed topics from being forwarded to the MQTT broker.
    /// Per MQTT 3.1.1 §4.7.2, topics beginning with <c>$</c> are reserved for broker use
    /// and should never be published by application code to the broker.
    /// </summary>
    protected override bool ShouldForwardPublish(string key) => !key.StartsWith('$');
}
