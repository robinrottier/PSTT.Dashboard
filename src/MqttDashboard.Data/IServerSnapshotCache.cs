namespace MqttDashboard.Data;

/// <summary>
/// Marker interface for a server-side snapshot cache that can be used to seed
/// client-side caches during SSR pre-render. The implementing type is ServerDataCache.
/// </summary>
public interface IServerSnapshotCache : IDataCache { }
