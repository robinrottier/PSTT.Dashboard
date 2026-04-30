using System.Net.Http.Json;

namespace PSTT.Dashboard.Services;

public class HttpSetupService : ISetupService
{
    private readonly HttpClient _httpClient;
    public HttpSetupService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<bool> IsSetupNeededAsync()
    {
        try
        {
            var resp = await _httpClient.GetFromJsonAsync<SetupNeededResponse>("/api/setup/needed");
            return resp?.Needed ?? false;
        }
        catch { return false; }
    }

    private record SetupNeededResponse(bool Needed);
}
