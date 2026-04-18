# MQTT Message Flow - Debugging Guide

## Problem Fixed

**Issue:** MQTT subscriptions work but messages don't reach the client UI.

**Root Cause:** The `GetInterestedClients()` method was doing exact topic matching, which doesn't support MQTT wildcards (`+` for single-level, `#` for multi-level).

### Example of the Problem

- Client subscribes to: `sensor/+/temperature` (with wildcard)
- MQTT message arrives on: `sensor/bedroom/temperature` (specific topic)
- Old code: No match found (exact string comparison)
- **New code: Match found (wildcard matching)** ✅

## Changes Made

### 1. Fixed Topic Matching (MqttTopicSubscriptionManager.cs)

**Added MQTT wildcard support:**
```csharp
public HashSet<string> GetInterestedClients(string topic)
{
    var interestedClients = new HashSet<string>();
    
    foreach (var subscription in _subscriptions.Values)
    {
        if (TopicMatches(subscription.Topic, topic))
        {
            foreach (var client in subscription.GetClients())
            {
                interestedClients.Add(client);
            }
        }
    }
    
    return interestedClients;
}

private bool TopicMatches(string filter, string topic)
{
    // Handles:
    // - Exact matches: "sensor/temp" == "sensor/temp"
    // - Single-level wildcard: "sensor/+/temp" matches "sensor/bedroom/temp"
    // - Multi-level wildcard: "sensor/#" matches "sensor/bedroom/temp/current"
}
```

### 2. Enhanced Logging

**Server-side (MqttClientService):**
- Logs when MQTT message received
- Logs number of interested clients found
- Logs successful/failed message sending
- Warns when no clients are interested

**Server-side (MqttDataHub):**
- Logs client connections/disconnections
- Logs subscription requests
- Logs successful/failed subscriptions

**Client-side (SignalRService):**
- Console logs for connection status
- Console logs for subscription confirmations
- Console logs when messages received
- Console logs for reconnection events

## How to Debug

### 1. Check Server Logs

Look for these log entries in the server console:

```
[Connected to MQTT broker at localhost:1883]
[Client ABC123 requesting subscription to topic: sensor/+/temp]
[Client ABC123 successfully subscribed to topic: sensor/+/temp]
[Subscribed to MQTT topic: sensor/+/temp]
[Received MQTT message on topic sensor/bedroom/temp: 23.5]
[Found 1 interested clients for topic sensor/bedroom/temp]
[Sent MQTT message to 1 interested clients for topic sensor/bedroom/temp]
```

### 2. Check Browser Console (F12)

Look for these console messages:

```
[SignalR] Starting connection to https://localhost:5001/mqttdatahub
[SignalR] Connected. ConnectionId: ABC123
[SignalR] Subscribing to topic: sensor/+/temp
[SignalR] Subscription confirmed: sensor/+/temp
[SignalR] Received MQTT data: Topic=sensor/bedroom/temp, Payload=23.5
```

### 3. Verify MQTT Message Flow

The complete flow should be:

1. **Client → SignalR Hub**
   - Client calls `SubscribeToTopicAsync("sensor/+/temp")`
   - Console: `[SignalR] Subscribing to topic: sensor/+/temp`

2. **SignalR Hub → Subscription Manager**
   - Hub receives request
   - Server log: `Client ABC123 requesting subscription to topic: sensor/+/temp`
   - Subscription manager adds client to topic

3. **Subscription Manager → MQTT Client**
   - First subscription to topic triggers MQTT broker subscription
   - Server log: `Subscribed to MQTT topic: sensor/+/temp`

4. **MQTT Broker → MQTT Client**
   - Message published to `sensor/bedroom/temp`
   - Server log: `Received MQTT message on topic sensor/bedroom/temp: 23.5`

5. **MQTT Client → Subscription Manager**
   - Get interested clients for `sensor/bedroom/temp`
   - Wildcard matching: `sensor/+/temp` matches `sensor/bedroom/temp`
   - Server log: `Found 1 interested clients for topic sensor/bedroom/temp`

