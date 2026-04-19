using PSTT.Dashboard.Server.Services;
using PSTT.Dashboard.Server.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Services;
using PSTT.Data;
using PSTT.Mqtt;
using PSTT.Remote.AspNetCore.Extensions;
using System.Text;

namespace PSTT.Dashboard.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMqttDashboardServerServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ── PSTT transport stack ──────────────────────────────────────────────

        // Build MqttCache<string> from configuration (not yet connected — MqttHostedService does that)
        var broker   = configuration["MqttSettings:Broker"] ?? "localhost";
        var port     = int.TryParse(configuration["MqttSettings:Port"], out var p) ? p : 1883;
        var username = configuration["MqttSettings:Username"];
        var password = configuration["MqttSettings:Password"];

        var mqttBuilder = new MqttCacheBuilder<string>()
            .WithBroker(broker, port)
            .WithUtf8Encoding();

        if (!string.IsNullOrEmpty(username))
            mqttBuilder = mqttBuilder.WithCredentials(username, password);

        var mqttCache = mqttBuilder.Build();
        services.AddSingleton(mqttCache);

        // Singleton server cache — accumulates all MQTT values, wildcard-aware
        var serverCache = new ServerDataCache(mqttCache);
        services.AddSingleton(serverCache);

        // Register as ICache<string,string> for same-process mode (see AddMqttDashboardSameProcess)
        services.AddSingleton<ICache<string,string>>(serverCache);

        // Reconnect loop
        services.AddHostedService<MqttHostedService>();

        // SignalR-based remote endpoint for WASM / remote clients
        services.AddCacheSignalRServer<string>(
            serverCache,
            serializer:   v => Encoding.UTF8.GetBytes(v),
            deserializer: b => Encoding.UTF8.GetString(b),
            forwardPublish: true);

        // Scoped per-circuit cache: downstream of serverCache, wildcards forwarded
        services.AddScoped<ICache<string,string>>(sp => new CacheBuilder<string,string>()
            .WithWildcards()
            .WithUpstream(sp.GetRequiredService<ServerDataCache>(),
                supportsWildcards: true, forwardPublish: true)
            .Build());

        // ── Dashboard services ────────────────────────────────────────────────

        services.AddSingleton<DashboardStorageService>();
        services.AddSingleton<LoginTokenStore>();
        services.AddHttpContextAccessor();
        services.AddScoped<HttpClient>(sp => CreateLoopbackHttpClient(sp));
        services.AddScoped<IDashboardService, ServerDashboardService>();
        services.AddScoped<IAuthService, ServerAuthService>();
        services.AddScoped<RequireAdminFilter>();

        services.AddHttpClient("UpdateCheck");
        services.AddSingleton<UpdateCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckService>());

        services.AddHostedService<DashboardMetricsPublisher>();

        return services;
    }

    /// <summary>
    /// Additional bindings for a same-process host (e.g. MAUI Blazor) where server and client
    /// run in a single DI container with no SignalR transport.
    /// Call after <see cref="AddMqttDashboardServerServices"/> and
    /// <see cref="MqttDashboard.Services.ServiceCollectionExtensions.AddMqttDashboardServices"/>.
    /// </summary>
    public static IServiceCollection AddMqttDashboardSameProcess(this IServiceCollection services)
    {
        // Nothing extra required: ICache<string,string> singleton is already registered
        // to ServerDataCache in AddMqttDashboardServerServices, so ApplicationState picks it up directly.
        return services;
    }

    private static HttpClient CreateLoopbackHttpClient(IServiceProvider sp)
    {
        var ctx = sp.GetService<IHttpContextAccessor>()?.HttpContext;
        var port = ctx?.Connection.LocalPort ?? 0;
        if (port == 0)
            port = sp.GetService<PSTT.Dashboard.Services.RenderModeOptions>()?.LoopbackPort ?? 0;
        return new HttpClient
        {
            BaseAddress = port > 0 ? new Uri($"http://localhost:{port}/") : null
        };
    }
}