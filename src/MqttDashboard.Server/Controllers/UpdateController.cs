using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using MqttDashboard.Server.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using MqttDashboard.Server.Models;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/update")]
public class UpdateController : ControllerBase
{
    private readonly UpdateCheckService _updateService;
    private readonly DashboardStorageService _diagramStorage;
    private readonly ILogger<UpdateController> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public UpdateController(UpdateCheckService updateService, DashboardStorageService diagramStorage,
        ILogger<UpdateController> logger, IHostApplicationLifetime lifetime, IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _updateService = updateService;
        _diagramStorage = diagramStorage;
        _logger = logger;
        _lifetime = lifetime;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var info = _updateService.UpdateInfo;
        return Ok(new
        {
            currentVersion = info.CurrentVersion,
            latestVersion = info.LatestVersion,
            updateAvailable = info.UpdateAvailable,
            deploymentType = info.DeploymentType.ToString(),
            lastChecked = info.LastChecked,
            releaseUrl = info.ReleaseUrl,
            runtimeIdentifier = info.RuntimeIdentifier,
            machineName = Environment.MachineName,
            dataDirectory = _diagramStorage.StoragePath,
            dotNetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        });
    }

    [HttpPost("check")]
    public IActionResult CheckNow()
    {
        _ = Task.Run(() => _updateService.CheckNowAsync());
        return GetStatus();
    }

