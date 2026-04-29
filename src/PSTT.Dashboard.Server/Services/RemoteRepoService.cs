using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Server-side implementation of IRemoteRepoService.
/// Reads configuration directly to avoid HTTP loopback calls.
/// </summary>
public class RemoteRepoService : IRemoteRepoService
{
    private readonly IConfiguration _configuration;

    public RemoteRepoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the list of configured remote repositories (name + URL only, no tokens).
    /// </summary>
    public Task<List<RemoteRepoInfo>> GetRemoteReposAsync()
    {
        var repos = _configuration.GetSection("RemoteRepositories")
            .Get<List<RemoteRepoEntry>>() ?? [];

        var result = repos.Select(r => new RemoteRepoInfo(r.Name, r.Url)).ToList();
        return Task.FromResult(result);
    }
}

// Internal DTO that matches the configuration structure
internal record RemoteRepoEntry(string Name, string Url, string ApiToken);
