using MqttDashboard.Data;
using MqttDashboard.Mqtt;
using MqttDashboard.Server.Hubs;
using MqttDashboard.Server.Services;
using MqttDashboard.Server.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using MqttDashboard.Services;

namespace MqttDashboard.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMqttDashboardServerServices(this IServiceCollection services)
    {
        // Add MQTT Topic Subscription Manager as singleton
        services.AddSingleton<MqttTopicSubscriptionManager>();

        // Add connected client tracker as singleton (tracks SignalR hub connections)
        services.AddSingleton<HubConnectionTracker>();

        // Add MQTT Connection Monitor as singleton
        services.AddSingleton<MqttConnectionMonitor>();

        // Singleton store for per-connection DataCache subscription handles in DataHub.
        services.AddSingleton<HubSubscriptionStore>();

        // Register MqttClientService as both a singleton (injectable) and a hosted service.
        services.AddSingleton<MqttClientService>();
        services.AddHostedService(sp => sp.GetRequiredService<MqttClientService>());
        // Also register as the interface so hubs and tests can inject IMqttClientService.
        services.AddSingleton<IMqttClientService>(sp => sp.GetRequiredService<MqttClientService>());

        // Bridges MqttConnectionMonitor state changes into SignalR hub broadcasts.
        // Keeps SignalR knowledge out of MqttClientService.
        services.AddSingleton<MqttStatusBroadcaster>();

        // Add Diagram Storage Service
        services.AddSingleton<DashboardStorageService>();

        // Login token store for Blazor Server auth flow (one-time tokens, singleton lifetime)
        services.AddSingleton<LoginTokenStore>();

        // Add HttpContextAccessor for DashboardService
        services.AddHttpContextAccessor();

        // Register a scoped HttpClient for use in Blazor components (server-side rendering)
        services.AddScoped<HttpClient>(sp => CreateLoopbackHttpClient(sp));

        services.AddSingleton<MqttDataServer>();

        // Singleton server-side DataCache — accumulates every MQTT value; shared by all circuits.
        services.AddSingleton<ServerDataCache>();
        // Expose ServerDataCache as IServerSnapshotCache so MqttInitializationService can seed
        // AppState.DataCache during SSR pre-render. Per-circuit DataCache instances are unaffected.
        services.AddSingleton<IServerSnapshotCache>(sp => sp.GetRequiredService<ServerDataCache>());

        // Scoped per-circuit IDataServer: bridges the circuit's local DataCache to ServerDataCache.
        // Status and reconnect events are forwarded from the singleton MqttDataServer.
        services.AddScoped<CacheBridgeDataServer>(sp => new CacheBridgeDataServer(
            sp.GetRequiredService<ServerDataCache>(),
            sp.GetRequiredService<MqttDataServer>()));
        services.AddScoped<IDataServer>(sp => sp.GetRequiredService<CacheBridgeDataServer>());

        // Add DashboardService for server-side (in-process, no loopback HTTP)
        services.AddScoped<IDashboardService, ServerDashboardService>();

        // Add AuthService for server-side (in-process, no loopback HTTP)
        services.AddScoped<IAuthService, ServerAuthService>();

        // Add RequireAdminFilter as scoped service
        services.AddScoped<RequireAdminFilter>();

        // Update check service
        services.AddHttpClient("UpdateCheck");
        services.AddSingleton<UpdateCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckService>());

        // Publishes live diagnostic data as virtual $DASHBOARD/* topics into ServerDataCache
        services.AddHostedService<DashboardMetricsPublisher>();

        return services;
    }

    /// <summary>
    /// Registers the additional bindings needed for a same-process host (e.g. MAUI Blazor,
    /// combined desktop host) where the server and client run in a single DI container
    /// with no SignalR transport.
    /// <para>
    /// Call this <b>after</b> <see cref="AddMqttDashboardServerServices"/> and
    /// <see cref="MqttDashboard.Services.ServiceCollectionExtensions.AddMqttDashboardServices"/>.
    /// It registers <see cref="ServerDataCache"/> as the singleton <see cref="MqttDashboard.Data.IDataCache"/>
    /// so that <see cref="MqttDashboard.Services.ApplicationState"/> uses it directly — no per-circuit
    /// cache, no bridge, no SignalR hub needed.
    /// </para>
    /// <para>
    /// <see cref="MqttInitializationService"/> automatically skips re-registering a data server
    /// when <see cref="MqttDashboard.Data.IDataCache.HasServer"/> is already <c>true</c>.
    /// </para>
    /// </summary>
    public static IServiceCollection AddMqttDashboardSameProcess(this IServiceCollection services)
    {
        // Wire ApplicationState.DataCache directly to ServerDataCache; no per-circuit copy needed.
        services.AddSingleton<IDataCache>(sp => sp.GetRequiredService<ServerDataCache>());

        // In same-process mode, IDataServer for MqttInitializationService should be MqttDataServer
        // directly — not CacheBridgeDataServer (which is the Blazor-Server-circuit adapter).
        services.AddScoped<IDataServer>(sp => sp.GetRequiredService<MqttDataServer>());

        return services;
    }

    /// <summary>
    /// Creates an HttpClient whose base address points to the local server port.
    /// Using Connection.LocalPort bypasses any reverse proxy so server-side self-calls
    /// always reach the app directly, regardless of public hostname or subpath.
    /// Falls back to the cached port in RenderModeOptions for Blazor Server circuits
    /// where IHttpContextAccessor.HttpContext is null (no active HTTP request).
    /// </summary>
    private static HttpClient CreateLoopbackHttpClient(IServiceProvider sp)
    {
        var ctx = sp.GetService<IHttpContextAccessor>()?.HttpContext;
        var port = ctx?.Connection.LocalPort ?? 0;
        if (port == 0)
            port = sp.GetService<MqttDashboard.Services.RenderModeOptions>()?.LoopbackPort ?? 0;
        return new HttpClient
        {
            BaseAddress = port > 0 ? new Uri($"http://localhost:{port}/") : null
        };
    }
}


