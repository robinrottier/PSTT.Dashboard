using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PSTT.Dashboard.Server.Services;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for Tier A integration tests.
/// Removes <see cref="MqttHostedService"/> so no MQTT broker connection is attempted.
/// Use <see cref="FakeMqttService"/> to inject values directly into the server cache.
/// </summary>
public class IntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempDataDir =
        Path.Combine(Path.GetTempPath(), "dashboard_tests_" + Guid.NewGuid().ToString("N"));

    private FakeMqttClientService? _fakeMqttService;

    /// <summary>Inject messages via <see cref="FakeMqttClientService.TriggerIncomingMessageAsync"/>.</summary>
    public FakeMqttClientService FakeMqttService
        => _fakeMqttService ??= new FakeMqttClientService(Services.GetRequiredService<ServerDataCache>());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDataDir);

        builder.UseEnvironment("Test");

        builder.UseSetting("DiagramStorage:DataDirectory", _tempDataDir);
        builder.UseSetting("MqttSettings:Broker", "127.0.0.1");
        builder.UseSetting("MqttSettings:Port", "19999");

        builder.ConfigureServices(services =>
        {
            // Remove MqttHostedService so no real MQTT connection is attempted in tests.
            var hostedDesc = services.FirstOrDefault(d => d.ImplementationType == typeof(MqttHostedService));
            if (hostedDesc != null) services.Remove(hostedDesc);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_tempDataDir))
            try { Directory.Delete(_tempDataDir, recursive: true); } catch { /* best-effort */ }
    }
}
