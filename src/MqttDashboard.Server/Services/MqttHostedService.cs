using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PSTT.Mqtt;

namespace MqttDashboard.Server.Services;

/// <summary>
/// Hosted service that connects <see cref="MqttCache{TValue}"/> to the MQTT broker on startup
/// and automatically reconnects when the connection drops.
/// </summary>
public sealed class MqttHostedService : BackgroundService
{
    private readonly MqttCache<string> _mqttCache;
    private readonly ILogger<MqttHostedService> _logger;

    public MqttHostedService(MqttCache<string> mqttCache, ILogger<MqttHostedService> logger)
    {
        _mqttCache = mqttCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_mqttCache.IsConnected)
            {
                try
                {
                    _logger.LogInformation("Connecting to MQTT broker...");
                    await _mqttCache.ConnectAsync(stoppingToken);
                    _logger.LogInformation("MQTT broker connected");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MQTT connection failed, retrying in 10s");
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await _mqttCache.DisposeAsync();
    }
}
