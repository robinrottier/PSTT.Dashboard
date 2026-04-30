using System.Net.Http.Json;

namespace PSTT.Dashboard.Services;

public class HttpRemoteRepoService : IRemoteRepoService
{
    private readonly HttpClient _httpClient;
    public HttpRemoteRepoService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<List<PSTT.Dashboard.Models.RemoteRepoInfo>> GetRemoteReposAsync()
    {
        try { return await _httpClient.GetFromJsonAsync<List<PSTT.Dashboard.Models.RemoteRepoInfo>>("/api/settings/remote-repos") ?? []; }
        catch { return []; }
    }
}
