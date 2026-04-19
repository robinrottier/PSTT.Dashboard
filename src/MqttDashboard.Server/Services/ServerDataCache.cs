using PSTT.Data;
using PSTT.Mqtt;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Singleton server-side cache that accumulates all MQTT values.
/// Backed by <see cref="MqttCache{TValue}"/> as upstream; publishes from UI widgets are
/// forwarded to the broker via <c>forwardPublish: true</c>.
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
}
