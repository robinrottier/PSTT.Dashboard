using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Server-side implementation of ISetupService.
/// Reads IConfiguration directly to avoid HTTP loopback calls.
/// </summary>
public class ServerSetupService : ISetupService
{
    private readonly IConfiguration _configuration;

    public ServerSetupService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<bool> IsSetupNeededAsync()
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        return Task.FromResult(string.IsNullOrWhiteSpace(hash));
    }
}
