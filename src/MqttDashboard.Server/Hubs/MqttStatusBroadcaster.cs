using Microsoft.AspNetCore.SignalR;
using MqttDashboard.Mqtt;

namespace MqttDashboard.Server.Hubs;

/// <summary>
/// Singleton that bridges <see cref="MqttConnectionMonitor"/> state-change events into
/// SignalR broadcasts. Keeps SignalR knowledge out of <see cref="MqttClientService"/>.
/// <para>
/// Registered as a singleton; the constructor wires the event once for the lifetime
/// of the application.
/// </para>
/// </summary>
public sealed class MqttStatusBroadcaster
{
    public MqttStatusBroadcaster(
        IHubContext<DataHub> hubContext,
        MqttConnectionMonitor connectionMonitor)
    {
        connectionMonitor.OnStateChanged += async (state, attempts) =>
            await hubContext.Clients.All.SendAsync("MqttConnectionStatus", state.ToString(), attempts);
    }
}
