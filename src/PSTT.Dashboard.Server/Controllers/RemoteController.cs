using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PSTT.Dashboard.Server.Controllers;

/// <summary>
/// Server-side proxy that forwards dashboard open/save/list/delete requests
/// to configured remote PSTT.Dashboard instances, adding the stored Bearer token.
/// Tokens never leave the server — the browser only calls local /api/remote/* endpoints.
/// </summary>
[ApiController]
[Route("api/remote")]
[IgnoreAntiforgeryToken]
public class RemoteController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RemoteController> _logger;

    public RemoteController(IConfiguration configuration, IHttpClientFactory httpClientFactory,
        ILogger<RemoteController> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("{repoName}/list")]
    public Task<IActionResult> ListDashboards(string repoName, CancellationToken ct) =>
        ForwardGetAsync(repoName, "api/dashboard/list", ct);

    [HttpGet("{repoName}/{dashboardName}")]
    public Task<IActionResult> GetDashboard(string repoName, string dashboardName, CancellationToken ct) =>
        ForwardGetAsync(repoName, $"api/dashboard/{Uri.EscapeDataString(dashboardName)}", ct);

    [HttpPost("{repoName}/{dashboardName}")]
    public Task<IActionResult> SaveDashboard(string repoName, string dashboardName, CancellationToken ct) =>
        ForwardWriteAsync(repoName, $"api/dashboard/{Uri.EscapeDataString(dashboardName)}", HttpMethod.Post, ct);

    [HttpDelete("{repoName}/{dashboardName}")]
    public Task<IActionResult> DeleteDashboard(string repoName, string dashboardName, CancellationToken ct) =>
        ForwardWriteAsync(repoName, $"api/dashboard/{Uri.EscapeDataString(dashboardName)}", HttpMethod.Delete, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private (string? Url, string? Token) FindRepo(string repoName)
    {
        var repos = _configuration.GetSection("RemoteRepositories").Get<List<RemoteRepoEntry>>() ?? [];
        var repo = repos.FirstOrDefault(r => string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));
        return repo == null ? (null, null) : (repo.Url, repo.ApiToken);
    }

    private async Task<IActionResult> ForwardGetAsync(string repoName, string path, CancellationToken ct)
    {
        var (baseUrl, token) = FindRepo(repoName);
        if (baseUrl == null)
            return NotFound(new { error = $"Remote repository '{repoName}' not found." });

        var client = CreateClient(baseUrl, token);
        try
        {
            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
            return await StreamResponseAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RemoteController] GET {Repo}/{Path} failed", repoName, path);
            return StatusCode(502, new { error = $"Remote request failed: {ex.Message}" });
        }
    }

    private async Task<IActionResult> ForwardWriteAsync(string repoName, string path,
        HttpMethod method, CancellationToken ct)
    {
        var (baseUrl, token) = FindRepo(repoName);
        if (baseUrl == null)
            return NotFound(new { error = $"Remote repository '{repoName}' not found." });

        _logger.LogInformation("[RemoteController] Forwarding {Method} to {Repo}/{Path} with token", 
            method, repoName, path);

        var client = CreateClient(baseUrl, token);
        try
        {
            HttpResponseMessage response;
            if (method == HttpMethod.Post)
            {
                // Read the request body into memory to be able to send it to the remote
                byte[] bodyBytes;
                using (var memStream = new MemoryStream())
                {
                    await Request.Body.CopyToAsync(memStream, ct);
                    bodyBytes = memStream.ToArray();
                }
                
                var content = new ByteArrayContent(bodyBytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(path, content, ct);
                
                _logger.LogInformation("[RemoteController] POST response: {StatusCode}", response.StatusCode);
            }
            else // DELETE
            {
                response = await client.DeleteAsync(path, ct);
                _logger.LogInformation("[RemoteController] DELETE response: {StatusCode}", response.StatusCode);
            }

            using (response)
                return await StreamResponseAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RemoteController] {Method} {Repo}/{Path} failed", method, repoName, path);
            return StatusCode(502, new { error = $"Remote request failed: {ex.Message}" });
        }
    }

    private async Task<IActionResult> StreamResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var status = (int)response.StatusCode;
        // StatusCode(status, null) can be silently converted to 204 by ASP.NET output formatters.
        // Use StatusCodeResult (no body) explicitly when the upstream returned an empty body.
        return body == string.Empty ? new StatusCodeResult(status) : StatusCode(status, body);
    }

    private HttpClient CreateClient(string baseUrl, string? token)
    {
        // Check if this is a loopback/self-reference by comparing the base URL to the current request
        var isSelfReference = false;
        if (Request?.Host.Host != null && Uri.TryCreate(baseUrl, UriKind.Absolute, out var remoteUri))
        {
            isSelfReference = string.Equals(remoteUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase);
        }

        // For self-references use the "loopback" named client (SSL validation bypassed).
        // Using the factory for both cases lets tests override it via IHttpClientFactory replacement.
        var clientName = isSelfReference ? "loopback" : string.Empty;
        if (isSelfReference)
            _logger.LogInformation("[RemoteController] Self-referencing proxy — using loopback client");
        var client = _httpClientFactory.CreateClient(clientName);

        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            _logger.LogInformation("[RemoteController] Added Bearer token to client (first 8 chars: {Token}...", 
                token.Substring(0, Math.Min(8, token.Length)));
        }
        else
        {
            _logger.LogWarning("[RemoteController] No token to add to client!");
        }
        return client;
    }
}
