namespace PSTT.Dashboard.Services;

/// <summary>
/// Service for reading app-level settings.
/// Server-side implementation reads IConfiguration directly; WASM uses HttpClient.
/// </summary>
public interface IAppSettingsService
{
    Task<AppSettingsDto> GetAppSettingsAsync();
}

public record AppSettingsDto(bool AutoSaveOnExit, List<AlternateInstanceDto> AlternateInstances);
public record AlternateInstanceDto(string Label, string Url);
