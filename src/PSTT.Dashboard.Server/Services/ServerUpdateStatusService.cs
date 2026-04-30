using System.Runtime.InteropServices;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Server-side implementation of IUpdateStatusService.
/// Reads UpdateCheckService directly to avoid HTTP loopback calls.
/// </summary>
public class ServerUpdateStatusService : IUpdateStatusService
{
    private readonly UpdateCheckService _updateService;
    private readonly DashboardStorageService _diagramStorage;

    public ServerUpdateStatusService(UpdateCheckService updateService, DashboardStorageService diagramStorage)
    {
        _updateService = updateService;
        _diagramStorage = diagramStorage;
    }

    public Task<UpdateStatusDto?> GetStatusAsync()
    {
        var info = _updateService.UpdateInfo;
        return Task.FromResult<UpdateStatusDto?>(new UpdateStatusDto(
            info.CurrentVersion,
            info.LatestVersion,
            info.UpdateAvailable,
            info.DeploymentType.ToString(),
            info.LastChecked,
            info.ReleaseUrl,
            info.RuntimeIdentifier,
            Environment.MachineName,
            _diagramStorage.StoragePath,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription));
    }
}
