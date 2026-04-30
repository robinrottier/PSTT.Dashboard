namespace PSTT.Dashboard.Services;

/// <summary>
/// Service for reading update status.
/// Server-side implementation reads UpdateCheckService directly; WASM uses HttpClient.
/// </summary>
public interface IUpdateStatusService
{
    Task<UpdateStatusDto?> GetStatusAsync();
}

public record UpdateStatusDto(
    string CurrentVersion,
    string? LatestVersion,
    bool UpdateAvailable,
    string DeploymentType,
    DateTimeOffset? LastChecked,
    string? ReleaseUrl,
    string RuntimeIdentifier,
    string? MachineName,
    string? DataDirectory,
    string? DotNetVersion,
    string? OsDescription);
