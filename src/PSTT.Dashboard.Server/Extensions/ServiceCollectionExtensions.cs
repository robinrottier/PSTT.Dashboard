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
using System;

namespace PSTT.Dashboard.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardServerServices(
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
            .WithUtf8Encoding()
            .WithUnsubscribeGracePeriod(TimeSpan.FromSeconds(30));

        if (!string.IsNullOrEmpty(username))
            mqttBuilder = mqttBuilder.WithCredentials(username, password);

        var mqttCache = mqttBuilder.Build();
        services.AddSingleton(mqttCache);

        // Singleton server cache — accumulates all MQTT values, wildcard-aware
        var serverCache = new ServerDataCache(mqttCache);
        services.AddSingleton(serverCache);

        // Register as ICache<string,string> for same-process mode (see AddDashboardSameProcess)
        services.AddSingleton<ICache<string,string>>(serverCache);

        // Reconnect loop
        services.AddHostedService<MqttHostedService>();

        // SignalR-based remote endpoint for WASM / remote clients
        services.AddCacheSignalRServer<string>(
            serverCache,
            serializer:   v => Encoding.UTF8.GetBytes(v),
            deserializer: b => Encoding.UTF8.GetString(b),
            forwardPublish: true);

        // Optional TCP cache server: enables external tools to subscribe/publish via PSTT TCP protocol.
        // Set CacheSettings:TcpPort > 0 to enable (0 = disabled).
        var tcpPort = int.TryParse(configuration["CacheSettings:TcpPort"], out var tp) ? tp : 0;
        if (tcpPort > 0)
        {
            services.AddCacheTcpServer<string>(
                serverCache,
                tcpPort,
                serializer:    v => Encoding.UTF8.GetBytes(v),
                deserializer:  b => Encoding.UTF8.GetString(b),
                forwardPublish: true);
        }

        // Scoped per-circuit cache: downstream of serverCache, wildcards forwarded.
        // 30 s grace period avoids MQTT churn when Blazor reconnects or the user navigates.
        services.AddScoped<ICache<string,string>>(sp => new CacheBuilder<string,string>()
            .WithWildcards()
            .WithUpstream(sp.GetRequiredService<ServerDataCache>(),
                supportsWildcards: true, forwardPublish: true)
            .WithUnsubscribeGracePeriod(TimeSpan.FromSeconds(30))
            .Build());

        // ── Dashboard services ────────────────────────────────────────────────

        services.AddSingleton<DashboardStorageService>();
        services.AddSingleton<UserSettingsService>();
        services.AddSingleton<LoginTokenStore>();
        services.AddHttpContextAccessor();
        services.AddScoped<HttpClient>(sp => CreateLoopbackHttpClient(sp));
        services.AddScoped<IDashboardService, ServerDashboardService>();
        services.AddScoped<IAuthService, ServerAuthService>();
        services.AddScoped<RequireAdminFilter>();
        services.AddScoped<ApiTokenAuthFilter>();

        services.AddHttpClient("UpdateCheck");
        services.AddSingleton<UpdateCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckService>());

        services.AddHostedService<DashboardMetricsPublisher>();

        return services;
    }

    /// <summary>
    /// Additional bindings for a same-process host (e.g. MAUI Blazor) where server and client
    /// run in a single DI container with no SignalR transport.
    /// Call after <see cref="AddDashboardServerServices"/> and
    /// <see cref="PSTT.Dashboard.Services.ServiceCollectionExtensions.AddDashboardServices"/>.
    /// </summary>
    public static IServiceCollection AddDashboardSameProcess(this IServiceCollection services)
    {
        // Nothing extra required: ICache<string,string> singleton is already registered
        // to ServerDataCache in AddDashboardServerServices, so ApplicationState picks it up directly.
        return services;
    }

    private static HttpClient CreateLoopbackHttpClient(IServiceProvider sp)
    {
        // Prefer the startup-cached HTTP address (set from Kestrel's http:// listener).
        // Blazor Server circuits run on the SignalR/WebSocket connection which may be HTTPS —
        // using that connection's address ensures we connect to the actual listening endpoint.
        var renderModeOptions = sp.GetService<PSTT.Dashboard.Services.RenderModeOptions>();
        var address = renderModeOptions?.LoopbackAddress;

        if (address == null)
        {
            // Fallback: construct address from current HTTP context
            var ctx = sp.GetService<IHttpContextAccessor>()?.HttpContext;
            if (ctx != null)
            {
                var port = ctx.Connection.LocalPort;
                var host = ctx.Request.Host.Host;
                if (port > 0)
                    address = new Uri($"http://{host}:{port}/");
            }
        }

        return new HttpClient
        {
            BaseAddress = address
        };
    }
}