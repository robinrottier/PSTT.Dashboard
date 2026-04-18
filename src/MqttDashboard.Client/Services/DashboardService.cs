using System.Net.Http.Json;
using MqttDashboard.Models;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Services;

public class DashboardService : IDashboardService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DashboardService>? _logger;

    public DashboardService(HttpClient httpClient, ILogger<DashboardService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DashboardModel?> LoadDashboardAsync()
    {
        try
        {
            _logger?.LogInformation("Loading dashboard from API: {Url}", "api/dashboard");
            var result = await _httpClient.GetFromJsonAsync<DashboardModel>("api/dashboard");
            _logger?.LogInformation("dashboard loaded successfully");
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "dashboard doesn't exist yet or request failed. Status: {Status}, Message: {Message}", 
                ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error loading dashboard");
            return null;
        }
    }

    public async Task<List<string>> ListDashboardsAsync()
    {
        try { return await _httpClient.GetFromJsonAsync<List<string>>("api/dashboard/list") ?? []; }
        catch (Exception ex) { _logger?.LogError(ex, "Error listing dashboards"); return []; }
    }

    public async Task<DashboardModel?> LoadDashboardByNameAsync(string name)
    {
        try { return await _httpClient.GetFromJsonAsync<DashboardModel>($"api/dashboard/{Uri.EscapeDataString(name)}"); }
        catch (Exception ex) { _logger?.LogError(ex, "Error loading dashboard '{Name}'", name); return null; }
    }

    public async Task<bool> SaveDashboardByNameAsync(string name, DashboardModel dashboard)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/dashboard/{Uri.EscapeDataString(name)}", dashboard);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error saving dashboard '{Name}'", name); return false; }
    }

    public async Task<bool> SaveDashboardAsync(DashboardModel dashboard)
    {
        try
        {
            _logger?.LogInformation("Saving dashboard to API: {Url} with {PageCount} pages", 
                "api/dashboard", dashboard.Pages.Count);

            var response = await _httpClient.PostAsJsonAsync("api/dashboard", dashboard);

            _logger?.LogInformation("POST response: Status={Status}, ReasonPhrase={Reason}", 
                (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger?.LogError("Save failed. Status: {Status} ({StatusCode}), Reason: {Reason}, Content: {Content}", 
                    response.StatusCode, (int)response.StatusCode, response.ReasonPhrase, content);
                return false;
            }

            _logger?.LogInformation("dashboard saved successfully");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP request failed. Status: {Status}, Message: {Message}", 
                ex.StatusCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error saving dashboard: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteDashboardByNameAsync(string name)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/dashboard/{Uri.EscapeDataString(name)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger?.LogError(ex, "Error deleting dashboard '{Name}'", name); return false; }
    }
}


