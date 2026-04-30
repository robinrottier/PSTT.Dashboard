using System.Net.Http.Json;

namespace PSTT.Dashboard.Services;

public class HttpStartupSettingsService : IStartupSettingsService
{
    private readonly HttpClient _httpClient;
    public HttpStartupSettingsService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<StartupSettingsDto> GetStartupSettingsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<StartupSettingsDto>("/api/settings/startup")
                   ?? new StartupSettingsDto("LastUsed", null);
        }
        catch { return new StartupSettingsDto("LastUsed", null); }
    }
}
