using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Server-side implementation of IAppSettingsService.
/// Reads IConfiguration directly to avoid HTTP loopback calls.
/// </summary>
public class ServerAppSettingsService : IAppSettingsService
{
    private readonly IConfiguration _configuration;

    public ServerAppSettingsService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<AppSettingsDto> GetAppSettingsAsync()
    {
        var autoSaveOnExit = _configuration.GetValue<bool>("App:AutoSaveOnExit", false);
        var alternateInstances = _configuration
            .GetSection("App:AlternateInstances")
            .Get<List<AlternateInstanceEntry>>() ?? [];

        var dtos = alternateInstances
            .Select(a => new AlternateInstanceDto(a.Label, a.Url))
            .ToList();

        return Task.FromResult(new AppSettingsDto(autoSaveOnExit, dtos));
    }

    private class AlternateInstanceEntry
    {
        public string Label { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
