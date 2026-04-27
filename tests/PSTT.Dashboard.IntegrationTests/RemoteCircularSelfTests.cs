using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Server.Controllers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PSTT.Dashboard.IntegrationTests;

// ── Shared fixture ────────────────────────────────────────────────────────────

/// <summary>
/// Builds the server once and registers it as its own remote ("Self").
/// Shared across all tests via <see cref="IClassFixture{T}"/>.
/// </summary>
public class CircularTestFixture : IAsyncLifetime
{
    public CircularIntegrationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string Token { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new CircularIntegrationFactory();
        // Build the server (first CreateClient triggers WebApplicationFactory.EnsureServer)
        // then initialize the in-process handler BEFORE any requests are in-flight.
        Factory.CreateClient().Dispose();
        Factory.InitInProcessHandler();

        Client = Factory.CreateClient();

        var tokenResp = await Client.GetFromJsonAsync<TokenResponse>("/api/settings/remote-access/token");
        Token = tokenResp?.Token ?? throw new InvalidOperationException("Failed to get API token");

        // Generating the token enables token auth on protected endpoints.
        // Add Bearer so Client can write dashboards directly.
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        var baseUrl = Client.BaseAddress!.ToString().TrimEnd('/');
        var addResp = await Client.PostAsJsonAsync("/api/settings/remote-repos",
            new RemoteRepoRequest("Self", baseUrl, Token));
        if (!addResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to register Self remote: {addResp.StatusCode}");
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        Factory?.Dispose();
        return Task.CompletedTask;
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Integration tests for a server configured as its own remote repository.
/// The custom <see cref="CircularIntegrationFactory"/> intercepts outbound proxy calls
/// and routes them back through the in-process test server via a pre-created handler.
/// </summary>
public class RemoteCircularSelfTests(CircularTestFixture fixture) : IClassFixture<CircularTestFixture>
{
    private HttpClient Client => fixture.Client;
    private string Token => fixture.Token;
    private CircularIntegrationFactory Factory => fixture.Factory;

    // ── Local CRUD ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveLocally_CanRead()
    {
        var dashboard = new DashboardModel { Name = "LocalSave", Pages = [new DashboardPageModel { Name = "Page1" }] };
        await Client.PostAsJsonAsync("/api/dashboard/CircSaveLocal", dashboard);

        var read = await Client.GetFromJsonAsync<DashboardModel>("/api/dashboard/CircSaveLocal");
        Assert.NotNull(read);
        Assert.Equal("LocalSave", read.Name);
    }

    [Fact]
    public async Task ListLocalDashboards_ReturnsSavedItems()
    {
        await Client.PostAsJsonAsync("/api/dashboard/CircList1", new DashboardModel { Name = "D1", Pages = [] });
        await Client.PostAsJsonAsync("/api/dashboard/CircList2", new DashboardModel { Name = "D2", Pages = [] });

        var list = await Client.GetFromJsonAsync<List<string>>("/api/dashboard/list");
        Assert.NotNull(list);
        Assert.Contains("CircList1", list);
        Assert.Contains("CircList2", list);
    }

    [Fact]
    public async Task DeleteLocalDashboard_RemovedFromList()
    {
        await Client.PostAsJsonAsync("/api/dashboard/CircDelTest", new DashboardModel { Name = "ToDelete", Pages = [] });
        var delResp = await Client.DeleteAsync("/api/dashboard/CircDelTest");
        Assert.True(delResp.IsSuccessStatusCode);

        var getResp = await Client.GetAsync("/api/dashboard/CircDelTest");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    // ── Bearer token auth ─────────────────────────────────────────────────────

    [Fact]
    public async Task BearerToken_ValidToken_AllowsWrite()
    {
        using var bearer = Factory.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        var resp = await bearer.PostAsJsonAsync("/api/dashboard/CircBearerTest",
            new DashboardModel { Name = "BearerSave", Pages = [] });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task BearerToken_InvalidToken_Returns401()
    {
        using var bearer = Factory.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "totally-wrong-token");

        var resp = await bearer.PostAsJsonAsync("/api/dashboard/CircBadTokenTest",
            new DashboardModel { Name = "BadToken", Pages = [] });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task BearerToken_NoAuthHeader_Returns401()
    {
        using var bearer = Factory.CreateClient();
        var resp = await bearer.PostAsJsonAsync("/api/dashboard/CircNoAuthTest",
            new DashboardModel { Name = "NoAuth", Pages = [] });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Remote proxy (circular) ───────────────────────────────────────────────

    [Fact]
    public async Task CircularProxy_ListDashboards_ReturnsOk()
    {
        await Client.PostAsJsonAsync("/api/dashboard/CircProxyListDash", new DashboardModel { Name = "PL", Pages = [] });

        var resp = await Client.GetAsync("/api/remote/Self/list");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(list);
        Assert.Contains("CircProxyListDash", list);
    }

    [Fact]
    public async Task CircularProxy_GetDashboard_ReturnsCorrectContent()
    {
        var dashboard = new DashboardModel { Name = "ProxyGet", Pages = [new DashboardPageModel { Name = "P1" }] };
        await Client.PostAsJsonAsync("/api/dashboard/CircProxyGetDash", dashboard);

        var resp = await Client.GetAsync("/api/remote/Self/CircProxyGetDash");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("ProxyGet", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CircularProxy_SaveDashboard_CanReadBack()
    {
        var dashboard = new DashboardModel { Name = "ProxySaved", Pages = [new DashboardPageModel { Name = "Page1" }] };
        var json = JsonSerializer.Serialize(dashboard);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var saveResp = await Client.PostAsync("/api/remote/Self/CircProxySaveDash", content);
        Assert.Equal(HttpStatusCode.OK, saveResp.StatusCode);

        var read = await Client.GetFromJsonAsync<DashboardModel>("/api/dashboard/CircProxySaveDash");
        Assert.NotNull(read);
        Assert.Equal("ProxySaved", read.Name);
    }

    [Fact]
    public async Task CircularProxy_DeleteDashboard_RemovesIt()
    {
        await Client.PostAsJsonAsync("/api/dashboard/CircProxyDelDash", new DashboardModel { Name = "Del", Pages = [] });

        var delResp = await Client.DeleteAsync("/api/remote/Self/CircProxyDelDash");
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/dashboard/CircProxyDelDash")).StatusCode);
    }

    [Fact]
    public async Task UnknownRemote_Returns404()
    {
        var resp = await Client.GetAsync("/api/remote/NonExistent/list");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Remote repo CRUD (add/edit/delete) ────────────────────────────────────

    [Fact]
    public async Task AddRemoteRepo_DuplicateName_Returns409()
    {
        var addResp = await Client.PostAsJsonAsync("/api/settings/remote-repos",
            new RemoteRepoRequest("Self", "https://example.com", "token123"));
        Assert.Equal(HttpStatusCode.Conflict, addResp.StatusCode);
    }

    [Fact]
    public async Task EditRemoteRepo_UpdatesNameAndUrl()
    {
        await Client.PostAsJsonAsync("/api/settings/remote-repos",
            new RemoteRepoRequest("CircEditTest", "https://original.example.com", "tok1"));

        var putResp = await Client.PutAsJsonAsync("/api/settings/remote-repos/CircEditTest",
            new RemoteRepoRequest("CircEditTestRenamed", "https://updated.example.com", "tok2"));
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var list = await Client.GetFromJsonAsync<List<RemoteRepoDto>>("/api/settings/remote-repos");
        Assert.NotNull(list);
        Assert.Contains(list, r => r.Name == "CircEditTestRenamed" && r.Url == "https://updated.example.com");
        Assert.DoesNotContain(list, r => r.Name == "CircEditTest");
    }

    [Fact]
    public async Task EditRemoteRepo_NotFound_Returns404()
    {
        var resp = await Client.PutAsJsonAsync("/api/settings/remote-repos/DoesNotExist",
            new RemoteRepoRequest("Whatever", "https://example.com", "tok"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task EditRemoteRepo_RenameConflict_Returns409()
    {
        await Client.PostAsJsonAsync("/api/settings/remote-repos",
            new RemoteRepoRequest("CircConflictA", "https://a.example.com", "tokA"));
        await Client.PostAsJsonAsync("/api/settings/remote-repos",
            new RemoteRepoRequest("CircConflictB", "https://b.example.com", "tokB"));

        var resp = await Client.PutAsJsonAsync("/api/settings/remote-repos/CircConflictA",
            new RemoteRepoRequest("CircConflictB", "https://a.example.com", "tokA"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }
}

// ── Test factory ──────────────────────────────────────────────────────────────

/// <summary>
/// Replaces <see cref="IHttpClientFactory"/> with one that routes proxy calls back
/// through the test server's in-process handler (pre-created before any requests).
/// </summary>
public class CircularIntegrationFactory : IntegrationWebApplicationFactory
{
    private HttpMessageHandler? _inProcessHandler;

    /// <summary>
    /// Must be called after <see cref="WebApplicationFactory{T}.CreateClient()"/> has been called
    /// (server is built) but before any proxy requests are made.
    /// </summary>
    public void InitInProcessHandler()
        => _inProcessHandler = Server.CreateHandler();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(sp => new InProcessHttpClientFactory(this));
        });
    }

    private sealed class InProcessHttpClientFactory(CircularIntegrationFactory factory) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "")
        {
            var handler = factory._inProcessHandler;
            if (handler == null)
            {
                // Called during host startup (e.g., UpdateCheckService) before handler is initialized.
                // Return a plain HttpClient — these callers don't need the in-process route.
                return new HttpClient();
            }
            // Create a fresh HttpClient each time (safe to set BaseAddress/headers).
            // The shared handler is thread-safe for concurrent SendAsync calls.
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}

// ── Shared types ──────────────────────────────────────────────────────────────

public record TokenResponse(string Token, string? Warning);
public record RemoteRepoDto(string Name, string Url);
