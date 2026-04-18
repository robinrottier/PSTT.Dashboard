# Real-Time MQTT Data Layer - Setup Guide

## Overview
This solution implements a real-time data layer that connects your Blazor WebAssembly client to an MQTT broker through a SignalR hub on the server.

## Architecture
1. **MQTT Client Service** (Server) - Connects to MQTT broker and receives messages
2. **SignalR Hub** (Server) - Broadcasts MQTT messages to all connected clients
3. **SignalR Service** (Client) - Receives real-time updates from the server
4. **Blazor Component** (Client) - Displays the real-time data

## Configuration

### 1. MQTT Broker Settings
Edit `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\appsettings.json`:

```json
{
  "MqttSettings": {
    "Broker": "localhost",     // Your MQTT broker address
    "Port": "1883",            // MQTT broker port
    "Topic": "#"               // Topic to subscribe to (# = all topics)
  }
}
```

### 2. Production Settings
For production, create `appsettings.Production.json`:

```json
{
  "MqttSettings": {
    "Broker": "your-mqtt-broker.com",
    "Port": "1883",
    "Topic": "your/topic/path"
  }
}
```

## Testing the Solution

### Option 1: Using a Public MQTT Broker
Update `appsettings.json` to use a public test broker:

```json
{
  "MqttSettings": {
    "Broker": "test.mosquitto.org",
    "Port": "1883",
    "Topic": "test/#"
  }
}
```

### Option 2: Local MQTT Broker (Mosquitto)

1. **Install Mosquitto**:
   - Windows: Download from https://mosquitto.org/download/
   - macOS: `brew install mosquitto`
   - Linux: `sudo apt-get install mosquitto mosquitto-clients`

2. **Start Mosquitto**:
   ```bash
   mosquitto -v
   ```

3. **Publish test messages**:
   ```bash
   mosquitto_pub -h localhost -t "test/topic" -m "Hello from MQTT"
   ```

### Option 3: Using MQTT Explorer GUI
Download and install [MQTT Explorer](http://mqtt-explorer.com/) to easily send test messages.

## Running the Application

1. **Start the application**:
   ```bash
   dotnet run --project MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly
   ```

2. **Navigate to the MQTT Data page**:
   Open your browser to `https://localhost:5001/mqtt-data`

3. **Send test messages**:
   Use one of the testing methods above to publish MQTT messages

4. **View real-time updates**:
   The page will automatically display incoming MQTT messages in real-time

## Features

- ✅ Automatic reconnection if SignalR connection is lost
- ✅ Displays last 50 messages with timestamps
- ✅ Shows connection status
- ✅ Real-time updates without page refresh
- ✅ Works across multiple browser tabs/windows

## Customization

### Adding the Link to Navigation
To add the MQTT Data page to your navigation menu, update your navigation component to include:

```html
<a href="/mqtt-data">MQTT Data</a>
```

### Filtering Messages
To filter messages by topic, modify the `MqttData.razor` component:

```csharp
private void HandleDataReceived(MqttDataMessage message)
{
    // Filter by topic
    if (message.Topic.StartsWith("sensors/"))
    {
        _messages.Add(message);
        InvokeAsync(StateHasChanged);
    }
}
```

### Custom Data Processing
Modify `MqttClientService.cs` to parse and process MQTT payloads:

```csharp
_mqttClient.ApplicationMessageReceivedAsync += async e =>
{
    var topic = e.ApplicationMessage.Topic;
    var payload = e.ApplicationMessage.ConvertPayloadToString();
    
    // Parse JSON payload
    var data = JsonSerializer.Deserialize<YourDataType>(payload);
    
    // Process and broadcast
    await _hubContext.Clients.All.SendAsync("ReceiveMqttData", 
        topic, 
        data.ToString(), 
        DateTime.UtcNow, 
        stoppingToken);
};
```

## Troubleshooting

### Connection Issues
- Check that the MQTT broker is running and accessible
- Verify firewall settings allow connection to the MQTT port
- Check the server logs for connection errors

### No Messages Appearing
- Verify you're subscribed to the correct topic
- Check that messages are being published to the broker
- Review server logs for any errors

### SignalR Connection Failed
- Ensure the server is running
- Check browser console for errors
- Verify the hub URL in `MqttData.razor` matches your server URL

## Security Considerations

For production use, consider:

1. **MQTT Authentication**:
   ```csharp
   var options = new MqttClientOptionsBuilder()
       .WithTcpServer(mqttBroker, mqttPort)
       .WithCredentials("username", "password")
       .Build();
   ```

2. **SignalR Authentication**: Add authorization to the hub
3. **TLS/SSL**: Use secure connections for both MQTT and SignalR
4. **Data Validation**: Validate and sanitize all incoming MQTT data

## Files Created/Modified

### Created:
- `MqttDashboard.WebAppWasmOnly.Client\Models\MqttDataMessage.cs`
- `MqttDashboard.WebAppWasmOnly.Client\Services\SignalRService.cs`
- `MqttDashboard.WebAppWasmOnly.Client\Pages\MqttData.razor`
- `MqttDashboard.WebAppWasmOnly\Hubs\MqttDataHub.cs`
- `MqttDashboard.WebAppWasmOnly\Services\MqttClientService.cs`

### Modified:
- `MqttDashboard.WebAppWasmOnly\Program.cs` - Added SignalR and MQTT service
- `MqttDashboard.WebAppWasmOnly.Client\Program.cs` - Added SignalR client service
- `MqttDashboard.WebAppWasmOnly\appsettings.json` - Added MQTT configuration
