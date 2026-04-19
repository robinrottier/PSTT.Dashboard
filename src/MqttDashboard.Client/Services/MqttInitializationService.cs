using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using PSTT.Data;
using PSTT.Remote;

namespace PSTT.Dashboard.Services;

/// <summary>
/// Service to initialize the PSTT data connection and restore topic subscriptions on startup.
/// <list type="bullet">
///   <item>Blazor Server interactive circuit: scoped <see cref="ICache{TKey,TValue}"/> is already wired.</item>
///   <item>WASM: connects <see cref="RemoteCache{TValue}"/> to the server SignalR hub.</item>
///   <item>SSR pre-render: defers connection, returns early.</item>
/// </list>
/// </summary>
public class MqttInitializationService
{
    private readonly ApplicationState _appState;
    private readonly IDashboardService _dashboardService;
    private readonly NavigationManager _navigationManager;
    private readonly IAuthService _authService;
    private readonly RenderModeOptions? _renderModeOptions;
    private readonly ILogger<MqttInitializationService>? _logger;
    private bool _initialized = false;

    public MqttInitializationService(
        ApplicationState appState,
        IDashboardService dashboardService,
        NavigationManager navigationManager,
        IAuthService authService,
        RenderModeOptions? renderModeOptions = null,
        ILogger<MqttInitializationService>? logger = null)
    {
        _appState = appState;
        _dashboardService = dashboardService;
        _navigationManager = navigationManager;
        _authService = authService;
        _renderModeOptions = renderModeOptions;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the connection. Pass <c>RendererInfo.IsInteractive</c> from the calling
    /// component so the service can distinguish SSR pre-render from an active circuit.
    /// </summary>
    public async Task InitializeAsync(bool isInteractive = false)
    {
        if (_initialized)
        {
            _logger?.LogInformation("MQTT already initialized, skipping");
            return;
        }

        try
        {
            _logger?.LogInformation("Starting MQTT initialization...");

            var (isAdmin, authEnabled, readOnly) = await _authService.GetStatusAsync();
            _appState.SetAuthState(isAdmin, authEnabled, readOnly);

            var defaultDashboard = await _dashboardService.LoadDashboardAsync();
            if (defaultDashboard?.MqttSubscriptions?.Count > 0)
                _appState.SetSubscribedTopics(defaultDashboard.MqttSubscriptions);
            _logger?.LogInformation("Loaded {Count} saved subscriptions", _appState.SubscribedTopics.Count);

            // SSR pre-render: defer connection
            if (!OperatingSystem.IsBrowser() && !isInteractive)
            {
                _logger?.LogInformation("MQTT initialization deferred: SSR pre-render");
                if (_renderModeOptions?.IsWasmCapable == true)
                    _initialized = true; // WASM will connect in browser; mark done for SSR pass
                return;
            }

            // WASM: connect RemoteCache to the server hub
            if (OperatingSystem.IsBrowser() && _appState.DataCache is RemoteCache<string> remoteCache)
            {
                var hubUrl = _navigationManager.ToAbsoluteUri("cachehub").ToString();
                _logger?.LogInformation("Connecting WASM remote cache to {Url}", hubUrl);
                await remoteCache.ConnectAsync();
            }

            // Track MQTT connection status via the well-known status topic
            _appState.DataCache.Subscribe(DashboardTopics.MqttStatus, async sub =>
            {
                if (sub.Status.IsPending) return;
                var connected = "Connected".Equals(sub.Value, StringComparison.OrdinalIgnoreCase);
                _appState.SetMqttConnectionStatus(sub.Value ?? "Unknown", connected);
                await Task.CompletedTask;
            });

            // Accumulate message history for all received values
            _appState.DataCache.Subscribe("#", async sub =>
            {
                if (sub.Status.IsPending) return;
                _appState.AddMessage(new Models.MqttDataMessage
                {
                    Topic = sub.Key,
                    Payload = sub.Value ?? string.Empty,
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            });

            _initialized = true;
            _logger?.LogInformation("MQTT initialization completed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during MQTT initialization");
            _appState.SetMqttConnectionStatus($"Error: {ex.Message}", false);
        }
    }

    public bool IsInitialized => _initialized;
}