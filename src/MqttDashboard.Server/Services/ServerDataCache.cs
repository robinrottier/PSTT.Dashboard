using MqttDashboard.Data;
using MqttDashboard.Mqtt;

namespace MqttDashboard.Server.Services;

/// <summary>
/// Singleton server-side <see cref="DataCache"/> that accumulates ALL MQTT values
/// received since the server started. Registered with <see cref="MqttDataServer"/>
/// on construction so incoming messages are automatically stored here.
/// <para>
/// Per-circuit caches subscribe to this cache via <see cref="CacheBridgeDataServer"/>
/// rather than hooking MQTT events directly. This means:
/// <list type="bullet">
///   <item>Value history is preserved across circuit reconnections.</item>
///   <item>Widgets see cached values immediately on mount (no wait for first MQTT message).</item>
///   <item>Broker-level subscriptions are ref-counted across all circuits by
///         <see cref="MqttDataServer"/> + <see cref="MqttTopicSubscriptionManager"/>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ServerDataCache : DataCache, IServerSnapshotCache
{
    public ServerDataCache(MqttDataServer mqttDataServer)
    {
        RegisterServer(mqttDataServer);
    }
}
