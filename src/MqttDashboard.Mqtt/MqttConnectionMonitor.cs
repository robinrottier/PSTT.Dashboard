namespace MqttDashboard.Mqtt;

public enum MqttConnectionState { Disconnected, Connecting, Connected, Failed }

/// <summary>
/// Singleton that tracks the current MQTT broker connection state.
/// MqttClientService updates it; MqttDataHub reads it on client connect and broadcasts changes.
/// </summary>
public class MqttConnectionMonitor
{
    private volatile MqttConnectionState _state = MqttConnectionState.Disconnected;
    private volatile int _reconnectAttempts = 0;
    private string _broker = string.Empty;

    public MqttConnectionState State => _state;
    public int ReconnectAttempts => _reconnectAttempts;
    public string Broker => _broker;

    public event Func<MqttConnectionState, int, Task>? OnStateChanged;

    public void SetBroker(string broker) => _broker = broker;

    public async Task UpdateStateAsync(MqttConnectionState state, int reconnectAttempts = 0)
    {
        _state = state;
        _reconnectAttempts = reconnectAttempts;
        if (OnStateChanged != null)
            await OnStateChanged(state, reconnectAttempts);
    }
}
