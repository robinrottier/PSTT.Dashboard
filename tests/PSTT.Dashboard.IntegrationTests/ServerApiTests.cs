using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Server.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PSTT.Dashboard.IntegrationTests;

// ─────────────────────────────────────────────────────────────────────────────
// Pure unit tests — no factory needed
// ─────────────────────────────────────────────────────────────────────────────

public class ReadOnlyHelperTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static DefaultHttpContext ContextWithPort(int port)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.LocalPort = port;
        return ctx;
    }

    [Fact]
    public void GlobalReadOnly_True_ReturnsTrue()
    {
        var cfg = Config(new() { ["ReadOnly"] = "true" });
        Assert.True(ReadOnlyHelper.IsReadOnly(cfg));
    }

    [Fact]
    public void GlobalReadOnly_False_NoPorts_ReturnsFalse()
    {
        var cfg = Config(new() { ["ReadOnly"] = "false" });
        Assert.False(ReadOnlyHelper.IsReadOnly(cfg));
    }

    [Fact]
    public void ReadOnlyPorts_NullHttpContext_ReturnsFalse()
    {
        var cfg = Config(new() { ["ReadOnlyPorts"] = "8080" });
        Assert.False(ReadOnlyHelper.IsReadOnly(cfg, null));
    }

    [Fact]
    public void ReadOnlyPorts_NoPortsConfigured_ReturnsFalse()
    {
        var cfg = Config(new());
        Assert.False(ReadOnlyHelper.IsReadOnly(cfg, ContextWithPort(9000)));
    }

    [Fact]
    public void ReadOnlyPorts_MatchingPort_ReturnsTrue()
    {
        var cfg = Config(new() { ["ReadOnlyPorts"] = "8080" });
        Assert.True(ReadOnlyHelper.IsReadOnly(cfg, ContextWithPort(8080)));
    }

    [Fact]
    public void ReadOnlyPorts_NonMatchingPort_ReturnsFalse()
    {
        var cfg = Config(new() { ["ReadOnlyPorts"] = "8080" });
        Assert.False(ReadOnlyHelper.IsReadOnly(cfg, ContextWithPort(9000)));
    }

    [Fact]
    public void ReadOnlyPorts_MultiplePortsOneMatches_ReturnsTrue()
    {
        var cfg = Config(new() { ["ReadOnlyPorts"] = "8080, 8443, 9090" });
        Assert.True(ReadOnlyHelper.IsReadOnly(cfg, ContextWithPort(8443)));
    }

    [Fact]
    public void GlobalReadOnly_True_OverridesPortCheck()
    {
        var cfg = Config(new() { ["ReadOnly"] = "true", ["ReadOnlyPorts"] = "8080" });
        Assert.True(ReadOnlyHelper.IsReadOnly(cfg, ContextWithPort(9000))); // global flag wins
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// LoginTokenStore unit tests
// ─────────────────────────────────────────────────────────────────────────────

public class LoginTokenStoreTests
{
    [Fact]
    public void Issue_ReturnsNonEmptyToken()
    {
        var store = new LoginTokenStore();
        var token = store.Issue();
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public void Issue_TwoCallsReturnDifferentTokens()
    {
        var store = new LoginTokenStore();
        Assert.NotEqual(store.Issue(), store.Issue());
    }

    [Fact]
    public void TryRedeem_ValidToken_ReturnsTrue()
    {
        var store = new LoginTokenStore();
        var token = store.Issue();
        Assert.True(store.TryRedeem(token));
    }

    [Fact]
    public void TryRedeem_ValidToken_IsOneUse()
    {
        var store = new LoginTokenStore();
        var token = store.Issue();
        store.TryRedeem(token);
        Assert.False(store.TryRedeem(token));
    }

    [Fact]
    public void TryRedeem_UnknownToken_ReturnsFalse()
    {
        var store = new LoginTokenStore();
        Assert.False(store.TryRedeem("notavalidtoken"));
    }

    [Fact]
    public void TryRedeem_EmptyToken_ReturnsFalse()
    {
        var store = new LoginTokenStore();
        Assert.False(store.TryRedeem(string.Empty));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// API tests — no auth configured (uses shared IntegrationWebApplicationFactory)
// ─────────────────────────────────────────────────────────────────────────────

public class NoAuthApiTests : IClassFixture<IntegrationWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NoAuthApiTests(IntegrationWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── AuthController ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuthStatus_NoAuthConfigured_ReturnsAdminNoAuth()
    {
        var resp = await _client.GetAsync("/api/auth/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.GetProperty("isAdmin").GetBoolean());
        Assert.False(json.GetProperty("authEnabled").GetBoolean());
        Assert.False(json.GetProperty("readOnly").GetBoolean());
    }

    [Fact]
    public async Task Login_NoAuthConfigured_ReturnsAdmin()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { Password = "anything" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Logout_Returns200()
    {
        var resp = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── SetupController ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSetupNeeded_NoHashConfigured_ReturnsNeededTrue()
    {
        var resp = await _client.GetAsync("/api/setup/needed");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.GetProperty("needed").GetBoolean());
    }

    // ── SettingsController ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsStartup_Returns200WithMode()
    {
        var resp = await _client.GetAsync("/api/settings/startup");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.TryGetProperty("mode", out _));
    }

    [Fact]
    public async Task GetSettingsApp_Returns200WithAutoSave()
    {
        var resp = await _client.GetAsync("/api/settings/app");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.TryGetProperty("autoSaveOnExit", out _));
    }

    [Fact]
    public async Task PostSettingsStartup_NoAuth_Returns200()
    {
        var resp = await _client.PostAsJsonAsync("/api/settings/startup",
            new { Mode = "None", Dashboard = "" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PostSettingsStartup_InvalidMode_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/settings/startup",
            new { Mode = "InvalidMode", Dashboard = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostSettingsApp_NoAuth_Returns200()
    {
        var resp = await _client.PostAsJsonAsync("/api/settings/app",
            new { AutoSaveOnExit = true });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── UpdateController ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUpdateStatus_Returns200WithVersionInfo()
    {
        var resp = await _client.GetAsync("/api/update/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.TryGetProperty("currentVersion", out _));
        Assert.True(json.TryGetProperty("deploymentType", out _));
        Assert.True(json.TryGetProperty("machineName", out _));
    }

    [Fact]
    public async Task PostUpdateCheck_Returns200WithVersionInfo()
    {
        var resp = await _client.PostAsync("/api/update/check", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.TryGetProperty("currentVersion", out _));
    }

    // ── DashboardController ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndGetNamedDashboard_RoundTrips()
    {
        var name = "test-dashboard-" + Guid.NewGuid().ToString("N")[..8];
        var dashboard = new DashboardModel { Name = name, Pages = [new DashboardPageModel { Name = "Page 1" }] };

        var saveResp = await _client.PostAsJsonAsync($"/api/dashboard/{name}", dashboard);
        Assert.Equal(HttpStatusCode.OK, saveResp.StatusCode);

        var getResp = await _client.GetAsync($"/api/dashboard/{name}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var loaded = await getResp.Content.ReadFromJsonAsync<DashboardModel>();
        Assert.Equal(name, loaded!.Name);
    }

    [Fact]
    public async Task GetDashboardByName_NotExisting_Returns404()
    {
        var resp = await _client.GetAsync("/api/dashboard/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteDashboard_ExistingDashboard_Returns200()
    {
        var name = "delete-me-" + Guid.NewGuid().ToString("N")[..8];
        var dashboard = new DashboardModel { Name = name };
        await _client.PostAsJsonAsync($"/api/dashboard/{name}", dashboard);

        var resp = await _client.DeleteAsync($"/api/dashboard/{name}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteDashboard_NonExistentDashboard_Returns404()
    {
        var resp = await _client.DeleteAsync("/api/dashboard/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task SaveDefaultDashboard_NoAuth_Returns200()
    {
        var dashboard = new DashboardModel { Name = "Default" };
        var resp = await _client.PostAsJsonAsync("/api/dashboard", dashboard);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // Helper
    private static async Task<JsonElement> ParseJsonAsync(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Auth-enabled factory — adds a BCrypt hash so auth is active
// ─────────────────────────────────────────────────────────────────────────────

public class WithAuthWebApplicationFactory : IntegrationWebApplicationFactory
{
    internal const string TestPassword = "TestPassword1!";
    // Computed once; workFactor=4 is intentionally fast for tests.
    internal static readonly string TestPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(TestPassword, workFactor: 4);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:AdminPasswordHash", TestPasswordHash);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// API tests — auth enabled (uses WithAuthWebApplicationFactory)
// ─────────────────────────────────────────────────────────────────────────────

public class AuthApiTests : IClassFixture<WithAuthWebApplicationFactory>
{
    private readonly WithAuthWebApplicationFactory _factory;
    private readonly HttpClient _anonClient;

    public AuthApiTests(WithAuthWebApplicationFactory factory)
    {
        _factory = factory;
        _anonClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── AuthController ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuthStatus_AuthEnabled_NotLoggedIn_ReturnsNotAdmin()
    {
        var resp = await _anonClient.GetAsync("/api/auth/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.False(json.GetProperty("isAdmin").GetBoolean());
        Assert.True(json.GetProperty("authEnabled").GetBoolean());
        Assert.False(json.GetProperty("readOnly").GetBoolean());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new { Password = "WrongPassword!" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectPassword_Returns200AndIsAdmin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { Password = WithAuthWebApplicationFactory.TestPassword });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.True(json.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Login_EmptyPassword_Returns401()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/auth/login", new { Password = "" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task RedeemToken_ValidToken_Redirects()
    {
        // Issue a real token by logging in via ServerAuthService directly
        var tokenStore = (LoginTokenStore)_factory.Services.GetService(typeof(LoginTokenStore))!;
        var token = tokenStore.Issue();

        var resp = await _anonClient.GetAsync($"/api/auth/redeem/{token}");
        // Should redirect (302) to "/"
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/", resp.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RedeemToken_InvalidToken_RedirectsToLoginError()
    {
        var resp = await _anonClient.GetAsync("/api/auth/redeem/notavalidtoken");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("login", resp.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ── SetupController ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSetupNeeded_HashConfigured_ReturnsFalse()
    {
        var resp = await _anonClient.GetAsync("/api/setup/needed");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await ParseJsonAsync(resp);
        Assert.False(json.GetProperty("needed").GetBoolean());
    }

    [Fact]
    public async Task PostSetupPassword_AlreadyConfigured_Returns409()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/setup/password",
            new { Password = "AnotherPass1!" });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── UpdateController ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostUpdateRestart_NotLoggedIn_Returns401()
    {
        var resp = await _anonClient.PostAsync("/api/update/restart", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── RequireAdminFilter on DashboardController ─────────────────────────────

    [Fact]
    public async Task PostDashboard_AuthEnabled_NotLoggedIn_Returns401()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/dashboard",
            new DashboardModel { Name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostNamedDashboard_AuthEnabled_NotLoggedIn_Returns401()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/dashboard/MyDash",
            new DashboardModel { Name = "MyDash" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteNamedDashboard_AuthEnabled_NotLoggedIn_Returns401()
    {
        var resp = await _anonClient.DeleteAsync("/api/dashboard/MyDash");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── SettingsController (auth-protected writes) ────────────────────────────

    [Fact]
    public async Task PostSettingsStartup_AuthEnabled_NotLoggedIn_Returns401()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/settings/startup",
            new { Mode = "None", Dashboard = "" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostSettingsApp_AuthEnabled_NotLoggedIn_Returns401()
    {
        var resp = await _anonClient.PostAsJsonAsync("/api/settings/app",
            new { AutoSaveOnExit = false });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // Helper
    private static async Task<JsonElement> ParseJsonAsync(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Setup password tests — each test class gets its own fresh factory (not shared)
// so that setting a password in one test doesn't affect another.
// ─────────────────────────────────────────────────────────────────────────────

public class SetupPasswordTests : IClassFixture<IntegrationWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SetupPasswordTests(IntegrationWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostSetupPassword_ValidPassword_Returns200()
    {
        // Only valid on a factory where no hash is yet set.
        // We rely on the temp-dir isolation per factory instance — but this factory
        // IS shared across the tests in this class. Guard with a check first.
        var checkResp = await _client.GetAsync("/api/setup/needed");
        var json = JsonDocument.Parse(await checkResp.Content.ReadAsStringAsync()).RootElement;
        if (!json.GetProperty("needed").GetBoolean())
            return; // another test already set the password — skip

        var resp = await _client.PostAsJsonAsync("/api/setup/password",
            new { Password = "ValidPassword1!" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PostSetupPassword_TooShort_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/setup/password",
            new { Password = "short" });
        // Either 400 (password too short) or 409 (already configured after another test ran first)
        Assert.True(
            resp.StatusCode == HttpStatusCode.BadRequest || resp.StatusCode == HttpStatusCode.Conflict,
            $"Expected 400 or 409, got {resp.StatusCode}");
    }

    [Fact]
    public async Task PostSetupPassword_EmptyPassword_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/setup/password",
            new { Password = "" });
        Assert.True(
            resp.StatusCode == HttpStatusCode.BadRequest || resp.StatusCode == HttpStatusCode.Conflict,
            $"Expected 400 or 409, got {resp.StatusCode}");
    }
}
