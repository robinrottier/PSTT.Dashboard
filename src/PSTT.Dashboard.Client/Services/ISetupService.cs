namespace PSTT.Dashboard.Services;

/// <summary>
/// Service for querying setup state.
/// Server-side implementation reads IConfiguration directly; WASM uses HttpClient.
/// </summary>
public interface ISetupService
{
    Task<bool> IsSetupNeededAsync();
}
