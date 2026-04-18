namespace MqttDashboard.Server.Hubs;

/// <summary>
/// Singleton that tracks the number of currently connected SignalR clients.
/// </summary>
public class HubConnectionTracker
{
    private int _count = 0;
    public int ConnectedCount => _count;
    public void Increment() => Interlocked.Increment(ref _count);
    public void Decrement() => Interlocked.Decrement(ref _count);
}
