using MqttDashboard.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace MqttDashboard.Server.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the HTTP request pipeline for MqttDashboard with the specified render mode
    /// </summary>
    public static WebApplication UseMqttDashboard<TApp>(
        this WebApplication app,
        BlazorRenderMode renderMode) where TApp : IComponent
    {
        // At startup, proactively cache the HTTP (non-TLS) loopback port from Kestrel's address
        // features. We prefer HTTP to avoid TLS overhead and certificate issues for server-to-self
        // SignalR connections from Blazor Server circuits.
        var renderModeOptions = app.Services.GetService<MqttDashboard.Services.RenderModeOptions>();
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            // Force MqttStatusBroadcaster to be instantiated so its constructor wires
            // the MqttConnectionMonitor.OnStateChanged → SignalR broadcast event.
            app.Services.GetRequiredService<MqttStatusBroadcaster>();

            try
            {
                var addresses = app.Services.GetService<IServer>()
                    ?.Features?.Get<IServerAddressesFeature>()?.Addresses;
                if (addresses == null) return;

                foreach (var address in addresses)
                {
                    if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) continue;
                    var normalized = address.Replace("+", "localhost").Replace("*", "localhost")
                                            .Replace("[::]", "localhost").Replace("0.0.0.0", "localhost");
                    if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Port > 0)
                    {
                        renderModeOptions?.CacheLoopbackPort(uri.Port);
                        return;
                    }
                }
            }
            catch { /* non-critical — middleware fallback will cache on the first request */ }
        });

        // Fallback: cache the local port on the first request in case the startup callback didn't
        // find an HTTP address (e.g. HTTPS-only deployment). CacheLoopbackPort is once-only
        // (CompareExchange), so this won't overwrite the port already set by the startup callback.
        app.Use(async (ctx, next) =>
        {
            ctx.RequestServices.GetService<MqttDashboard.Services.RenderModeOptions>()
                ?.CacheLoopbackPort(ctx.Connection.LocalPort);
            await next();
        });

        // Apply X-Forwarded-Prefix as the request path base, but only when the header value
        // exactly matches the configured AllowedPathBase (e.g. "/rr-dev").
        // This allows the app to be reached directly (no path base) OR via a reverse proxy
        // sub-path without accepting arbitrary values from untrusted clients.
        var allowedPathBase = app.Configuration["AllowedPathBase"]?.Trim('/');
        if (!string.IsNullOrEmpty(allowedPathBase))
        {
            var canonicalPathBase = new PathString("/" + allowedPathBase);
            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var prefix))
                {
                    var prefixValue = "/" + prefix.ToString().Trim('/');
                    if (string.Equals(prefixValue, canonicalPathBase, StringComparison.OrdinalIgnoreCase))
                        context.Request.PathBase = canonicalPathBase;
                }
                return next(context);
            });
        }

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            if (renderMode == BlazorRenderMode.InteractiveWebAssembly || 
                renderMode == BlazorRenderMode.InteractiveAuto)
            {
                app.UseWebAssemblyDebugging();
            }
        }
        else if (app.Environment.IsProduction())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        // Redirect to HTTPS in production/staging; skip in development and test
        // to avoid redirect loops when no HTTPS port is configured.
        if (app.Environment.IsProduction())
            app.UseHttpsRedirection();

        // Add antiforgery middleware (required by Blazor components)
        // API controllers are exempt via the IgnoreAntiforgeryTokenAttribute global filter
        app.UseAntiforgery();

        // Authentication/authorization middleware is always active (services are always registered).
        // When no AdminPasswordHash is configured, auth is effectively open — everyone is admin.
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();

        // Map Controllers
        app.MapControllers();

        // Health check endpoint.
        // GET /healthz              — full check; 200 healthy, 503 degraded/unhealthy.
        // GET /healthz?ignoreMqtt  — skip the MQTT check; useful for startup probes and
        //                            test harnesses where no broker is intentionally present.
        //                            Always returns 200 as long as the web server is up.
        app.MapGet("/healthz", async (HttpContext ctx, HealthCheckService healthService) =>
        {
            var ignoreMqtt = ctx.Request.Query.ContainsKey("ignoreMqtt");

            Func<HealthCheckRegistration, bool>? predicate = ignoreMqtt
                ? reg => !string.Equals(reg.Name, "mqtt", StringComparison.OrdinalIgnoreCase)
                : null;

            var report = await healthService.CheckHealthAsync(predicate, ctx.RequestAborted);

            var body = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name        = e.Key,
                    status      = e.Value.Status.ToString(),
                    description = e.Value.Description,
                })
            };

            return report.Status == HealthStatus.Healthy
                ? Results.Ok(body)
                : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        // Map SignalR Hub — disable antiforgery since SignalR manages its own security
        // (WebSocket same-origin policy protects against CSRF; antiforgery tokens don't apply here)
        app.MapHub<DataHub>("/datahub").DisableAntiforgery();

        // Map Razor Components with appropriate render mode
        var razorComponentsEndpoint = app.MapRazorComponents<TApp>();

        switch (renderMode)
        {
            case BlazorRenderMode.InteractiveServer:
                razorComponentsEndpoint.AddInteractiveServerRenderMode();
                break;
            case BlazorRenderMode.InteractiveWebAssembly:
                razorComponentsEndpoint.AddInteractiveWebAssemblyRenderMode();
                break;
            case BlazorRenderMode.InteractiveAuto:
                razorComponentsEndpoint
                    .AddInteractiveServerRenderMode()
                    .AddInteractiveWebAssemblyRenderMode();
                break;
        }

        // Add additional assemblies
        razorComponentsEndpoint.AddAdditionalAssemblies(typeof(MqttDashboard._Imports).Assembly);

        return app;
    }
}
