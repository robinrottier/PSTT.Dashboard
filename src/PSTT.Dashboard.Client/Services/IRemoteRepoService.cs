using PSTT.Dashboard.Models;

namespace PSTT.Dashboard.Services;

/// <summary>
/// Service for managing remote repository configuration.
/// Server-side implementation avoids HTTP loopback; WASM uses HttpClient.
/// </summary>
public interface IRemoteRepoService
{
    /// <summary>
    /// Gets the list of configured remote repositories (name + URL only, no tokens).
    /// </summary>
    Task<List<RemoteRepoInfo>> GetRemoteReposAsync();
}
