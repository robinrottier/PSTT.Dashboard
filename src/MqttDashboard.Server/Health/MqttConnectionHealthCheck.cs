using Microsoft.Extensions.Diagnostics.HealthChecks;
using PSTT.Mqtt;

namespace MqttDashboard.Server.Health;

public class MqttConnectionHealthCheck : IHealthCheck
{
    private readonly MqttCache<string> _mqttCache;

    public MqttConnectionHealthCheck(MqttCache<string> mqttCache)
    {
        _mqttCache = mqttCache;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = _mqttCache.IsConnected
            ? HealthCheckResult.Healthy("MQTT broker connected")
            : HealthCheckResult.Degraded("MQTT broker disconnected");
        return Task.FromResult(result);
    }
}
