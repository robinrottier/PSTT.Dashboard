using System.Net.Http.Json;
using PSTT.Dashboard.Models;
using Microsoft.Extensions.Logging;

namespace PSTT.Dashboard.Services;

/// <summary>
/// Proxies dashboard requests through a local /api/remote/{repoName}/* endpoint
/// to access dashboards on a remote PSTT.Dashboard server.
/// The local server holds the remote's API token and adds it to forwarded requests.
/// </summary>
public class RemoteProxyDashboardService : IDashboardService
{
    private readonly HttpClient _httpClient;
    private readonly string _repoName;
    private readonly ILogger<RemoteProxyDashboardService>? _logger;

    public RemoteProxyDashboardService(HttpClient httpClient, string repoName,
        ILogger<RemoteProxyDashboardService>? logger = null)
    {
        _httpClient = httpClient;
        _repoName = repoName;
        _logger = logger;
    }

    public async Task<DashboardModel?> LoadDashboardAsync()
    {
        _logger?.LogInformation("Loading default dashboard from remote '{RepoName}'", _repoName);
        return await LoadDashboardByNameAsync("Default");
    }

    public async Task<List<string>> ListDashboardsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>($"api/remote/{Uri.EscapeDataString(_repoName)}/list") ?? [];
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error listing dashboards from remote '{RepoName}'", _repoName);
            return [];
        }
    }

    public async Task<DashboardModel?> LoadDashboardByNameAsync(string name)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DashboardModel>(
                $"api/remote/{Uri.EscapeDataString(_repoName)}/{Uri.EscapeDataString(name)}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading dashboard '{Name}' from remote '{RepoName}'", name, _repoName);
            return null;
        }
    }

    public async Task<bool> SaveDashboardAsync(DashboardModel dashboard)
    {
        return await SaveDashboardByNameAsync("Default", dashboard);
    }

    public async Task<bool> SaveDashboardByNameAsync(string name, DashboardModel dashboard)
    {
        try
        {
            _logger?.LogInformation("Saving dashboard '{Name}' to remote '{RepoName}'", name, _repoName);
            var response = await _httpClient.PostAsJsonAsync(
                $"api/remote/{Uri.EscapeDataString(_repoName)}/{Uri.EscapeDataString(name)}", dashboard);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving dashboard '{Name}' to remote '{RepoName}'", name, _repoName);
            return false;
        }
    }

    public async Task<bool> DeleteDashboardByNameAsync(string name)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"api/remote/{Uri.EscapeDataString(_repoName)}/{Uri.EscapeDataString(name)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting dashboard '{Name}' from remote '{RepoName}'", name, _repoName);
            return false;
        }
    }
}
