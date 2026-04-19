using PSTT.Dashboard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PSTT.Dashboard.Server.Extensions;

public enum BlazorRenderMode
{
    InteractiveServer,
    InteractiveWebAssembly,
    InteractiveAuto
}

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures Serilog as the application logger with environment-aware console output:
    /// JSON in Production, human-readable template in Development/Test.
    /// Call before <see cref="AddMqttDashboard"/> so startup errors are captured.
    /// </summary>
    public static WebApplicationBuilder AddMqttDashboardSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, config) =>
        {
            config.ReadFrom.Configuration(ctx.Configuration)
                  .ReadFrom.Services(services)
                  .Enrich.FromLogContext();

            if (ctx.HostingEnvironment.IsProduction())
                config.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
            else
                config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
        });

        return builder;
    }

    /// <summary>
    /// Resolves the data directory, creates it, migrates legacy user settings, registers
    /// <c>appsettings.user.json</c> from the data dir, and loads Home Assistant add-on options
    /// from <c>/data/options.json</c> when running inside an HA add-on container.
    /// <para>Call this before <see cref="AddMqttDashboard"/> so that user settings and HA options
    /// are available during service registration.</para>
    /// </summary>
    public static WebApplicationBuilder AddMqttDashboardDataDirectory(this WebApplicationBuilder builder)
    {
        var dataDir = ResolveDataDir(builder.Configuration, builder.Environment.ContentRootPath);
        Directory.CreateDirectory(dataDir);

        // One-time migration: move appsettings.user.json from ContentRoot into data dir
        var oldUserSettings = Path.Combine(builder.Environment.ContentRootPath, "appsettings.user.json");
        var newUserSettings = Path.Combine(dataDir, "appsettings.user.json");
        if (File.Exists(oldUserSettings) && !File.Exists(newUserSettings))
        {
            File.Copy(oldUserSettings, newUserSettings);
            Log.Information("Migrated appsettings.user.json to data directory {Dir}", dataDir);
        }

        // Load user-specific settings (e.g. admin password hash) from the data dir.
        // This file persists across container restarts when data dir is a mounted volume.
        var settingsFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dataDir);
        builder.Configuration.AddJsonFile(settingsFileProvider, "appsettings.user.json", optional: true, reloadOnChange: true);

        // Home Assistant add-on: /data/options.json is written by the HA supervisor
        // with add-on configuration. Map known keys to our environment variables.
        var haOptionsPath = "/data/options.json";
        if (File.Exists(haOptionsPath))
        {
            try
            {
                var json = File.ReadAllText(haOptionsPath);
                var haOptions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
                if (haOptions != null)
                {
                    void SetIfPresent(string key, string envVar)
                    {
                        if (haOptions.TryGetValue(key, out var val))
                            Environment.SetEnvironmentVariable(envVar, val.ToString());
                    }
                    SetIfPresent("mqtt_broker",   "MqttSettings__Broker");
                    SetIfPresent("mqtt_port",     "MqttSettings__Port");
                    SetIfPresent("mqtt_username", "MqttSettings__Username");
                    SetIfPresent("mqtt_password", "MqttSettings__Password");
                }
                Log.Information("Home Assistant options loaded from {Path}", haOptionsPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read Home Assistant options from {Path}", haOptionsPath);
            }
        }

        return builder;
    }

    /// <summary>
    /// Adds all services required for PSTT.Dashboard with the specified render mode.
    /// </summary>
    public static WebApplicationBuilder AddMqttDashboard(
        this WebApplicationBuilder builder, 
        BlazorRenderMode renderMode)
    {
        // Persist Data Protection keys so antiforgery tokens survive container restarts.
        // Configure DataProtection:KeysDirectory in appsettings/env (e.g. /app/data/keys in Docker).
        // If not set, default to the application's data directory (DiagramStorage:DataDirectory or DIAGRAM_DATA_DIR)
        var keysDir = builder.Configuration["DataProtection:KeysDirectory"];
        if (string.IsNullOrWhiteSpace(keysDir))
        {
            // Resolve data directory the same way hosts do so keys live in the persisted data volume
            var envDir = Environment.GetEnvironmentVariable("DIAGRAM_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(envDir))
                keysDir = Path.Combine(envDir, "keys");
            else
            {
                var cfgDir = builder.Configuration["DiagramStorage:DataDirectory"];
                if (!string.IsNullOrWhiteSpace(cfgDir))
                    keysDir = Path.GetFullPath(Path.Combine(cfgDir, "keys"), builder.Environment.ContentRootPath);
                else
                    keysDir = Path.Combine(builder.Environment.ContentRootPath, "Data", "keys");
            }
        }

        if (!string.IsNullOrWhiteSpace(keysDir))
        {
            var keysDirInfo = new DirectoryInfo(keysDir);
            keysDirInfo.Create(); // ensure it exists
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(keysDirInfo)
                .SetApplicationName("PSTT.Dashboard");
        }

        // Cookie authentication — always registered so that setting the admin password for the
        // first time (via the Setup page) takes effect without requiring a restart.
        // When no AdminPasswordHash is configured, all users are treated as admin (no login needed),
        // but the auth middleware is present and ready the moment a hash is saved.
        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "PSTT.Dashboard.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
                options.Events.OnRedirectToLogin = ctx =>
                {
                    // For API requests, return 401 instead of redirecting to login page
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();

        // Add Razor Components with appropriate render mode
        var razorComponentsBuilder = builder.Services.AddRazorComponents();

        switch (renderMode)
        {
            case BlazorRenderMode.InteractiveServer:
                razorComponentsBuilder.AddInteractiveServerComponents();
                break;
            case BlazorRenderMode.InteractiveWebAssembly:
                razorComponentsBuilder.AddInteractiveWebAssemblyComponents();
                break;
            case BlazorRenderMode.InteractiveAuto:
                razorComponentsBuilder
                    .AddInteractiveServerComponents()
                    .AddInteractiveWebAssemblyComponents();
                break;
        }

        // Register render mode options so client services can distinguish SSR from Blazor Server circuits
        // and can find the loopback port when IHttpContextAccessor is unavailable.
        builder.Services.AddSingleton(new PSTT.Dashboard.Services.RenderModeOptions
        {
            IsWasmCapable = renderMode is BlazorRenderMode.InteractiveAuto or BlazorRenderMode.InteractiveWebAssembly
        });

        // In Development mode, WebApplication.CreateBuilder() calls UseStaticWebAssets()
        // automatically. For other non-production environments (e.g. "Test"), call it
        // explicitly so that RCL static assets (MudBlazor CSS/JS, Blazor framework scripts,
        // etc.) are served from the build manifest rather than requiring a publish.
        var envName = builder.Environment.EnvironmentName;
        if (!string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase))
            builder.WebHost.UseStaticWebAssets();

        builder.Services.AddMqttDashboardServices();
        builder.Services.AddMqttDashboardServerServices(builder.Configuration);

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddCheck<PSTT.Dashboard.Server.Health.MqttConnectionHealthCheck>("mqtt");

        // Add Controllers for API endpoints
        builder.Services.AddControllers(options =>
        {
            // Disable antiforgery validation for API controllers
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        })
        .AddApplicationPart(typeof(PSTT.Dashboard.Server.Controllers.DashboardController).Assembly)
        .AddControllersAsServices();

        return builder;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>Resolves the data directory from env var, config, or default.</summary>
    private static string ResolveDataDir(IConfiguration config, string contentRoot)
    {
        var envDir = Environment.GetEnvironmentVariable("DIAGRAM_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envDir)) return envDir;
        var cfgDir = config["DiagramStorage:DataDirectory"];
        if (!string.IsNullOrWhiteSpace(cfgDir)) return Path.GetFullPath(cfgDir, contentRoot);
        return Path.Combine(contentRoot, "Data");
    }
}
