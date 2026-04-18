using MqttDashboard.Models;

namespace MqttDashboard.Services;

public interface IDashboardService
{
    Task<DashboardModel?> LoadDashboardAsync();
    Task<List<string>> ListDashboardsAsync();
    Task<DashboardModel?> LoadDashboardByNameAsync(string name);
    Task<bool> SaveDashboardAsync(DashboardModel dashboard);
    Task<bool> SaveDashboardByNameAsync(string name, DashboardModel dashboard);
    Task<bool> DeleteDashboardByNameAsync(string name);
}