    /// <summary>
    /// Gracefully stops the application. Under Docker with restart:always, the container
    /// restarts automatically — picking up a newly pulled image if one is available.
    /// For standalone deployments, use the download endpoint instead.
    /// </summary>
    [HttpPost("restart")]
    public IActionResult RestartApp()
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });

        _logger.LogInformation("Application restart requested from web UI");
        // Small delay so the HTTP response can be sent before shutdown begins
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            _lifetime.StopApplication();
        });
        return Ok(new { success = true, message = "Application is shutting down. It will restart automatically." });
    }

    [HttpPost("download")]
    public async Task<IActionResult> DownloadUpdate()
    {
        var info = _updateService.UpdateInfo;
        if (!info.UpdateAvailable)
            return BadRequest(new { error = "No update available." });
        if (info.DeploymentType != DeploymentType.Standalone)
            return BadRequest(new { error = "Self-update is only available for standalone deployments." });
        if (string.IsNullOrEmpty(info.DownloadAssetUrl))
            return BadRequest(new { error = "No download URL available for this runtime." });

        try
        {
            var updatesDir = Path.Combine(AppContext.BaseDirectory, "updates");
            Directory.CreateDirectory(updatesDir);
            var zipPath = Path.Combine(updatesDir, "mqttdashboard-update.zip");

            _logger.LogInformation("Downloading update from {Url}", info.DownloadAssetUrl);
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "MqttDashboard-Updater/1.0");
            var bytes = await http.GetByteArrayAsync(info.DownloadAssetUrl);
            await System.IO.File.WriteAllBytesAsync(zipPath, bytes);

            // Extract to updates/new/
            var extractDir = Path.Combine(updatesDir, "new");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Write update script
            var currentExe = Environment.ProcessPath ?? "MqttDashboard.WebApp";
            var newExe = Path.Combine(extractDir, Path.GetFileName(currentExe));
            string scriptPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath = Path.Combine(updatesDir, "update.bat");
                var bat = $"""
                @echo off
                echo Stopping application...
                taskkill /F /PID {Environment.ProcessId} >nul 2>&1
                timeout /t 2 /nobreak >nul
                echo Applying update...
                copy /Y "{newExe}" "{currentExe}"
                echo Update applied. Restart the application.
                pause
                """;
                await System.IO.File.WriteAllTextAsync(scriptPath, bat);
            }
            else
            {
                scriptPath = Path.Combine(updatesDir, "update.sh");
                var sh = $"""
                #!/usr/bin/env bash
                set -e
                echo "Stopping application (PID {Environment.ProcessId})..."
                kill {Environment.ProcessId} 2>/dev/null || true
                sleep 2
                echo "Applying update..."
                cp -f "{newExe}" "{currentExe}"
                chmod +x "{currentExe}"
                echo "Update applied. Restart the application with:"
                echo "  {currentExe}"
                """;
                await System.IO.File.WriteAllTextAsync(scriptPath, sh);
                try
                {
                    System.IO.File.SetUnixFileMode(scriptPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                catch { /* ignore on platforms that don't support it */ }
            }

            var scriptName = Path.GetFileName(scriptPath);
            return Ok(new
            {
                success = true,
                updatesDir,
                scriptName,
                instructions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? $"Update downloaded. Run '.\\updates\\{scriptName}' to apply (this will stop the app)."
                    : $"Update downloaded. Run './updates/{scriptName}' to apply (this will stop the app)."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update download failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Payload forwarded to the local update agent (agent.py / update-agent.sh)
    // Note: HostUpdateRequest model moved to Server.Models.HostUpdateRequest

    /// <summary>
    /// Request the host-local update agent to pull a new image / restart the service.
    /// The agent is expected to listen on 127.0.0.1 and accept a POST /update JSON body.
    /// </summary>
    [HttpPost("host-update")]
    public async Task<IActionResult> HostUpdate([FromBody] HostUpdateRequest? req)
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (authEnabled && User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Admin authentication required." });
        // Server-side builds the request body forwarded to the update agent.
        // This ensures container/service names and compose files come from server config,
        // while allowing the client to optionally provide the agent token (prompted secret).

        // Build agent URL and token (client may pass AgentToken in the request to prompt for secret)
        var agentUrl = _configuration["UpdateAgent:Url"] ?? "http://127.0.0.1:8080/update";
        var configuredToken = _configuration["UpdateAgent:Token"];
        // If client provided AgentToken (prompted), it takes precedence for this request.
        var agentToken = req?.AgentToken ?? configuredToken;

        var forwardReq = new HostUpdateRequest();

        // Configuration keys the server will use to determine update target
        var confService = _configuration["UpdateAgent:Service"];
        var confCompose = _configuration["UpdateAgent:ComposeFile"];
        var confWatchtower = _configuration["UpdateAgent:WatchtowerContainer"];
        var confWorkdir = _configuration["UpdateAgent:Workdir"];

        // When a client-supplied request body is provided with either composefile or container
        // then use it as is
        // this is used by unit tests: forward it directly to the agent using the
        // IHttpClientFactory-provided HttpClient and return the response.
        if (req != null && (req.ComposeFile != null || req.WatchtowerContainer != null))
        {
            try
            {
                var clientDirect = _httpClientFactory.CreateClient("UpdateAgent");
                if (clientDirect == null) clientDirect = _httpClientFactory.CreateClient();
                if (!string.IsNullOrEmpty(req.AgentToken)) clientDirect.DefaultRequestHeaders.Add("X-Update-Token", req.AgentToken);
                var respDirect = await clientDirect.PostAsJsonAsync(agentUrl, req);
                var bodyDirect = await respDirect.Content.ReadAsStringAsync();
                if ((int)respDirect.StatusCode >= 200 && (int)respDirect.StatusCode <= 299)
                {
                    try { return Content(bodyDirect, "application/json"); }
                    catch { return Ok(new { raw = bodyDirect }); }
                }
                else
                {
                    return StatusCode((int)respDirect.StatusCode, new { error = "Agent returned error", details = bodyDirect });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Host update request failed");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Normal client will just send a token entered by caller, so build ongoing reqeust frmo that
        // and server configuration to build the forward request
        if (string.IsNullOrEmpty(confService) && string.IsNullOrEmpty(confWatchtower))
            return BadRequest(new { error = "No update target configured." });

        forwardReq.Service = confService;
        forwardReq.ComposeFile = confCompose ?? "docker-compose.yml";
        forwardReq.WatchtowerContainer = confWatchtower;
        forwardReq.Workdir = confWorkdir;

        try
        {
            var client = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(agentToken)) client.DefaultRequestHeaders.Add("X-Update-Token", agentToken);

            var resp = await client.PostAsJsonAsync(agentUrl, forwardReq);
            var body = await resp.Content.ReadAsStringAsync();

            if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode <= 299)
            {
                try { return Content(body, "application/json"); }
                catch { return Ok(new { raw = body }); }
            }
            else
            {
                return StatusCode((int)resp.StatusCode, new { error = "Agent returned error", details = body });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Host update request failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
