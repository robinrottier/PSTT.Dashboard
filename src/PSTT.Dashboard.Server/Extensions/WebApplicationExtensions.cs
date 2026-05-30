using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PSTT.Dashboard.Server.Services;
using PSTT.Remote.AspNetCore.Extensions;
using System.Net;

namespace PSTT.Dashboard.Server.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the HTTP request pipeline for PSTT.Dashboard with the specified render mode
    /// </summary>
    public static WebApplication UseDashboard<TApp>(
        this WebApplication app,
        BlazorRenderMode renderMode) where TApp : IComponent
    {
        // At startup, proactively cache the HTTP (non-TLS) loopback port from Kestrel's address
        // features. We prefer HTTP to avoid TLS overhead and certificate issues for server-to-self
        // SignalR connections from Blazor Server circuits.
        var renderModeOptions = app.Services.GetService<PSTT.Dashboard.Services.RenderModeOptions>();
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var addresses = app.Services.GetService<IServer>()
                    ?.Features?.Get<IServerAddressesFeature>()?.Addresses;
                if (addresses == null) return;

                foreach (var address in addresses)
                {
                    if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) continue;
                    // Don't normalize to localhost - use the actual address
                    if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Port > 0)
                    {
                        renderModeOptions?.CacheLoopbackAddress(uri);
                        return;
                    }
                }
            }
            catch { /* non-critical — middleware fallback will cache on the first request */ }
        });

        // Fallback: cache the local address on the first request in case the startup callback didn't
        // find an HTTP address (e.g. HTTPS-only deployment). CacheLoopbackAddress is once-only
        // (CompareExchange), so this won't overwrite the address already set by the startup callback.
        app.Use(async (ctx, next) =>
        {
            var port = ctx.Connection.LocalPort;
            var host = ctx.Request.Host.Host;
            if (port > 0)
            {
                ctx.RequestServices.GetService<PSTT.Dashboard.Services.RenderModeOptions>()
                    ?.CacheLoopbackAddress(new Uri($"http://{host}:{port}/"));
            }
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

        // Configure forwarded headers support for reverse proxy scenarios (nginx, etc.)
        // This must run early in the pipeline so other middleware sees the correct scheme/host.
        app.UseForwardedHeaders(BuildForwardedHeadersOptions(app));

        // Security headers — applied to every response before any other middleware writes to it.
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            ctx.Response.Headers["Permissions-Policy"] =
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), " +
                "magnetometer=(), microphone=(), payment=(), usb=()";
            await next();
        });

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
        // By default only the aggregate status is returned. Set HealthCheck:DetailedResponse=true
        // in appsettings to include per-check names and descriptions (for uptime monitors etc.).
        // Set HealthCheck:Enabled=false to disable the endpoint entirely.
        var healthEnabled = !string.Equals(
            app.Configuration["HealthCheck:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

        if (healthEnabled)
        {
            app.MapGet("/healthz", async (HttpContext ctx, HealthCheckService healthService) =>
            {
                var ignoreMqtt = ctx.Request.Query.ContainsKey("ignoreMqtt");

                Func<HealthCheckRegistration, bool>? predicate = ignoreMqtt
                    ? reg => !string.Equals(reg.Name, "mqtt", StringComparison.OrdinalIgnoreCase)
                    : null;

                var report = await healthService.CheckHealthAsync(predicate, ctx.RequestAborted);

                var detailed = string.Equals(
                    ctx.RequestServices.GetRequiredService<IConfiguration>()["HealthCheck:DetailedResponse"],
                    "true", StringComparison.OrdinalIgnoreCase);

                object body = detailed
                    ? new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(e => new
                        {
                            name        = e.Key,
                            status      = e.Value.Status.ToString(),
                            description = e.Value.Description,
                        })
                    }
                    : new { status = report.Status.ToString() };

                return report.Status == HealthStatus.Healthy
                    ? Results.Ok(body)
                    : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
            });
        }

        // ── CacheHub presence-cookie protection ───────────────────────────────────
        //
        // Goal: stop internet scanners from hitting /cachehub directly.  Any browser that
        // loads a page receives a signed, time-limited "chsession" cookie (issued by
        // CacheHubTokenService using ASP.NET Core Data Protection).  The cookie is sent
        // automatically on the same-origin /cachehub/negotiate WebSocket request.
        // Cookies survive server restarts because DP keys are persisted to disk.
        //
        // Blazor Server circuits NEVER connect to /cachehub — they use in-process
        // ServerDataCache.  Only WASM browser clients do, so this barrier only needs
        // to work for same-origin browser requests (which it does automatically).
        //
        // This is NOT a full authentication system — a cookie-aware scripted client that
        // first fetches a page can still bypass it.  The goal is noise reduction, not
        // cryptographic access control.

        // 1. Issue a presence cookie on any navigational request (not static assets,
        //    not API endpoints, not the hub itself).  Fires before the response body
        //    is written so headers can still be set.
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "";
            var isExcluded =
                path.StartsWith("/cachehub", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/",      StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/healthz",   StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/_content/",   StringComparison.OrdinalIgnoreCase);

            if (!isExcluded)
            {
                var tokenSvc = ctx.RequestServices.GetRequiredService<CacheHubTokenService>();
                var existing = ctx.Request.Cookies["chsession"];
                if (!tokenSvc.Validate(existing))
                {
                    ctx.Response.Cookies.Append("chsession", tokenSvc.IssueToken(), new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Secure   = ctx.Request.IsHttps,
                        MaxAge   = CacheHubTokenService.TokenLifetime,
                        IsEssential = true,
                    });
                }
            }

            await next();
        });

        // 2. Validate the presence cookie on /cachehub connections.
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/cachehub"))
            {
                var tokenSvc = ctx.RequestServices.GetRequiredService<CacheHubTokenService>();
                if (!tokenSvc.Validate(ctx.Request.Cookies["chsession"]))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await next();
        });

        // Map PSTT CacheHub — SignalR hub
        app.MapCacheHub("/cachehub");

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
        razorComponentsEndpoint.AddAdditionalAssemblies(typeof(PSTT.Dashboard._Imports).Assembly);

        return app;
    }

    private static ForwardedHeadersOptions BuildForwardedHeadersOptions(WebApplication app)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost,
        };

        // Clear ASP.NET Core's default (loopback-only) trusted sources before applying config.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var knownNetworksCidr = app.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];
        var knownProxiesIp = app.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];

        foreach (var cidr in knownNetworksCidr)
        {
            if (System.Net.IPNetwork.TryParse(cidr, out var network))
                options.KnownIPNetworks.Add(network);
            else
                app.Logger.LogWarning("ReverseProxy:KnownNetworks: '{Cidr}' is not valid CIDR notation, skipping", cidr);
        }

        foreach (var ip in knownProxiesIp)
        {
            if (IPAddress.TryParse(ip, out var address))
                options.KnownProxies.Add(address);
            else
                app.Logger.LogWarning("ReverseProxy:KnownProxies: '{Ip}' is not a valid IP address, skipping", ip);
        }

        // When both collections are empty, ASP.NET Core trusts forwarded headers from all sources.
        // This is the safe default for local/development use but risky behind a public reverse proxy.
        if (options.KnownIPNetworks.Count == 0 && options.KnownProxies.Count == 0
            && !app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning(
                "ReverseProxy: No KnownNetworks or KnownProxies configured — X-Forwarded-* headers " +
                "are trusted from any source. Set ReverseProxy:KnownNetworks (CIDR) or " +
                "ReverseProxy:KnownProxies (IP) in appsettings to restrict to your reverse proxy.");
        }

        return options;
    }
}
