namespace MqttDashboard.Data;

/// <summary>
/// Upstream data provider for an <see cref="IDataCache"/>.
/// An <c>IDataServer</c> is registered with a cache via <see cref="IDataCache.RegisterServer"/>.
/// When a subscriber requests a topic with no cached value the cache calls <see cref="SubscribeAsync"/>;
/// when the last subscriber for a topic disposes the cache calls <see cref="UnsubscribeAsync"/>.
/// The server pushes incoming values back via <see cref="ValueUpdated"/>.
/// </summary>
public interface IDataServer : IAsyncDisposable
{
    /// <summary>
    /// Publish a value upstream (e.g. to the MQTT broker or via SignalR to the server).
    /// Called by <see cref="IDataCache.PublishAsync"/> after updating the local cache.
    /// </summary>
    Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0);

    /// <summary>Fired when the server has a new value for a topic. The cache wires this to <see cref="IDataCache.UpdateValue"/>.</summary>
    event Action<string, object>? ValueUpdated;

    /// <summary>Fired after a reconnection. The cache re-subscribes all currently active topics.</summary>
    event Action? Reconnected;

    /// <summary>Fired when the server's connection state changes.
    /// Parameters: human-readable status string, connected flag.</summary>
    event Action<string, bool>? StatusChanged;

    /// <summary>Connect / start the server. <paramref name="serverUrl"/> is used by transport-backed
    /// implementations (e.g. SignalR hub URL); in-process implementations may ignore it.</summary>
    Task StartAsync(string serverUrl = "");

    /// <summary>Request upstream data for <paramref name="topic"/>.
    /// Called by the cache when the first subscriber registers for a topic that has no cached value.</summary>
    Task SubscribeAsync(string topic);

    /// <summary>Release interest in <paramref name="topic"/> upstream.
    /// Called by the cache when the last subscriber for a topic disposes its handle.</summary>
    Task UnsubscribeAsync(string topic);
}
