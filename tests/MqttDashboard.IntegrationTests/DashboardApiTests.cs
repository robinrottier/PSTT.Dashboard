using System.Net;
using System.Net.Http.Json;

namespace MqttDashboard.IntegrationTests;

/// <summary>REST API integration tests using <see cref="IntegrationWebApplicationFactory"/>.</summary>
public class DashboardApiTests : IClassFixture<IntegrationWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DashboardApiTests(IntegrationWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Health check ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_WithMqttDisconnected_Returns503()
    {
        // The fake MQTT service reports "disconnected", so the full health check must be 503.
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // Body should include per-check status
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("mqtt", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheck_IgnoreMqtt_Returns200()
    {
        // ?ignoreMqtt skips the MQTT check — the web server itself is healthy.
        var response = await _client.GetAsync("/healthz?ignoreMqtt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
    }

    // ── Dashboard API ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardList_Returns200()
    {
        var response = await _client.GetAsync("/api/dashboard/list");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDefaultDashboard_Returns200OrNotFound()
    {
        var response = await _client.GetAsync("/api/dashboard");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status: {response.StatusCode}");
    }
}
