using Microsoft.Extensions.Diagnostics.HealthChecks;
using MqttDashboard.Mqtt;

namespace MqttDashboard.Server.Health;

public class MqttConnectionHealthCheck : IHealthCheck
{
    private readonly MqttConnectionMonitor _monitor;

    public MqttConnectionHealthCheck(MqttConnectionMonitor monitor)
    {
        _monitor = monitor;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["broker"] = _monitor.Broker,
            ["state"] = _monitor.State.ToString(),
            ["reconnectAttempts"] = _monitor.ReconnectAttempts
        };

        var result = _monitor.State switch
        {
            MqttConnectionState.Connected    => HealthCheckResult.Healthy("MQTT broker connected", data),
            MqttConnectionState.Connecting   => HealthCheckResult.Degraded($"Connecting to MQTT broker (attempt {_monitor.ReconnectAttempts})", data: data),
            MqttConnectionState.Failed       => HealthCheckResult.Unhealthy("MQTT broker connection failed", data: data),
            _                                => HealthCheckResult.Unhealthy("MQTT broker disconnected", data: data),
        };

        return Task.FromResult(result);
    }
}
