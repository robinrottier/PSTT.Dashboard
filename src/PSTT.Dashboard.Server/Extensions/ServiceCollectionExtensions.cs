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
        services.AddSingleton<IRemoteRepoService, RemoteRepoService>();
        services.AddSingleton<IAppSettingsService, ServerAppSettingsService>();
        services.AddSingleton<ISetupService, ServerSetupService>();
        services.AddSingleton<IUpdateStatusService, ServerUpdateStatusService>();
        services.AddSingleton<IStartupSettingsService, ServerStartupSettingsService>();
        services.AddSingleton<IRemoteAccessService, ServerRemoteAccessService>();
        services.AddHttpContextAccessor();
        services.AddScoped<HttpClient>(sp => CreateLoopbackHttpClient(sp));
        services.AddScoped<IDashboardService, ServerDashboardService>();
        services.AddScoped<IAuthService, ServerAuthService>();
        services.AddScoped<RequireAdminFilter>();
        services.AddScoped<ApiTokenAuthFilter>();

        services.AddHttpClient("UpdateCheck");
        // Named client for self-referencing (loopback) proxy calls: SSL cert validation is
        // bypassed so self-signed certs on localhost/IP don't block circular calls.
        // In tests the entire IHttpClientFactory is replaced, so this registration is overridden.
        services.AddHttpClient("loopback").ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
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
        Uri? address = null;

        var ctx = sp.GetService<IHttpContextAccessor>()?.HttpContext;

        // Priority 1: Check if we're behind a reverse proxy (forwarded headers present)
        // In this case, use the public-facing URL because the internal address may not be accessible
        if (ctx != null)
        {
            var forwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var forwardedHost = ctx.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var forwardedPrefix = ctx.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();

            if (!string.IsNullOrEmpty(forwardedHost))
            {
                // Reverse proxy scenario: use the forwarded scheme and host (what the client sees)
                var scheme = !string.IsNullOrEmpty(forwardedProto) ? forwardedProto : ctx.Request.Scheme;
                var pathBase = !string.IsNullOrEmpty(forwardedPrefix) ? forwardedPrefix.TrimEnd('/') : ctx.Request.PathBase.Value;

                var uriBuilder = new UriBuilder(scheme, forwardedHost)
                {
                    Path = pathBase ?? ""
                };
                address = uriBuilder.Uri;
            }
        }

        // Priority 2: Use the startup-cached HTTP address (development/direct access)
        // This avoids SSL certificate validation issues with self-signed certs on IP addresses
        if (address == null)
        {
            var renderModeOptions = sp.GetService<PSTT.Dashboard.Services.RenderModeOptions>();
            address = renderModeOptions?.LoopbackAddress;
        }

        // Priority 3: Fallback to current request address (shouldn't normally happen)
        if (address == null && ctx != null)
        {
            var scheme = ctx.Request.Scheme;
            var host = ctx.Request.Host.Host;
            var port = ctx.Request.Host.Port;
            var pathBase = ctx.Request.PathBase.Value;

            // Prefer HTTP over HTTPS for loopback to avoid certificate issues
            // If the request is HTTPS, try to find the HTTP port by subtracting 1 (common convention)
            if (scheme == "https" && port.HasValue)
            {
                // Common pattern: HTTP port is HTTPS port + 1 (e.g., 7190 HTTPS, 7191 HTTP)
                var httpPort = port.Value + 1;
                address = new Uri($"http://{host}:{httpPort}{pathBase}");
            }
            else if (port.HasValue)
            {
                address = new Uri($"{scheme}://{host}:{port}{pathBase}");
            }
            else
            {
                address = new Uri($"{scheme}://{host}{pathBase}");
            }
        }

        return new HttpClient
        {
            BaseAddress = address
        };
    }
}