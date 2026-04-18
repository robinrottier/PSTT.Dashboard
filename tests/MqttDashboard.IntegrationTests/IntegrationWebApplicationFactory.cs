using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MqttDashboard.Mqtt;
using MqttDashboard.Server.Services;

namespace MqttDashboard.IntegrationTests;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for Tier A integration tests.
/// Replaces <see cref="MqttClientService"/> with <see cref="FakeMqttClientService"/>
/// so tests run without a real MQTT broker.
/// </summary>
public class IntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempDataDir =
        Path.Combine(Path.GetTempPath(), "mqttdashboard_tests_" + Guid.NewGuid().ToString("N"));

    /// <summary>The fake MQTT service — inject messages via <see cref="FakeMqttClientService.TriggerIncomingMessageAsync"/>.</summary>
    public FakeMqttClientService FakeMqttService =>
        Services.GetRequiredService<FakeMqttClientService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDataDir);

        builder.UseEnvironment("Test");

        builder.UseSetting("DiagramStorage:DataDirectory", _tempDataDir);
        // Point at a non-existent broker so even if the real service starts, it fails fast.
        builder.UseSetting("MqttSettings:Broker", "127.0.0.1");
        builder.UseSetting("MqttSettings:Port", "19999");

        builder.ConfigureServices(services =>
        {
            // Remove the real MqttClientService singleton so the fake takes its place.
            // The existing hosted-service factory (sp => sp.GetRequiredService<MqttClientService>())
            // will now resolve the fake, because we re-register MqttClientService → FakeMqttClientService.
            var realDesc = services.FirstOrDefault(d => d.ServiceType == typeof(MqttClientService)
                                                     && d.Lifetime == ServiceLifetime.Singleton
                                                     && d.ImplementationType == typeof(MqttClientService));
            if (realDesc != null) services.Remove(realDesc);

            services.AddSingleton<FakeMqttClientService>();
            // Let the existing hosted service factory resolve to the fake.
            services.AddSingleton<MqttClientService>(sp => sp.GetRequiredService<FakeMqttClientService>());
            // IMqttClientService was registered as sp => GetRequiredService<MqttClientService>(),
            // which now also resolves to the fake — no extra registration needed.
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        // Eagerly resolve ServerDataCache so MqttDataServer is constructed and its
        // OnMessagePublished / OnStateChanged handlers are wired before any test
        // injects fake MQTT messages via FakeMqttClientService.
        _ = host.Services.GetRequiredService<ServerDataCache>();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_tempDataDir))
            try { Directory.Delete(_tempDataDir, recursive: true); } catch { /* best-effort */ }
    }
}
