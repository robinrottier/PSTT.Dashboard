using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Server-side implementation of IStartupSettingsService.
/// Reads IConfiguration directly to avoid HTTP loopback calls.
/// </summary>
public class ServerStartupSettingsService : IStartupSettingsService
{
    private readonly IConfiguration _configuration;

    public ServerStartupSettingsService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<StartupSettingsDto> GetStartupSettingsAsync()
    {
        var mode = _configuration["Startup:Mode"] ?? "LastUsed";
        var dashboard = _configuration["Startup:Dashboard"] ?? string.Empty;
        return Task.FromResult(new StartupSettingsDto(mode, dashboard));
    }
}
