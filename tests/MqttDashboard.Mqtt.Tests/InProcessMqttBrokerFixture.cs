using MQTTnet.Server;

namespace MqttDashboard.Mqtt.Tests;

/// <summary>
/// xUnit class fixture that starts a real in-process MQTT broker for the duration of a test class.
/// The broker listens on a dynamically-assigned free TCP port to avoid collisions when tests
/// run in parallel.
/// </summary>
public sealed class InProcessMqttBrokerFixture : IAsyncLifetime
{
    private MqttServer? _server;

    /// <summary>The TCP port the broker is listening on.</summary>
    public int Port { get; private set; }

    public async Task InitializeAsync()
    {
        Port = FindFreePort();

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(Port)
            .Build();

        _server = new MqttServerFactory().CreateMqttServer(options);
        await _server.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }
    }

    private static int FindFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
