using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MqttDashboard.IntegrationTests;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for Tier B integration tests.
/// Configures the app to connect to an in-process MQTT broker started by
/// <see cref="InProcessMqttBrokerFixture"/>. The real <see cref="MqttDashboard.Server.Services.MqttClientService"/>
/// runs unmodified — tests publish via a separate MQTT client and observe SignalR output.
/// </summary>
public class MqttBrokerIntegrationFactory : WebApplicationFactory<Program>
{
    private readonly int _brokerPort;
    private readonly string _tempDataDir =
        Path.Combine(Path.GetTempPath(), "mqttdashboard_broker_tests_" + Guid.NewGuid().ToString("N"));

    public MqttBrokerIntegrationFactory(int brokerPort)
    {
        _brokerPort = brokerPort;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDataDir);

        builder.UseEnvironment("Test");

        builder.UseSetting("DiagramStorage:DataDirectory", _tempDataDir);
        builder.UseSetting("MqttSettings:Broker", "127.0.0.1");
        builder.UseSetting("MqttSettings:Port", _brokerPort.ToString());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_tempDataDir))
            try { Directory.Delete(_tempDataDir, recursive: true); } catch { /* best-effort */ }
    }
}
