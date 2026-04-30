using System.Net.Http.Json;

namespace PSTT.Dashboard.Services;

public class HttpRemoteAccessService : IRemoteAccessService
{
    private readonly HttpClient _httpClient;
    public HttpRemoteAccessService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<RemoteAccessTokenDto> GetOrCreateTokenAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RemoteAccessTokenDto>("/api/settings/remote-access/token")
                   ?? new RemoteAccessTokenDto(string.Empty, null);
        }
        catch { return new RemoteAccessTokenDto(string.Empty, null); }
    }
}