6. **MQTT Client → SignalR Hub → Client**
   - Send to interested clients via SignalR
   - Server log: `Sent MQTT message to 1 interested clients`
   - Browser console: `[SignalR] Received MQTT data: Topic=sensor/bedroom/temp, Payload=23.5`

7. **Client UI Update**
   - `OnDataReceived` event fires
   - `ApplicationState.AddMessage()` called
   - `NotifyStateChangedAsync()` called
   - `InvokeAsync(StateHasChanged)` updates UI

## Troubleshooting

### Messages not appearing in UI

**Check 1: Is SignalR connected?**
```
Browser Console → Look for: "[SignalR] Connected. ConnectionId: ..."
```

**Check 2: Is subscription confirmed?**
```
Browser Console → Look for: "[SignalR] Subscription confirmed: ..."
Server Log → Look for: "Client ... successfully subscribed to topic: ..."
```

**Check 3: Is MQTT receiving messages?**
```
Server Log → Look for: "Received MQTT message on topic ..."
```

**Check 4: Are clients being matched?**
```
Server Log → Look for: "Found X interested clients for topic ..."
If X = 0, topic matching is failing
```

**Check 5: Is SignalR sending messages?**
```
Server Log → Look for: "Sent MQTT message to X interested clients"
Browser Console → Look for: "[SignalR] Received MQTT data: ..."
```

**Check 6: Is UI updating?**
```
- Check if OnDataReceived event has subscribers
- Check if ApplicationState.AddMessage() is called
- Check if StateHasChanged is being invoked on UI thread
```

### Common Issues

**Issue: "No interested clients found"**
- **Cause:** Topic wildcard matching not working
- **Solution:** Verify `TopicMatches()` logic

**Issue: "SignalR not connected"**
- **Cause:** Hub URL incorrect or SignalR not started
- **Solution:** Check `NavigationManager.ToAbsoluteUri("/mqttdatahub")`

**Issue: "Messages received but UI not updating"**
- **Cause:** StateHasChanged called on wrong thread
- **Solution:** Use `InvokeAsync(StateHasChanged)` in HandleStateChanged()

**Issue: "Subscription confirmed but no MQTT subscription"**
- **Cause:** Event handler not wired up
- **Solution:** Check `_subscriptionManager.OnTopicSubscribeRequested += ...`

## MQTT Topic Matching Rules

### Exact Match
```
Subscription: "sensor/temp"
Matches:      "sensor/temp" ✅
Doesn't match: "sensor/temperature" ❌
```

### Single-level Wildcard (+)
```
Subscription: "sensor/+/temp"
Matches:      "sensor/bedroom/temp" ✅
Matches:      "sensor/kitchen/temp" ✅
Doesn't match: "sensor/bedroom/bathroom/temp" ❌
```

### Multi-level Wildcard (#)
```
Subscription: "sensor/#"
Matches:      "sensor/temp" ✅
Matches:      "sensor/bedroom/temp" ✅
Matches:      "sensor/bedroom/bathroom/temp" ✅
Doesn't match: "device/sensor/temp" ❌
```

### Special Cases
```
Subscription: "#"
Matches:      Everything ✅

Subscription: "sensor/+/#"
Matches:      "sensor/bedroom/temp" ✅
Matches:      "sensor/bedroom/temp/current" ✅
Doesn't match: "sensor/temp" ❌ (+ requires one level)
```

## Testing

### Test with exact topic
```
Subscribe: "test/message"
Publish to: "test/message"
Expected: Message received ✅
```

### Test with single-level wildcard
```
Subscribe: "test/+/message"
Publish to: "test/123/message"
Expected: Message received ✅
```

### Test with multi-level wildcard
```
Subscribe: "test/#"
Publish to: "test/a/b/c/message"
Expected: Message received ✅
```

### Test no match
```
Subscribe: "test/specific"
Publish to: "test/other"
Expected: Message NOT received ✅
Server log: "No interested clients found" ✅
```
