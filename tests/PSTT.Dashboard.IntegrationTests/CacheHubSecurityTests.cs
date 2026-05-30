using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using PSTT.Dashboard.Server.Services;
using PSTT.Remote;
using System.Net;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// Confirms that the <c>/cachehub</c> presence-cookie gate works correctly in
/// read-only mode (no <c>Auth:AdminPasswordHash</c> configured):
/// <list type="bullet">
///   <item>Direct connections without a valid presence cookie are rejected (401).</item>
///   <item>Connections from a "browser that loaded a page" (valid cookie) succeed and
///         can receive data — i.e. the read-only scenario still has full data access.</item>
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
