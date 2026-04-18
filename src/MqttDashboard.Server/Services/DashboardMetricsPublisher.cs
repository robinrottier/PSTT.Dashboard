using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MqttDashboard.Mqtt;
using MqttDashboard.Server.Hubs;
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
    private readonly MqttConnectionMonitor _connectionMonitor;
    private readonly HubConnectionTracker _connectionTracker;
    private readonly UpdateCheckService _updateCheckService;
    private readonly ILogger<DashboardMetricsPublisher> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public DashboardMetricsPublisher(
        ServerDataCache cache,
        MqttConnectionMonitor connectionMonitor,
        HubConnectionTracker connectionTracker,
        UpdateCheckService updateCheckService,
        ILogger<DashboardMetricsPublisher> logger)
    {
        _cache = cache;
        _connectionMonitor = connectionMonitor;
        _connectionTracker = connectionTracker;
        _updateCheckService = updateCheckService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PublishVersion();
        PublishMqttStatus();

        _connectionMonitor.OnStateChanged += HandleStateChanged;

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                PublishTimeMetrics();
                PublishDynamicMetrics();
                MaybePublishLatestVersion();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _connectionMonitor.OnStateChanged -= HandleStateChanged;
        }
    }

    private void PublishVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";
        // Strip commit hash suffix (e.g. "1.2.3+abc123" → "1.2.3")
        var plusIdx = version.IndexOf('+');
        if (plusIdx > 0) version = version[..plusIdx];
        _cache.UpdateValue(DashboardTopics.Version, version);
        _logger.LogDebug("[DashboardMetrics] Published version: {Version}", version);
    }

    private void MaybePublishLatestVersion()
    {
        var latest = _updateCheckService.UpdateInfo.LatestVersion;
        if (!string.IsNullOrEmpty(latest))
            _cache.UpdateValue(DashboardTopics.VersionLatest, latest);
    }

    private void PublishMqttStatus()
    {
        var state = _connectionMonitor.State;
        var broker = _connectionMonitor.Broker is { Length: > 0 } b ? b : "unknown";
        var status = state == MqttConnectionState.Connected ? "Connected" : state.ToString();
        _cache.UpdateValue(DashboardTopics.MqttStatus, status);
        _cache.UpdateValue(DashboardTopics.MqttBroker, broker);
    }

    private void PublishTimeMetrics()
    {
        _cache.UpdateValue(DashboardTopics.Time, DateTime.UtcNow.ToString("o"));
        _cache.UpdateValue(DashboardTopics.Uptime, FormatUptime(DateTime.UtcNow - _startTime));
    }

    private void PublishDynamicMetrics()
    {
        _cache.UpdateValue(DashboardTopics.MqttTopicCount, _cache.GetAllTopics().Count().ToString());
        _cache.UpdateValue(DashboardTopics.ClientsCount, _connectionTracker.ConnectedCount.ToString());
    }

    private Task HandleStateChanged(MqttConnectionState state, int reconnectAttempts)
    {
        PublishMqttStatus();
        return Task.CompletedTask;
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
