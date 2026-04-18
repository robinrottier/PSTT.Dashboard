using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MqttDashboard.Data;
using MqttDashboard.Models;

namespace MqttDashboard.Services;

/// <summary>
/// Browser-side <see cref="IDataServer"/> implementation.
/// Connects to the SignalR hub via WebSocket; translates hub events into the
/// <see cref="IDataServer"/> contract used by <see cref="IDataCache"/>.
/// Implements <see cref="IDataServer.PublishAsync"/> by forwarding to the hub's
/// <c>PublishMessage</c> method.
/// </summary>
public class SignalRDataServer : IDataServer
{
    private HubConnection? _hubConnection;
    private readonly ILogger<SignalRDataServer> _logger;

    public event Action<string, object>? ValueUpdated;
    public event Action? Reconnected;
    public event Action<string, bool>? StatusChanged;

    public SignalRDataServer(ILogger<SignalRDataServer> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(string serverUrl = "")
    {
        _logger.LogInformation("Starting SignalR connection to {HubUrl}", serverUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
            .Build();

        _hubConnection.On<string, string, DateTime>("ReceiveMqttData", (topic, payload, _) =>
        {
            _logger.LogDebug("[SignalR] Received MQTT data: Topic={Topic}", topic);
            ValueUpdated?.Invoke(topic, payload);
        });

        _hubConnection.On<string, int>("MqttConnectionStatus", (state, attempts) =>
        {
            _logger.LogInformation("[SignalR] MQTT connection status: {State}, attempts: {Attempts}", state, attempts);
            var connected = state == "Connected";
            var status = connected ? $"MQTT {state}" : attempts > 0
                ? $"MQTT reconnecting (attempt {attempts})..."
                : $"MQTT {state}";
            StatusChanged?.Invoke(status, connected);
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "[SignalR] Connection lost. Reconnecting...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += _ =>
        {
            _logger.LogInformation("[SignalR] Reconnected.");
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogError(error, "[SignalR] Connection closed");
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("[SignalR] Connected. ConnectionId: {ConnectionId}", _hubConnection.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalR] Failed to start connection to {HubUrl}", serverUrl);
            throw;
        }
    }

    public async Task SubscribeAsync(string topic)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("[SignalR] Subscribing to topic: {Topic}", topic);
            try { await _hubConnection.InvokeAsync("SubscribeToTopic", topic); }
            catch (Exception ex) { _logger.LogError(ex, "[SignalR] Failed to subscribe to {Topic}", topic); throw; }
        }
        else
        {
            _logger.LogWarning("[SignalR] Cannot subscribe to {Topic}: hub is null", topic);
        }
    }

    public async Task UnsubscribeAsync(string topic)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("[SignalR] Unsubscribing from topic: {Topic}", topic);
            try { await _hubConnection.InvokeAsync("UnsubscribeFromTopic", topic); }
            catch (Exception ex) { _logger.LogError(ex, "[SignalR] Failed to unsubscribe from {Topic}", topic); throw; }
        }
        else
        {
            _logger.LogWarning("[SignalR] Cannot unsubscribe from {Topic}: hub is null", topic);
        }
    }

    public async Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("[SignalR] Publishing to {Topic}", topic);
            try { await _hubConnection.InvokeAsync("PublishMessage", topic, payload, retain, qos); }
            catch (Exception ex) { _logger.LogError(ex, "[SignalR] Failed to publish to {Topic}", topic); throw; }
        }
        else
        {
            _logger.LogWarning("[SignalR] Cannot publish to {Topic}: hub is null", topic);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            _logger.LogInformation("[SignalR] Disposing connection. State: {State}", _hubConnection.State);
            await _hubConnection.DisposeAsync();
        }
    }
}
