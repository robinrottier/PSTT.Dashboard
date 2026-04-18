using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MqttDashboard.Server.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/settings")]
[IgnoreAntiforgeryToken]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly DashboardStorageService _storage;

    public SettingsController(IConfiguration configuration, DashboardStorageService storage)
    {
        _configuration = configuration;
        _storage = storage;
    }

    /// <summary>Returns the system-wide startup dashboard configuration.</summary>
    [HttpGet("startup")]
    public IActionResult GetStartup()
    {
        var mode = _configuration["Startup:Mode"] ?? "LastUsed";
        var dashboard = _configuration["Startup:Dashboard"] ?? string.Empty;
        return Ok(new { mode, dashboard });
    }

    /// <summary>Returns system-wide app preferences.</summary>
    [HttpGet("app")]
    public IActionResult GetApp()
    {
        var autoSaveOnExit = _configuration.GetValue<bool>("App:AutoSaveOnExit", false);
        return Ok(new { autoSaveOnExit });
    }

    /// <summary>Sets system-wide app preferences. Admin only.</summary>
    [HttpPost("app")]
    public IActionResult SetApp([FromBody] SetAppRequest request)
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, HttpContext))
            return StatusCode(403, new { error = "Dashboard is in read-only mode." });

        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });

        SaveApp(request.AutoSaveOnExit);

        if (_configuration is IConfigurationRoot configRoot)
            configRoot.Reload();

        return Ok(new { success = true });
    }

    /// <summary>Sets the system-wide startup configuration. Admin only.</summary>
    [HttpPost("startup")]
    public IActionResult SetStartup([FromBody] SetStartupRequest request)
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, HttpContext))
            return StatusCode(403, new { error = "Dashboard is in read-only mode." });

        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });

        var allowedModes = new[] { "LastUsed", "SpecificFile", "None" };
        if (!allowedModes.Contains(request.Mode, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Mode must be one of: LastUsed, SpecificFile, None" });

        Save(request.Mode, request.Dashboard ?? string.Empty);

        if (_configuration is IConfigurationRoot configRoot)
            configRoot.Reload();

        return Ok(new { success = true });
    }

    private void Save(string mode, string dashboard)
    {
        var path = Path.Combine(_storage.StoragePath, "appsettings.user.json");
        var root = ReadUserJson(path);

        root["Startup"] = new JsonObject
        {
            ["Mode"] = mode,
            ["Dashboard"] = dashboard
        };

        WriteUserJson(path, root);
    }

    private void SaveApp(bool autoSaveOnExit)
    {
        var path = Path.Combine(_storage.StoragePath, "appsettings.user.json");
        var root = ReadUserJson(path);

        if (root["App"] is not JsonObject appObj)
        {
            appObj = new JsonObject();
            root["App"] = appObj;
        }
        appObj["AutoSaveOnExit"] = autoSaveOnExit;

        WriteUserJson(path, root);
    }

    private static JsonObject ReadUserJson(string path)
    {
        if (System.IO.File.Exists(path))
        {
            try { return JsonNode.Parse(System.IO.File.ReadAllText(path))?.AsObject() ?? new JsonObject(); }
            catch { }
        }
        return new JsonObject();
    }

    private static void WriteUserJson(string path, JsonObject root)
    {
        System.IO.File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}

public record SetStartupRequest(string Mode, string? Dashboard);
public record SetAppRequest(bool AutoSaveOnExit);
