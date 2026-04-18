using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MqttDashboard.Server.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SetupController> _logger;
    private readonly DashboardStorageService _storage;

    public SetupController(IConfiguration configuration, ILogger<SetupController> logger, DashboardStorageService storage)
    {
        _configuration = configuration;
        _logger = logger;
        _storage = storage;
    }

    [HttpGet("needed")]
    public IActionResult IsSetupNeeded()
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        return Ok(new { needed = string.IsNullOrWhiteSpace(hash) });
    }

    [HttpPost("password")]
    public IActionResult SetPassword([FromBody] SetPasswordRequest request)
    {
        var existingHash = _configuration["Auth:AdminPasswordHash"];
        if (!string.IsNullOrWhiteSpace(existingHash))
            return Conflict(new { error = "Admin password is already configured." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 10);

        SavePasswordHash(hash);

        _logger.LogInformation("Admin password configured and saved to appsettings.user.json");

        if (_configuration is IConfigurationRoot configRoot)
            configRoot.Reload();

        return Ok(new { success = true });
    }

    [HttpPut("password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var existingHash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrWhiteSpace(existingHash))
            return BadRequest(new { error = "No admin password is configured. Use the initial setup instead." });

        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "You must be logged in as admin to change the password." });

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest(new { error = "Current password is required." });

        bool currentValid;
        try { currentValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, existingHash); }
        catch { currentValid = false; }

        if (!currentValid)
            return Unauthorized(new { error = "Current password is incorrect." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "New password must be at least 8 characters." });

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 10);

        SavePasswordHash(newHash);

        _logger.LogInformation("Admin password changed and saved to appsettings.user.json");

        if (_configuration is IConfigurationRoot configRoot)
            configRoot.Reload();

        return Ok(new { success = true });
    }

    private void SavePasswordHash(string hash)
    {
        var userSettingsPath = Path.Combine(_storage.StoragePath, "appsettings.user.json");

        JsonObject root;
        if (System.IO.File.Exists(userSettingsPath))
        {
            try
            {
                var existingJson = System.IO.File.ReadAllText(userSettingsPath);
                root = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
            }
            catch { root = new JsonObject(); }
        }
        else
        {
            root = new JsonObject();
        }

        root["Auth"] = new JsonObject
        {
            ["AdminPasswordHash"] = hash
        };

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(userSettingsPath, json);
    }
}

public record SetPasswordRequest(string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
