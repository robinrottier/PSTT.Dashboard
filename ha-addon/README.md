# Mqtt Dashboard — Home Assistant Add-on

Visual MQTT data dashboard. Connect to your MQTT broker and build node-based diagrams with live data.

## Installation

1. In Home Assistant go to **Settings → Add-ons → Add-on Store**
2. Click the three-dot menu (⋮) → **Repositories**
3. Add: `https://github.com/robinrottier/MqttDashboard`
4. Find **Mqtt Dashboard** in the store and click **Install**
5. Configure the add-on options (MQTT broker, credentials) then click **Start**
6. Open the Web UI on port 8080

## Configuration

| Option | Description | Default |
|---|---|---|
| `mqtt_broker` | MQTT broker hostname | `homeassistant` |
| `mqtt_port` | MQTT broker port | `1883` |
| `mqtt_username` | MQTT username | |
| `mqtt_password` | MQTT password | |

## Data persistence

Diagram files are stored in `/data/` which maps to the Home Assistant add-on data directory and persists across restarts and updates.
