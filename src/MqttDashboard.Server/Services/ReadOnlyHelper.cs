using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MqttDashboard.Server.Services;

/// <summary>
/// Centralised read-only check used by controllers and filters.
///
/// Two ways to mark a request as read-only:
///   1. Global flag:  ReadOnly=true in config / env — the whole process is read-only.
///   2. Per-port:     ReadOnlyPorts=8080 (comma-separated) — only requests arriving on
///                    those ports are read-only. Enables a single process to serve a
///                    public read-only audience on one port and admins on another, while
///                    sharing the MQTT connection, data cache, and SignalR hub in-process.
///
/// If ReadOnly=true is set, all requests are read-only regardless of ReadOnlyPorts.
/// If neither is set, all requests are read-write (default behaviour).
/// </summary>
public static class ReadOnlyHelper
{
    /// <summary>
    /// Returns true if the current request / context should be treated as read-only.
    /// Pass <paramref name="httpContext"/> as null to get the global flag only
    /// (e.g. when no HTTP context is available at startup).
    /// </summary>
    public static bool IsReadOnly(IConfiguration config, HttpContext? httpContext = null)
    {
        // Global flag takes priority
        if (config.GetValue<bool>("ReadOnly"))
            return true;

        // Per-port check
        var portsRaw = config["ReadOnlyPorts"];
        if (string.IsNullOrWhiteSpace(portsRaw) || httpContext == null)
            return false;

        var localPort = httpContext.Connection.LocalPort;
        foreach (var part in portsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var port) && port == localPort)
                return true;
        }

        return false;
    }
}
