using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System.Collections.Concurrent;

namespace MqttDashboard.Mqtt;

public class MqttClientService : BackgroundService, IMqttClientService
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly MqttConnectionMonitor _connectionMonitor;
    private IMqttClient? _mqttClient;
    private MqttClientOptions? _mqttOptions;
    private readonly ConcurrentDictionary<string, bool> _subscribedTopics = new();
    private CancellationToken _stoppingToken;
    private int _isReconnecting = 0; // 0 = false, 1 = true (Interlocked flag)

    public event Func<string, string, DateTime, Task>? OnMessagePublished;

    public MqttClientService(
        ILogger<MqttClientService> logger,
        IConfiguration configuration,
        MqttTopicSubscriptionManager subscriptionManager,
        MqttConnectionMonitor connectionMonitor)
    {
        _logger = logger;
        _configuration = configuration;
        _subscriptionManager = subscriptionManager;
        _connectionMonitor = connectionMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        _logger.LogInformation("MQTT Client Service starting...");
        try
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var mqttBroker = _configuration["MqttSettings:Broker"] ?? "localhost";
            var mqttPort = int.Parse(_configuration["MqttSettings:Port"] ?? "1883");
            var mqttUsername = _configuration["MqttSettings:Username"];
            var mqttPassword = _configuration["MqttSettings:Password"];

            _logger.LogInformation("MQTT Configuration - Broker: {Broker}, Port: {Port}, Username: {Username}",
                mqttBroker, mqttPort, string.IsNullOrEmpty(mqttUsername) ? "<none>" : mqttUsername);

            _connectionMonitor.SetBroker($"{mqttBroker}:{mqttPort}");

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBroker, mqttPort)
                .WithClientId($"MqttDashboard_{Guid.NewGuid()}")
                .WithCleanSession();

            if (!string.IsNullOrEmpty(mqttUsername))
            {
                optionsBuilder.WithCredentials(mqttUsername, mqttPassword);
                _logger.LogInformation("MQTT client configured with username: {Username}", mqttUsername);
            }

            _mqttOptions = optionsBuilder.Build();

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT client disconnected. Reason: {Reason}, Was connected: {WasConnected}{ExMsg}",
                    e.Reason, e.ClientWasConnected,
                    e.Exception != null ? $", Exception: {e.Exception.Message}" : "");

                // Guard: only one reconnect loop at a time (MQTTnet v5 fires DisconnectedAsync
                // even on failed ConnectAsync attempts, which would spawn cascading loops)
                if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) == 0)
                {
                    try { await ReconnectWithBackoffAsync(); }
                    finally { Interlocked.Exchange(ref _isReconnecting, 0); }
                }
                else
                {
                    _logger.LogDebug("MQTT DisconnectedAsync fired but reconnect loop already running — skipping");
                }
            };

            _mqttClient.ConnectedAsync += async e =>
            {
                Interlocked.Exchange(ref _isReconnecting, 0); // allow future reconnects
                _logger.LogInformation("MQTT client connected. Session present: {SessionPresent}",
                    e.ConnectResult.IsSessionPresent);
                await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Connected);
                // Re-subscribe all tracked topics after reconnect
                foreach (var topic in _subscribedTopics.Keys)
                {
                    try
                    {
                        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                            .WithTopicFilter(f => f.WithTopic(topic))
                            .Build();
                        await _mqttClient!.SubscribeAsync(subscribeOptions);
                        _logger.LogDebug("Re-subscribed to MQTT topic after reconnect: {Topic}", topic);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to re-subscribe to topic {Topic} after reconnect", topic);
                    }
                }
            };

            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = SanitizePayload(e.ApplicationMessage.ConvertPayloadToString());
                var timestamp = DateTime.UtcNow;

                _logger.LogTrace("Received MQTT message on topic {Topic}: {Payload}", topic, payload);

                await HandleIncomingMessageAsync(topic, payload, timestamp, stoppingToken);
            };

            _logger.LogInformation("Attempting to connect to MQTT broker at {Broker}:{Port}...", mqttBroker, mqttPort);
            await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Connecting);
            var connectResult = await _mqttClient.ConnectAsync(_mqttOptions, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker. Result: {ResultCode}", connectResult.ResultCode);

            // Wire up subscription manager events
            _subscriptionManager.OnTopicSubscribeRequested += async topic =>
            {
                await SubscribeToMqttTopicAsync(topic);
            };

            _subscriptionManager.OnTopicUnsubscribeRequested += async topic =>
            {
                await UnsubscribeFromMqttTopicAsync(topic);
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MQTT Client Service stopping (cancellation requested)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MQTT client service");
            await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Failed);
        }
    }

    private async Task ReconnectWithBackoffAsync()
    {
        var delay = TimeSpan.FromSeconds(2);
        var attempt = 0;

        while (!_stoppingToken.IsCancellationRequested && _mqttClient != null)
        {
            attempt++;
            await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Connecting, attempt);
            _logger.LogInformation("MQTT reconnect attempt {Attempt}, waiting {Delay}s...", attempt, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, _stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await _mqttClient.ConnectAsync(_mqttOptions!, _stoppingToken);
                // ConnectedAsync handler will update state to Connected
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT reconnect attempt {Attempt} failed", attempt);
                // Exponential backoff capped at 60s
                delay = delay.TotalSeconds >= 60
                    ? TimeSpan.FromSeconds(60)
                    : TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
        }

        await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Disconnected);
    }

    private async Task SubscribeToMqttTopicAsync(string topic)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot subscribe to topic {Topic}: MQTT client not connected", topic);
            return;
        }

        if (_subscribedTopics.TryAdd(topic, true))
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic))
                .Build();
            var result = await _mqttClient.SubscribeAsync(subscribeOptions);
            var resultCode = result.Items.FirstOrDefault()?.ResultCode;
            _logger.LogDebug("Subscribed to MQTT topic: {Topic}. Result: {ResultCode}", topic, resultCode);
        }
        else
        {
            _logger.LogDebug("Already subscribed to MQTT topic: {Topic}", topic);
        }
    }

    private async Task UnsubscribeFromMqttTopicAsync(string topic)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot unsubscribe from topic {Topic}: MQTT client not connected", topic);
            return;
        }

        if (_subscribedTopics.TryRemove(topic, out _))
        {
            await _mqttClient.UnsubscribeAsync(topic);
            _logger.LogDebug("Unsubscribed from MQTT topic: {Topic}", topic);
        }
    }

    /// <summary>
    /// Processes an incoming MQTT message and notifies in-process subscribers.
    /// Hub fan-out is handled by <see cref="MqttDashboard.Server.Hubs.DataHub"/> via
    /// <see cref="ServerDataCache"/> callbacks — no direct SignalR dispatch here.
    /// Override in test doubles to inject fake messages without a real broker.
    /// </summary>
    protected virtual async Task HandleIncomingMessageAsync(
        string topic, string payload, DateTime timestamp, CancellationToken ct = default)
    {
        var inProcessHandler = OnMessagePublished;
        if (inProcessHandler != null)
        {
            try { await inProcessHandler.Invoke(topic, payload, timestamp); }
            catch (Exception ex) { _logger.LogError(ex, "Error notifying in-process subscriber for topic {Topic}", topic); }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }

    public async Task PublishMessageAsync(string topic, string payload, bool retain = false, int qos = 0)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger.LogWarning("Cannot publish to {Topic}: MQTT client is not connected", topic);
            return;
        }
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(retain)
                .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
                .Build();
            var result = await _mqttClient.PublishAsync(message);
            _logger.LogInformation("Published to {Topic}: {Payload} (ReasonCode: {ReasonCode})", topic, payload, result.ReasonCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// Strips characters that are invalid in XML/HTML text nodes (null bytes, lone surrogates,
    /// and other C0/C1 control chars except tab, LF, CR) so payloads are safe to render in the DOM.
    /// </summary>
    private static string SanitizePayload(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            // Allow tab (0x09), LF (0x0A), CR (0x0D), and everything >= 0x20 except surrogates/FFFE/FFFF
            if (ch == '\t' || ch == '\n' || ch == '\r') { sb.Append(ch); continue; }
            if (ch < 0x20) continue;                // C0 control chars
            if (ch >= 0xD800 && ch <= 0xDFFF) continue; // lone surrogates (invalid in XML)
            if (ch == 0xFFFE || ch == 0xFFFF) continue;  // non-characters
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
