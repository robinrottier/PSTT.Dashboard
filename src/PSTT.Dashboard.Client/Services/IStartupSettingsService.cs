namespace PSTT.Dashboard.Services;

/// <summary>
/// Service for reading startup configuration.
/// Server-side implementation reads IConfiguration directly; WASM uses HttpClient.
/// </summary>
public interface IStartupSettingsService
{
    Task<StartupSettingsDto> GetStartupSettingsAsync();
}

public record StartupSettingsDto(string Mode, string? Dashboard);
