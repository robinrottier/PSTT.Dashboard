using System.Text.Json;
using MqttDashboard.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace MqttDashboard.Server.Services;

public class DashboardStorageService
{
    private readonly string _storagePath;
    private readonly string _dashboardsPath;
    private readonly ILogger<DashboardStorageService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private const string DiagramFileName = "Default.json";
    private const string LegacyDiagramFileName = "diagram.json";

    public string StoragePath => _storagePath;
    public string DashboardsPath => _dashboardsPath;

    public DashboardStorageService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<DashboardStorageService> logger)
    {
        // Priority: environment variable > appsettings.json > default (ContentRoot/Data)
        var envDir = Environment.GetEnvironmentVariable("DIAGRAM_DATA_DIR");
        var configDir = configuration["DiagramStorage:DataDirectory"];
        _storagePath = !string.IsNullOrWhiteSpace(envDir) ? envDir
                     : !string.IsNullOrWhiteSpace(configDir) ? Path.GetFullPath(configDir, environment.ContentRootPath)
                     : Path.Combine(environment.ContentRootPath, "Data");

        _dashboardsPath = Path.Combine(_storagePath, "dashboards");

        _logger = logger;

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
            _logger.LogInformation("Created data directory at {Path}", _storagePath);
        }
        else
        {
            _logger.LogInformation("Using data directory at {Path}", _storagePath);
        }

        if (!Directory.Exists(_dashboardsPath))
        {
            Directory.CreateDirectory(_dashboardsPath);
            _logger.LogInformation("Created dashboards directory at {Path}", _dashboardsPath);
        }

        MigrateLegacyDashboardFiles();
        MigrateDefaultDashboardFile();
    }

    /// <summary>
    /// Moves any .json dashboard files from the root data directory into the dashboards/ subdirectory,
    /// skipping known non-dashboard files like applicationstate.json.
    /// </summary>
    private void MigrateLegacyDashboardFiles()
    {
        var excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "applicationstate.json", "appsettings.user.json" };

        foreach (var file in Directory.GetFiles(_storagePath, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            if (excludedFiles.Contains(fileName)) continue;

            var dest = Path.Combine(_dashboardsPath, fileName);
            if (!File.Exists(dest))
            {
                try
                {
                    File.Move(file, dest);
                    _logger.LogInformation("Migrated dashboard file {File} to dashboards/ subdirectory", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to migrate dashboard file {File}", fileName);
                }
            }
        }
    }

    /// <summary>
    /// Renames the legacy default file "diagram.json" to "Default.json" if it exists
    /// and the new default file does not yet exist.
    /// </summary>
    private void MigrateDefaultDashboardFile()
    {
        var legacyPath = Path.Combine(_dashboardsPath, LegacyDiagramFileName);
        var newPath = Path.Combine(_dashboardsPath, DiagramFileName);
        if (File.Exists(legacyPath) && !File.Exists(newPath))
        {
            try
            {
                File.Move(legacyPath, newPath);
                _logger.LogInformation("Migrated default dashboard file from '{Legacy}' to '{New}'",
                    LegacyDiagramFileName, DiagramFileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate default dashboard file '{Legacy}'", LegacyDiagramFileName);
            }
        }
    }

    public Task<DashboardModel?> LoadDashboardAsync() => LoadDiagramAsync();
    public Task<bool> SaveDashboardAsync(DashboardModel state) => SaveDiagramAsync(state);
    public Task<DashboardModel?> LoadDashboardByNameAsync(string name) => LoadDiagramByNameAsync(name);
    public Task<bool> SaveDashboardByNameAsync(string name, DashboardModel state) => SaveDiagramByNameAsync(name, state);

    public async Task<DashboardModel?> LoadDiagramAsync()
    {
        var filePath = Path.Combine(_dashboardsPath, DiagramFileName);

        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogInformation("No saved diagram found at {Path}", filePath);
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var dashboard = JsonSerializer.Deserialize<DashboardModel>(json);
                _logger.LogInformation("Loaded diagram from {Path}", filePath);
                return dashboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load diagram from {Path}", filePath);
                return null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> SaveDiagramAsync(DashboardModel dashboard)
    {
        var filePath = Path.Combine(_dashboardsPath, DiagramFileName);

        await _lock.WaitAsync();
        try
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                };
                var json = JsonSerializer.Serialize(dashboard, options);
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation("Saved diagram to {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save diagram to {Path}", filePath);
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> ListDiagramNamesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_dashboardsPath, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !string.IsNullOrEmpty(n) && !n.Equals("appsettings.user", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n)
                .ToList();
            return files!;
        }
        finally { _lock.Release(); }
    }

    public async Task<DashboardModel?> LoadDiagramByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var filePath = Path.Combine(_dashboardsPath, $"{name}.json");
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(filePath)) return null;
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<DashboardModel>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load diagram '{Name}'", name);
            return null;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> SaveDiagramByNameAsync(string name, DashboardModel dashboard)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) return false;
        var filePath = Path.Combine(_dashboardsPath, $"{safeName}.json");
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(dashboard, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Saved diagram '{Name}' to {Path}", safeName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save diagram '{Name}'", name);
            return false;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> DeleteDashboardByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) return false;
        var filePath = Path.Combine(_dashboardsPath, $"{safeName}.json");
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(filePath)) return false;
            File.Delete(filePath);
            _logger.LogInformation("Deleted dashboard '{Name}' from {Path}", safeName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete dashboard '{Name}'", name);
            return false;
        }
        finally { _lock.Release(); }
    }
}
