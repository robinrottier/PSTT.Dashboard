# MQTT Dynamic Topic Subscriptions with Reference Counting

## Overview
The MQTT system has been refactored to support dynamic topic subscriptions from clients with intelligent reference counting. Multiple clients can subscribe to the same topic, but only a single MQTT broker subscription is created.

## Architecture

### Server-Side Components

#### 1. **MqttTopicSubscriptionManager** (New)
- **Location**: `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\Services\MqttTopicSubscriptionManager.cs`
- **Purpose**: Manages client topic subscriptions with reference counting
- **Key Features**:
  - Tracks which clients are subscribed to which topics
  - Reference counting: Multiple clients can subscribe to the same topic
  - Thread-safe operations using `SemaphoreSlim`
  - Events for topic subscribe/unsubscribe requests to MQTT broker
  - Automatic cleanup when clients disconnect

#### 2. **MqttClientService** (Updated)
- **Location**: `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\Services\MqttClientService.cs`
- **Changes**:
  - Removed static topic configuration from `appsettings.json`
  - Dynamic topic subscription/unsubscription based on client requests
  - Only sends messages to clients interested in specific topics
  - Maintains a dictionary of active MQTT subscriptions
  - Subscribes to MQTT broker only when first client requests a topic
  - Unsubscribes from MQTT broker when last client unsubscribes from a topic

#### 3. **MqttDataHub** (Updated)
- **Location**: `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\Hubs\MqttDataHub.cs`
- **New Methods**:
  - `SubscribeToTopic(string topic)`: Client requests to subscribe to a topic
  - `UnsubscribeFromTopic(string topic)`: Client requests to unsubscribe from a topic
  - `OnDisconnectedAsync()`: Automatically unsubscribes client from all topics on disconnect

### Client-Side Components

#### 1. **SignalRService** (Updated)
- **Location**: `MqttDashboard\Services\SignalRService.cs`
- **New Features**:
  - `SubscribeToTopicAsync(string topic)`: Request subscription to a topic
  - `UnsubscribeFromTopicAsync(string topic)`: Request unsubscription from a topic
  - Events for subscription/unsubscription confirmations
  - Existing message receiving functionality preserved

#### 2. **MqttData.razor** (Updated)
- **Location**: `MqttDashboard\Pages\MqttData.razor`
- **New UI Features**:
  - Text input for entering MQTT topics
  - Subscribe button (disabled when not connected)
  - List of active subscriptions
  - Unsubscribe buttons for each active subscription
  - Enter key support for quick subscription
  - Real-time subscription status updates

## How It Works

### Subscription Flow
1. Client enters a topic in the text box (e.g., `sensor/temperature`)
2. Client clicks "Subscribe" or presses Enter
3. `SignalRService.SubscribeToTopicAsync()` sends request to hub
4. `MqttDataHub.SubscribeToTopic()` receives the request
5. `MqttTopicSubscriptionManager` checks if this is a new topic:
   - **New topic**: Raises `OnTopicSubscribeRequested` event
   - **Existing topic**: Increments reference count
6. `MqttClientService` subscribes to MQTT broker (if new topic)
7. Client receives `SubscriptionConfirmed` callback
8. Client UI updates to show active subscription

### Message Distribution Flow
1. MQTT broker publishes message to a topic
2. `MqttClientService` receives the message
3. `MqttTopicSubscriptionManager.GetInterestedClients(topic)` returns list of ConnectionIds
4. SignalR sends message **only to interested clients**
5. Each interested client receives and displays the message

### Unsubscription Flow
1. Client clicks "Unsubscribe" button
2. `SignalRService.UnsubscribeFromTopicAsync()` sends request to hub
3. `MqttTopicSubscriptionManager` decrements reference count
4. If reference count reaches 0:
   - Raises `OnTopicUnsubscribeRequested` event
   - `MqttClientService` unsubscribes from MQTT broker
5. Client receives `UnsubscriptionConfirmed` callback
6. Client UI updates to remove subscription

### Automatic Cleanup
- When a client disconnects (browser closed, network issue, etc.)
- `MqttDataHub.OnDisconnectedAsync()` is called
- All subscriptions for that client are removed
- Reference counts are decremented
- MQTT broker subscriptions are removed if no more clients are interested

## Benefits

1. **Efficient Resource Usage**: Only subscribes to MQTT topics that clients actually need
2. **Reference Counting**: Multiple clients can subscribe to same topic without duplicate MQTT subscriptions
3. **Targeted Message Distribution**: Messages only sent to interested clients (reduces bandwidth)
4. **Automatic Cleanup**: No memory leaks from disconnected clients
5. **Thread-Safe**: All operations are properly synchronized
6. **Scalable**: Can handle many clients and topics efficiently

## Configuration

The MQTT broker connection settings remain in `appsettings.json`:
```json
{
  "MqttSettings": {
    "Broker": "localhost",
    "Port": "1883",
    "Username": "",
    "Password": ""
  }
}
```

**Note**: The `Topic` setting has been removed as topics are now dynamically managed.

## Example Usage

### Client subscribes to multiple topics:
1. Enter `sensor/temperature` → Subscribe
2. Enter `sensor/humidity` → Subscribe
3. Enter `device/+/status` → Subscribe (wildcard topics supported)

### Multiple clients subscribe to same topic:
- Client A subscribes to `sensor/temperature`
- Client B subscribes to `sensor/temperature`
- Only ONE MQTT subscription created
- Both clients receive messages
- When Client A unsubscribes, MQTT subscription remains (Client B still interested)
- When Client B unsubscribes, MQTT subscription is removed

## Technical Notes

- Uses `ConcurrentDictionary` for thread-safe topic tracking
- `SemaphoreSlim` ensures atomic subscription operations
- SignalR ConnectionIds used to identify clients
- Works with MQTT wildcard topics (`+`, `#`)
- Preserves all existing functionality while adding dynamic subscriptions
