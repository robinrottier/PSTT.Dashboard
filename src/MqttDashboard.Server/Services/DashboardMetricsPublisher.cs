using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PSTT.Data;
using PSTT.Mqtt;
using System.Reflection;

namespace MqttDashboard.Server.Services;

/// <summary>
/// Hosted service that publishes live diagnostic data as virtual
/// <c>$DASHBOARD/*</c> topics into <see cref="ServerDataCache"/>.
/// Any widget or component can subscribe to these topics to display
/// current time, uptime, version, MQTT status, etc., without ad-hoc service calls.
/// </summary>
public sealed class DashboardMetricsPublisher : BackgroundService
{
    private readonly ServerDataCache _cache;
    private readonly MqttCache<string> _mqttCache;
    private readonly UpdateCheckService _updateCheckService;
    private readonly ILogger<DashboardMetricsPublisher> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public DashboardMetricsPublisher(
        ServerDataCache cache,
        MqttCache<string> mqttCache,
        UpdateCheckService updateCheckService,
        ILogger<DashboardMetricsPublisher> logger)
    {
        _cache = cache;
        _mqttCache = mqttCache;
        _updateCheckService = updateCheckService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = _cache.PublishAsync(DashboardTopics.Version, ReadVersion());

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _ = _cache.PublishAsync(DashboardTopics.Time, DateTime.UtcNow.ToString("o"));
                _ = _cache.PublishAsync(DashboardTopics.Uptime, FormatUptime(DateTime.UtcNow - _startTime));
                _ = _cache.PublishAsync(DashboardTopics.MqttTopicCount, _cache.Count.ToString());
                _ = _cache.PublishAsync(DashboardTopics.MqttStatus, _mqttCache.IsConnected ? "Connected" : "Disconnected");

                var latest = _updateCheckService.UpdateInfo.LatestVersion;
                if (!string.IsNullOrEmpty(latest))
                    _ = _cache.PublishAsync(DashboardTopics.VersionLatest, latest);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private static string ReadVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";
        var plusIdx = version.IndexOf('+');
        if (plusIdx > 0) version = version[..plusIdx];
        return version;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours:D2}h {uptime.Minutes:D2}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";
        return $"{uptime.Minutes:D2}m {uptime.Seconds:D2}s";
    }
}
