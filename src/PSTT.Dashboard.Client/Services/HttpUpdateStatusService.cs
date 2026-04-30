using System.Net.Http.Json;

namespace PSTT.Dashboard.Services;

public class HttpUpdateStatusService : IUpdateStatusService
{
    private readonly HttpClient _httpClient;
    public HttpUpdateStatusService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<UpdateStatusDto?> GetStatusAsync()
    {
        try { return await _httpClient.GetFromJsonAsync<UpdateStatusDto>("/api/update/status"); }
        catch { return null; }
    }
}
