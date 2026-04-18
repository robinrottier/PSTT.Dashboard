namespace MqttDashboard.Mqtt;

/// <summary>
/// Interface for the MQTT client service, exposing the functionality needed by
/// <see cref="Hubs.DataHub"/> and test doubles.
/// </summary>
public interface IMqttClientService
{
    /// <summary>Publishes a message to the MQTT broker.</summary>
    Task PublishMessageAsync(string topic, string payload, bool retain = false, int qos = 0);
}
