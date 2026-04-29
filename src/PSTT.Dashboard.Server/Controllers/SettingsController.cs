using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Server.Services;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace PSTT.Dashboard.Server.Controllers;

[ApiController]
[Route("api/settings")]
[IgnoreAntiforgeryToken]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly UserSettingsService _userSettings;

    public SettingsController(IConfiguration configuration, UserSettingsService userSettings)
    {
        _configuration = configuration;
        _userSettings = userSettings;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    /// <summary>Returns the system-wide startup dashboard configuration.</summary>
    [HttpGet("startup")]
    public IActionResult GetStartup()
    {
        var mode = _configuration["Startup:Mode"] ?? "LastUsed";
        var dashboard = _configuration["Startup:Dashboard"] ?? string.Empty;
        return Ok(new { mode, dashboard });
    }

    /// <summary>Sets the system-wide startup configuration. Admin only.</summary>
    [HttpPost("startup")]
    public async Task<IActionResult> SetStartup([FromBody] SetStartupRequest request)
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, HttpContext))
            return StatusCode(403, new { error = "Dashboard is in read-only mode." });

        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });

        var allowedModes = new[] { "LastUsed", "SpecificFile", "None" };
        if (!allowedModes.Contains(request.Mode, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Mode must be one of: LastUsed, SpecificFile, None" });

        await _userSettings.UpdateAsync(root =>
        {
            root["Startup"] = new JsonObject
            {
                ["Mode"] = request.Mode,
                ["Dashboard"] = request.Dashboard ?? string.Empty
            };
        });

        return Ok(new { success = true });
    }

    // ── App preferences ───────────────────────────────────────────────────────

    /// <summary>Returns system-wide app preferences.</summary>
    [HttpGet("app")]
    public IActionResult GetApp()
    {
        var autoSaveOnExit = _configuration.GetValue<bool>("App:AutoSaveOnExit", false);
        var alternateInstances = _configuration
            .GetSection("App:AlternateInstances")
            .Get<List<AlternateInstanceConfig>>()
            ?? [];
        return Ok(new { autoSaveOnExit, alternateInstances });
    }

    /// <summary>Sets system-wide app preferences. Admin only.</summary>
    [HttpPost("app")]
    public async Task<IActionResult> SetApp([FromBody] SetAppRequest request)
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, HttpContext))
            return StatusCode(403, new { error = "Dashboard is in read-only mode." });

        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });

        await _userSettings.UpdateAsync(root =>
        {
            if (root["App"] is not JsonObject appObj)
            {
                appObj = new JsonObject();
                root["App"] = appObj;
            }
            appObj["AutoSaveOnExit"] = request.AutoSaveOnExit;
        });

        return Ok(new { success = true });
    }

    // ── Remote access token ───────────────────────────────────────────────────

    /// <summary>Returns the remote API access token, generating one if absent. Admin only.</summary>
    [HttpGet("remote-access/token")]
    public async Task<IActionResult> GetRemoteAccessToken()
    {
        if (!IsAdminOrNoAuth()) return Unauthorized(new { error = "Admin authentication required." });

        var token = _configuration["RemoteAccess:ApiToken"];
        if (string.IsNullOrEmpty(token))
            token = await GenerateAndStoreTokenAsync();

        var hasAdminAuth = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        return Ok(new
        {
            token,
            warning = hasAdminAuth ? null : "Remote access token is only effective when admin authentication is configured."
        });
    }

    /// <summary>Generates a new remote API access token, invalidating the previous one. Admin only.</summary>
    [HttpPost("remote-access/token/regenerate")]
    public async Task<IActionResult> RegenerateRemoteAccessToken()
    {
        if (!IsAdminOrNoAuth()) return Unauthorized(new { error = "Admin authentication required." });

        var token = await GenerateAndStoreTokenAsync();
        return Ok(new { token });
    }

    // ── Remote repositories ───────────────────────────────────────────────────

    /// <summary>Returns configured remote repositories (name + URL only, no tokens). Admin only.</summary>
    [HttpGet("remote-repos")]
    public IActionResult GetRemoteRepos()
    {
        if (!IsAdminOrNoAuth()) return Unauthorized(new { error = "Admin authentication required." });

        var repos = GetRemoteReposFromConfig();
        return Ok(repos.Select(r => new { r.Name, r.Url }).ToList());
    }

    /// <summary>Adds a remote repository. Admin only.</summary>
    [HttpPost("remote-repos")]
    public async Task<IActionResult> AddRemoteRepo([FromBody] RemoteRepoRequest request)
    {
        if (!IsAdminOrNoAuth()) return Unauthorized(new { error = "Admin authentication required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest(new { error = "A valid absolute URL (http or https) is required." });

        if (string.IsNullOrWhiteSpace(request.ApiToken))
            return BadRequest(new { error = "API token is required." });

        var safeName = request.Name.Trim();
        var existing = GetRemoteReposFromConfig();
        if (existing.Any(r => string.Equals(r.Name, safeName, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A remote repository named '{safeName}' already exists." });

        await _userSettings.UpdateAsync(root =>
        {
            var arr = root["RemoteRepositories"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject
            {
                ["Name"] = safeName,
                ["Url"] = request.Url.TrimEnd('/'),
                ["ApiToken"] = request.ApiToken.Trim()
            });
            root["RemoteRepositories"] = arr;
        });

        return Ok(new { success = true });
    }

    /// <summary>Removes a remote repository by name. Admin only.</summary>
    [HttpDelete("remote-repos/{name}")]
    public async Task<IActionResult> DeleteRemoteRepo(string name)
    {
        if (!IsAdminOrNoAuth()) return Unauthorized(new { error = "Admin authentication required." });

        var existing = GetRemoteReposFromConfig();
        if (!existing.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return NotFound(new { error = $"Remote repository '{name}' not found." });

        await _userSettings.UpdateAsync(root =>
        {
            var arr = root["RemoteRepositories"] as JsonArray;
            if (arr == null) return;
            var toRemove = arr
                .OfType<JsonObject>()
                .FirstOrDefault(o => string.Equals(o["Name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null) arr.Remove(toRemove);
        });

        return Ok(new { success = true });
    }

    /// <summary>Updates an existing remote repository. Admin only.</summary>
    [HttpPut("remote-repos/{name}")]
    public async Task<IActionResult> UpdateRemoteRepo(string name, [FromBody] RemoteRepoRequest request)
    {
        if (!IsAdminOrNoAuth()) return Unauthorized(new { error = "Admin authentication required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest(new { error = "A valid absolute URL (http or https) is required." });

        if (string.IsNullOrWhiteSpace(request.ApiToken))
            return BadRequest(new { error = "API token is required." });

        var existing = GetRemoteReposFromConfig();
        if (!existing.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return NotFound(new { error = $"Remote repository '{name}' not found." });

        // If renaming, ensure new name doesn't conflict with another entry
        var safeName = request.Name.Trim();
        if (!string.Equals(name, safeName, StringComparison.OrdinalIgnoreCase) &&
            existing.Any(r => string.Equals(r.Name, safeName, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A remote repository named '{safeName}' already exists." });

        await _userSettings.UpdateAsync(root =>
        {
            var arr = root["RemoteRepositories"] as JsonArray;
            if (arr == null) return;
            var toUpdate = arr
                .OfType<JsonObject>()
                .FirstOrDefault(o => string.Equals(o["Name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase));
            if (toUpdate == null) return;
            toUpdate["Name"] = safeName;
            toUpdate["Url"] = request.Url.TrimEnd('/');
            toUpdate["ApiToken"] = request.ApiToken.Trim();
        });

        return Ok(new { success = true });
    }


    private bool IsAdminOrNoAuth()
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, HttpContext)) return false;
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        return !authEnabled || User.Identity?.IsAuthenticated == true;
    }

    private async Task<string> GenerateAndStoreTokenAsync()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await _userSettings.UpdateAsync(root =>
        {
            if (root["RemoteAccess"] is not JsonObject ra)
            {
                ra = new JsonObject();
                root["RemoteAccess"] = ra;
            }
            ra["ApiToken"] = token;
        });

        return token;
    }

    private List<RemoteRepoEntry> GetRemoteReposFromConfig()
    {
        return _configuration.GetSection("RemoteRepositories")
            .Get<List<RemoteRepoEntry>>() ?? [];
    }
}

public record SetStartupRequest(string Mode, string? Dashboard);
public record SetAppRequest(bool AutoSaveOnExit);
public record RemoteRepoRequest(string Name, string Url, string ApiToken);

public class RemoteRepoEntry
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
}

public class AlternateInstanceConfig
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
