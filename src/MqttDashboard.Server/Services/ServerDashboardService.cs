using MqttDashboard.Models;
using MqttDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of IDashboardService for server-only deployments.
/// Calls DiagramStorageService directly instead of going through HTTP.
/// Admin checks replicate RequireAdminFilter logic using IHttpContextAccessor.
/// </summary>
public class ServerDashboardService : IDashboardService
{
    private readonly DashboardStorageService _storage;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerDashboardService>? _logger;

    public ServerDashboardService(
        DashboardStorageService storage,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ServerDashboardService>? logger = null)
    {
        _storage = storage;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<DashboardModel?> LoadDashboardAsync() =>
        _storage.LoadDashboardAsync();

    public Task<List<string>> ListDashboardsAsync() =>
        _storage.ListDiagramNamesAsync();

    public Task<DashboardModel?> LoadDashboardByNameAsync(string name) =>
        _storage.LoadDashboardByNameAsync(name);

    public async Task<bool> SaveDashboardAsync(DashboardModel dashboard)
    {
        if (!IsAdminAuthorized()) return false;
        return await _storage.SaveDashboardAsync(dashboard);
    }

    public async Task<bool> SaveDashboardByNameAsync(string name, DashboardModel dashboard)
    {
        if (!IsAdminAuthorized()) return false;
        return await _storage.SaveDashboardByNameAsync(name, dashboard);
    }

    public async Task<bool> DeleteDashboardByNameAsync(string name)
    {
        if (!IsAdminAuthorized()) return false;
        return await _storage.DeleteDashboardByNameAsync(name);
    }

    private bool IsAdminAuthorized()
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled) return true;

        var isAuthenticated = _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
        if (!isAuthenticated)
            _logger?.LogWarning("Save rejected: admin authentication required");
        return isAuthenticated;
    }
}

