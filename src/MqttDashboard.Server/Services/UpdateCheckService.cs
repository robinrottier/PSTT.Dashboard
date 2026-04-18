using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Services;

public enum DeploymentType { Development, Docker, HomeAssistant, Standalone }

public class UpdateInfo
{
    public string CurrentVersion { get; init; } = string.Empty;
    public string? LatestVersion { get; set; }
    public bool UpdateAvailable { get; set; }
    public DeploymentType DeploymentType { get; init; }
    public DateTimeOffset? LastChecked { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? DownloadAssetUrl { get; set; }  // for the correct runtime zip
    public string RuntimeIdentifier { get; init; } = string.Empty;
}

public class UpdateCheckService : BackgroundService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/robinrottier/MqttDashboard/releases/latest";
    private const string UserAgent = "MqttDashboard-UpdateChecker/1.0";
    private readonly ILogger<UpdateCheckService> _logger;
    private readonly HttpClient _httpClient;

    // Shared state — accessed by the API controller
    public UpdateInfo UpdateInfo { get; }

    // Event fired when update status changes
    public event Action? UpdateStatusChanged;

    public UpdateCheckService(ILogger<UpdateCheckService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        // Be defensive in tests/mocks: IHttpClientFactory.CreateClient may return null when using simple mocks.
        _httpClient = httpClientFactory?.CreateClient("UpdateCheck") ?? new HttpClient();
        if (_httpClient.DefaultRequestHeaders != null)
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        var currentVersion = GetCurrentVersion();
        UpdateInfo = new UpdateInfo
        {
            CurrentVersion = currentVersion,
            DeploymentType = DetectDeploymentType(),
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial check after 30-second startup delay
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch { return; }
        await CheckNowAsync();

        // Daily check
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await CheckNowAsync();
            }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task CheckNowAsync()
    {
        try
        {
            _logger.LogInformation("Checking for updates...");
            var response = await _httpClient.GetStringAsync(GitHubApiUrl);
            var release = JsonSerializer.Deserialize<GitHubRelease>(response);
            if (release == null) return;

            var latestTag = release.TagName?.TrimStart('v') ?? string.Empty;
            var currentClean = UpdateInfo.CurrentVersion.Split('+')[0]; // strip +gitsha

            UpdateInfo.LatestVersion = latestTag;
            UpdateInfo.LastChecked = DateTimeOffset.UtcNow;
            UpdateInfo.ReleaseUrl = release.HtmlUrl;

            // Compare versions
            UpdateInfo.UpdateAvailable = IsNewerVersion(latestTag, currentClean);

            // Find the asset for the current runtime
            if (UpdateInfo.UpdateAvailable && release.Assets != null)
            {
                var rid = UpdateInfo.RuntimeIdentifier;
                var asset = release.Assets.FirstOrDefault(a =>
                    a.Name?.Contains(rid, StringComparison.OrdinalIgnoreCase) == true);
                UpdateInfo.DownloadAssetUrl = asset?.BrowserDownloadUrl;
            }

            _logger.LogInformation("Update check: current={Current}, latest={Latest}, available={Available}",
                UpdateInfo.CurrentVersion, latestTag, UpdateInfo.UpdateAvailable);

            UpdateStatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            UpdateInfo.LastChecked = DateTimeOffset.UtcNow;
        }
    }

    private static string GetCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    private static DeploymentType DetectDeploymentType()
    {
        // Home Assistant: /data/options.json exists
        if (File.Exists("/data/options.json"))
            return DeploymentType.HomeAssistant;
        // Docker: environment variable set by dotnet runtime container or docker
        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            return DeploymentType.Docker;
        // Development: version contains 'dev' pre-release
        var version = GetCurrentVersion();
        if (version.Contains("dev", StringComparison.OrdinalIgnoreCase))
            return DeploymentType.Development;
        return DeploymentType.Standalone;
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (string.IsNullOrEmpty(latest)) return false;
        // Strip pre-release suffix for comparison
        var latestBase = latest.Split('-')[0];
        var currentBase = current.Split('-')[0];
        return Version.TryParse(latestBase, out var l)
            && Version.TryParse(currentBase, out var c)
            && l > c;
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
