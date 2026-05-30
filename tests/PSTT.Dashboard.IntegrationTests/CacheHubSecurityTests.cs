using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PSTT.Dashboard.Server.Services;
using PSTT.Remote;
using System.Net;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> variant with a real
/// <c>Auth:AdminPasswordHash</c> configured — simulates a deployment where an admin
/// password has been set but a visitor is browsing without logging in (read-only access).
/// </summary>
public class AuthConfiguredWebApplicationFactory : IntegrationWebApplicationFactory
{
    // bcrypt of "testpassword" — only used to simulate "auth is configured"
    internal const string TestHash = "$2a$10$Jc/qGYnpqxryhqTnedBGi.0HYc5wxmo2DZ5DcNRHR5Dm8Oa3vRnhe";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder); // apply base test settings first
        builder.UseSetting("Auth:AdminPasswordHash", TestHash);
    }
}

/// <summary>
/// Confirms that the <c>/cachehub</c> presence-cookie gate works correctly in
/// both auth-configured and read-only (no auth) modes:
/// <list type="bullet">
///   <item>Direct connections without a valid presence cookie are rejected (401).</item>
///   <item>Connections from a "browser that loaded a page" (valid cookie) succeed and
///         can receive data — even when an admin password IS configured and the user
///         is NOT logged in (the original broken scenario).</item>
/// </list>
/// </summary>
public class CacheHubSecurityTests : IClassFixture<IntegrationWebApplicationFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly IntegrationWebApplicationFactory _factory;
    private readonly HttpClient _http;

    public CacheHubSecurityTests(IntegrationWebApplicationFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    // ── Rejection cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task CacheHub_WithoutCookie_Returns401()
    {
        // Simulate a scanner that hits /cachehub/negotiate directly with no prior page load.
        var response = await _http.PostAsync("/cachehub/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CacheHub_WithInvalidCookie_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/cachehub/negotiate?negotiateVersion=1");
        request.Headers.Add("Cookie", "chsession=not-a-valid-token");
        var response = await _http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Allowed case (read-only data access) ─────────────────────────────────

    [Fact]
    public async Task CacheHub_ReadOnly_WithValidCookie_CanSubscribeAndReceiveData()
    {
        // In read-only mode there is no AdminPasswordHash — any browser that loaded a
        // page holds a valid chsession cookie and must be able to receive MQTT data.
        var tokenSvc = _factory.Services.GetRequiredService<CacheHubTokenService>();
        var token = tokenSvc.IssueToken();

        var httpClient = _factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, "cachehub"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers["Cookie"] = $"chsession={token}";
            })
            .Build();

        var remoteCache = new RemoteCacheBuilder<string>()
            .WithSignalRTransport(connection)
            .WithUtf8Encoding()
            .Build();

        try
        {
            // Connect (should succeed — cookie is valid)
            await remoteCache.ConnectAsync().WaitAsync(Timeout);

            // Subscribe and receive a value to confirm full data-path works
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            remoteCache.Subscribe("readonly/test", sub =>
            {
                tcs.TrySetResult(sub.Value);
                return Task.CompletedTask;
            });

            await _factory.FakeMqttService.TriggerIncomingMessageAsync("readonly/test", "ok");
            var value = await tcs.Task.WaitAsync(Timeout);

            Assert.Equal("ok", value);
        }
        finally
        {
            await remoteCache.DisposeAsync();
        }
    }
}

/// <summary>
/// Regression test for the original bug: when <c>Auth:AdminPasswordHash</c> IS configured
/// (admin password set) an unauthenticated visitor (no login cookie, only a valid presence
/// cookie) was previously blocked from <c>/cachehub</c>. This broke read-only data access.
/// </summary>
public class CacheHubAuthConfiguredTests : IClassFixture<AuthConfiguredWebApplicationFactory>, IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly AuthConfiguredWebApplicationFactory _factory;
    private RemoteCache<string>? _cache;

    public CacheHubAuthConfiguredTests(AuthConfiguredWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CacheHub_AuthConfigured_UnauthenticatedUser_WithPresenceCookie_CanReceiveData()
    {
        // Arrange: auth IS configured (AdminPasswordHash present) but the client has NOT
        // logged in — they only have the presence cookie a browser gets from loading any page.
        // This is the exact scenario that was broken by the previous auth-only gate.
        var tokenSvc = _factory.Services.GetRequiredService<CacheHubTokenService>();
        var httpClient = _factory.CreateClient();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, "cachehub"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                // Only presence cookie — NO login/auth cookie
                options.Headers["Cookie"] = $"chsession={tokenSvc.IssueToken()}";
            })
            .Build();

        _cache = new RemoteCacheBuilder<string>()
            .WithSignalRTransport(connection)
            .WithUtf8Encoding()
            .Build();

        // Act + Assert: connection should succeed and data should flow
        await _cache.ConnectAsync().WaitAsync(Timeout);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cache.Subscribe("authtest/topic", sub =>
        {
            tcs.TrySetResult(sub.Value);
            return Task.CompletedTask;
        });

        await _factory.FakeMqttService.TriggerIncomingMessageAsync("authtest/topic", "visible");
        var value = await tcs.Task.WaitAsync(Timeout);

        Assert.Equal("visible", value);
    }

    [Fact]
    public async Task CacheHub_AuthConfigured_NoCookie_Returns401()
    {
        // Even with auth configured, the gate is the presence cookie — not auth status.
        var http = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await http.PostAsync("/cachehub/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cache != null) await _cache.DisposeAsync();
    }
}
