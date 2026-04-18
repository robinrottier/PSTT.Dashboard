using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MqttDashboard.Mqtt;
using MqttDashboard.Server.Hubs;
using MqttDashboard.Server.Services;

namespace MqttDashboard.Server.Hubs;

public class DataHub : Hub
{
    private readonly IHubContext<DataHub> _hubContext;
    private readonly ILogger<DataHub> _logger;
    private readonly MqttConnectionMonitor _connectionMonitor;
    private readonly HubConnectionTracker _connectionTracker;
    private readonly IMqttClientService _mqttClientService;
    private readonly ServerDataCache _serverDataCache;
    private readonly HubSubscriptionStore _subscriptionStore;

    public DataHub(
        IHubContext<DataHub> hubContext,
        ILogger<DataHub> logger,
        MqttConnectionMonitor connectionMonitor,
        HubConnectionTracker connectionTracker,
        IMqttClientService mqttClientService,
        ServerDataCache serverDataCache,
        HubSubscriptionStore subscriptionStore)
    {
        _hubContext = hubContext;
        _logger = logger;
        _connectionMonitor = connectionMonitor;
        _connectionTracker = connectionTracker;
        _mqttClientService = mqttClientService;
        _serverDataCache = serverDataCache;
        _subscriptionStore = subscriptionStore;
    }

    public async Task SubscribeToTopic(string topic)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} requesting subscription to topic: {Topic}", connectionId, topic);

        if (_subscriptionStore.IsSubscribed(connectionId, topic))
        {
            _logger.LogWarning("Client {ConnectionId} already subscribed to topic: {Topic}", connectionId, topic);
        }
        else
        {
            var handle = _serverDataCache.Subscribe(topic, (t, v) =>
                _ = _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceiveMqttData", t, v?.ToString() ?? string.Empty, DateTime.UtcNow));

            _subscriptionStore.TryAdd(connectionId, topic, handle);
            _logger.LogInformation("Client {ConnectionId} successfully subscribed to topic: {Topic}", connectionId, topic);
        }

        await Clients.Caller.SendAsync("SubscriptionConfirmed", topic);
    }

    public async Task UnsubscribeFromTopic(string topic)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} requesting unsubscription from topic: {Topic}", connectionId, topic);

        if (_subscriptionStore.TryRemove(connectionId, topic, out var handle))
        {
            handle?.Dispose();
            _logger.LogInformation("Client {ConnectionId} successfully unsubscribed from topic: {Topic}", connectionId, topic);
            await Clients.Caller.SendAsync("UnsubscriptionConfirmed", topic);
        }
        else
        {
            _logger.LogWarning("Client {ConnectionId} was not subscribed to topic: {Topic}", connectionId, topic);
        }
    }

    public async Task PublishMessage(string topic, string payload, bool retain = false, int qos = 0)
    {
        _logger.LogInformation("Client {ConnectionId} publishing to topic: {Topic}", Context.ConnectionId, topic);
        await _mqttClientService.PublishMessageAsync(topic, payload, retain, qos);
    }

    public Task<Dictionary<string, string>> GetCurrentValuesForTopics(List<string> requestedFilters)
    {
        var result = new Dictionary<string, string>();
        foreach (var filter in requestedFilters)
        {
            foreach (var kvp in _serverDataCache.GetValuesByPattern(filter))
                result[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }
        return Task.FromResult(result);
    }

    public override async Task OnConnectedAsync()
    {
        _connectionTracker.Increment();
        _logger.LogInformation("Client {ConnectionId} connected to Data Hub", Context.ConnectionId);
        await Clients.Caller.SendAsync("MqttConnectionStatus",
            _connectionMonitor.State.ToString(),
            _connectionMonitor.ReconnectAttempts);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionTracker.Decrement();
        foreach (var handle in _subscriptionStore.RemoveAll(Context.ConnectionId))
            handle.Dispose();

        if (exception != null)
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with exception", Context.ConnectionId);
        else
            _logger.LogInformation("Client {ConnectionId} disconnected and unsubscribed from all topics", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
