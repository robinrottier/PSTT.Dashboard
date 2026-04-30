using System.Net.Http.Json;

namespace PSTT.Dashboard.Services;

public class HttpAppSettingsService : IAppSettingsService
{
    private readonly HttpClient _httpClient;
    public HttpAppSettingsService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<AppSettingsDto> GetAppSettingsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AppSettingsDto>("/api/settings/app")
                   ?? new AppSettingsDto(false, []);
        }
        catch { return new AppSettingsDto(false, []); }
    }
}
