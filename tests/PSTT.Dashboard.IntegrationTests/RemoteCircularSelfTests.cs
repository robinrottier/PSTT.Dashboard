using Microsoft.AspNetCore.Mvc.Testing;
using PSTT.Dashboard.Models;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// Integration tests for a server configured as its own remote repository.
/// Note: Full circular tests require actual network connectivity. This tests the setup and proxy configuration.
/// </summary>
public class RemoteCircularSelfTests : IAsyncLifetime
{
    private IntegrationWebApplicationFactory? _factory;
    private HttpClient? _client;
    private string? _token;
    private string? _baseUrl;

    public async Task InitializeAsync()
    {
        _factory = new IntegrationWebApplicationFactory();
        _client = _factory.CreateClient();

        // Get the server's incoming token
        var tokenResp = await _client.GetFromJsonAsync<TokenResponse>("/api/settings/remote-access/token");
        _token = tokenResp?.Token ?? throw new InvalidOperationException("Failed to get token");

        // Construct base URL from the client's actual base address
        var originalBase = _client.BaseAddress?.ToString() ?? throw new InvalidOperationException("No base address");
        _baseUrl = originalBase.TrimEnd('/');

        // Register server as its own remote named "Self"
        var addRepoRequest = new { name = "Self", url = _baseUrl, apiToken = _token };
        var addResp = await _client.PostAsJsonAsync("/api/settings/remote-repos", addRepoRequest);
        Assert.True(addResp.IsSuccessStatusCode, $"Failed to register self as remote: {addResp.StatusCode}");

        // Add Bearer token to all subsequent requests
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task SaveLocally_CanRead()
    {
        var dashboard = new DashboardModel { Name = "LocalSave", Pages = new() { new DashboardPageModel { Name = "Page1" } } };
        var saveResp = await _client!.PostAsJsonAsync("/api/dashboard/TestDash1", dashboard);
        Assert.True(saveResp.IsSuccessStatusCode, $"Failed to save locally: {saveResp.StatusCode}");

        var localRead = await _client.GetFromJsonAsync<DashboardModel>("/api/dashboard/TestDash1");
        Assert.NotNull(localRead);
        Assert.Equal("LocalSave", localRead.Name);
    }

    [Fact]
    public async Task ListLocalDashboards()
    {
        await _client!.PostAsJsonAsync("/api/dashboard/List1", new DashboardModel { Name = "D1", Pages = new() });
        await _client.PostAsJsonAsync("/api/dashboard/List2", new DashboardModel { Name = "D2", Pages = new() });

        var list = await _client.GetFromJsonAsync<List<string>>("/api/dashboard/list");
        Assert.NotNull(list);
        Assert.Contains("List1", list);
        Assert.Contains("List2", list);
    }

    [Fact]
    public async Task DeleteLocalDashboard()
    {
        await _client!.PostAsJsonAsync("/api/dashboard/DelTest", new DashboardModel { Name = "ToDelete", Pages = new() });
        var delResp = await _client.DeleteAsync("/api/dashboard/DelTest");
        Assert.True(delResp.IsSuccessStatusCode, $"Failed to delete: {delResp.StatusCode}");

        // After delete, getting the dashboard should return 404
        var getResp = await _client.GetAsync("/api/dashboard/DelTest");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task RemoteRepoConfigurationStored()
    {
        // Verify the remote repo was registered
        var repos = await _client!.GetFromJsonAsync<List<RemoteRepoDto>>("/api/settings/remote-repos");
        Assert.NotNull(repos);
        var selfRepo = repos?.FirstOrDefault(r => r.Name == "Self");
        Assert.NotNull(selfRepo);
        Assert.NotNull(selfRepo?.Url);
    }

    [Fact]
    public async Task UnknownRemoteReturns404()
    {
        var resp = await _client!.GetAsync("/api/remote/NonExistent/list");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, resp.StatusCode);
    }
}

public record TokenResponse(string Token, string? Warning);
public record RemoteRepoDto(string Name, string Url);
