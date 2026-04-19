using PSTT.Data;
using PSTT.Dashboard.Server.Services;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// Test double that replaces live MQTT with direct cache injection.
/// Exposes the same API surface as the old MqttClientService fake so existing
/// test call sites compile with minimal changes.
/// </summary>
public class FakeMqttClientService
{
    private readonly ServerDataCache _cache;

    public FakeMqttClientService(ServerDataCache cache)
    {
        _cache = cache;
    }

    /// <summary>Simulates an incoming MQTT message by publishing into the server cache.</summary>
    public Task TriggerIncomingMessageAsync(string topic, string payload)
        => _cache.PublishAsync(topic, payload);

    /// <summary>Seeds a cached value as if it had previously arrived from MQTT.</summary>
    public Task SeedLastKnownValueAsync(string topic, string value)
        => _cache.PublishAsync(topic, value);
}
