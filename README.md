# MqttDashboard

A live, node-based dashboard editor driven by real-time MQTT data.

Draw diagrams that show how things are connected in your system — and watch the values update in real time as MQTT messages arrive. Typical use cases include:

- **Energy flow** — solar panels → battery → inverter → grid, with live power and state-of-charge values on each node
- **Device state** — home automation sensors, switches, and actuators displayed as a connected graph
- **Process monitoring** — industrial or IoT pipelines with live readings at each stage

Dashboards are fully editable in the browser. Multiple dashboard files can be saved and opened without restarting.

---

## Features

- Node-based canvas — drag, connect, and label nodes
- Live data — MQTT values displayed directly on nodes, updated via SignalR push
- Edit / view modes — share a read-only view with no edit controls
- Optional admin authentication — password-protect editing
- **Read-only deployment mode** — `ReadOnly=true` disables all edit UI and blocks all write APIs; ideal for public or shared displays
- **Dual-port mode** — single process serving a read-only public port and an editable admin port simultaneously, sharing the MQTT connection and data cache
- Theme — light, dark, or system default
- Three render mode flavours:
  - **Auto** (default) — WASM for capable browsers, Blazor Server fallback
  - **WebAssembly** — WASM always
  - **Server** — Blazor Server only (same WebApp image, no WASM download; set `RenderMode=Server`)
  - **WebAppServerOnly** — dedicated Blazor Server project; smaller Docker image; recommended for Raspberry Pi

---

## Technical overview

| Layer | Technology |
|---|---|
| UI framework | [Blazor Web App](https://learn.microsoft.com/aspnet/core/blazor/) (.NET 10, InteractiveAuto render mode) |
| Component library | [MudBlazor](https://mudblazor.com/) |
| Diagram canvas | [Blazor.Diagrams](https://blazor-diagrams.zouri.fr/) |
| Real-time push | ASP.NET Core SignalR |
| MQTT client | [MQTTnet](https://github.com/dotnet/MQTTnet) |
| Versioning | [MinVer](https://github.com/adamralph/minver) (from git tags) |

The server subscribes to MQTT topics and forwards incoming messages to connected browser clients over SignalR. Dashboards are persisted as JSON files.

---

## Deployment

### Docker (recommended)

The pre-built multi-arch image (linux/amd64 and linux/arm64) is published to the GitHub Container Registry:

```
ghcr.io/robinrottier/mqttdashboard:latest
```

**Quick start with Docker Compose** — copy the sample below to a `docker-compose.yml` file and edit the MQTT settings:

```yaml
services:
  mqttdashboard:
    image: ghcr.io/robinrottier/mqttdashboard:latest
    ports:
      - "8080:8080"
    volumes:
      - ./data:/app/data
    environment:
      - MqttSettings__Broker=your-mqtt-broker-host
      - MqttSettings__Port=1883
      - MqttSettings__Username=
      - MqttSettings__Password=
      - DiagramStorage__DataDirectory=/app/data
      - AllowedPathBase=
    restart: unless-stopped
```

```bash
docker compose up -d
```

Open <http://localhost:8080>.

**Pull and run with the Docker CLI** (no compose file):

```bash
docker pull ghcr.io/robinrottier/mqttdashboard:latest

docker run -d \
  --name mqttdashboard \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -e MqttSettings__Broker=your-mqtt-broker-host \
  -e MqttSettings__Port=1883 \
  -e DiagramStorage__DataDirectory=/app/data \
  --restart unless-stopped \
  ghcr.io/robinrottier/mqttdashboard:latest
```

### Raspberry Pi

The Docker image is built for both `linux/amd64` and `linux/arm64`, so it runs natively on a Raspberry Pi 3/4/5 (64-bit OS).

```bash
docker pull ghcr.io/robinrottier/mqttdashboard:latest
docker run -d --name mqttdashboard -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  -e MqttSettings__Broker=your-mqtt-broker-host \
  --restart unless-stopped \
  ghcr.io/robinrottier/mqttdashboard:latest
```

**Alternatively, install the self-contained binary:**

1. Download `mqttdashboard-linux-arm64.zip` from the [latest release](../../releases/latest).
2. Extract and make executable:

   ```bash
   sudo mkdir -p /opt/mqttdashboard
   sudo unzip mqttdashboard-linux-arm64.zip -d /opt/mqttdashboard
   sudo chmod +x /opt/mqttdashboard/MqttDashboard.WebApp
   ```

3. Copy `appsettings.sample.json` to `appsettings.json` and edit your MQTT broker settings.

4. Create a systemd service so it starts automatically:

   ```ini
   # /etc/systemd/system/mqttdashboard.service
   [Unit]
   Description=MqttDashboard
   After=network.target

   [Service]
   Type=simple
   WorkingDirectory=/opt/mqttdashboard
   ExecStart=/opt/mqttdashboard/MqttDashboard.WebApp
   Restart=on-failure
   Environment=ASPNETCORE_URLS=http://+:8080
   Environment=ASPNETCORE_ENVIRONMENT=Production

   [Install]
   WantedBy=multi-user.target
   ```

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now mqttdashboard
   ```

Open <http://raspberry-pi-hostname:8080>.

### Windows / Linux / macOS — self-contained binary

Download the zip for your platform from the [latest release](../../releases/latest):

| File | Platform |
|---|---|
| `mqttdashboard-win-x64.zip` | Windows 64-bit |
| `mqttdashboard-linux-x64.zip` | Linux 64-bit |
| `mqttdashboard-linux-arm64.zip` | Linux ARM64 (Raspberry Pi, etc.) |

Extract, copy `appsettings.sample.json` to `appsettings.json`, edit your MQTT settings, then run:

```bash
# Linux / macOS
chmod +x ./MqttDashboard.WebApp
./MqttDashboard.WebApp

# Windows
MqttDashboard.WebApp.exe
```

### Home Assistant add-on

See [`ha-addon/`](ha-addon/) for the Home Assistant add-on configuration. The add-on uses the same Docker image.

---

## Behind a reverse proxy (nginx subpath)

To run the dashboard at a subpath (e.g. `https://myserver.com/dashboard`), set `AllowedPathBase` to the subpath **without** leading or trailing slashes:

```
AllowedPathBase=dashboard
```

The app reads the `X-Forwarded-Prefix` header sent by nginx. Only the exact value configured in `AllowedPathBase` is accepted; arbitrary client values are ignored.

Minimal nginx location block:

```nginx
location /dashboard/ {
    proxy_pass         http://localhost:8080/;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_set_header   X-Forwarded-Prefix /dashboard;
}
```

The app is also fully accessible at `http://localhost:8080` simultaneously (without the subpath).

---

## Configuration reference

All settings can be supplied as environment variables (using `__` as the section separator) or in `appsettings.json`.

| Setting | Environment variable | Description | Default |
|---|---|---|---|
| MQTT broker host | `MqttSettings__Broker` | Hostname or IP of your MQTT broker | `localhost` |
| MQTT port | `MqttSettings__Port` | Broker port | `1883` |
| MQTT username | `MqttSettings__Username` | Leave empty if not required | — |
| MQTT password | `MqttSettings__Password` | Leave empty if not required | — |
| Data directory | `DiagramStorage__DataDirectory` | Path where dashboard JSON files are stored | `./data` |
| Reverse proxy subpath | `AllowedPathBase` | Accepted subpath from `X-Forwarded-Prefix` | *(empty — root)* |
| Admin password hash | `Auth__AdminPasswordHash` | bcrypt hash of the admin password. Leave empty to disable authentication | — |
| Read-only mode | `ReadOnly` | `true` — disable all edit UI and block all write APIs | `false` |
| Read-only ports | `ReadOnlyPorts` | Comma-separated port numbers served as read-only (e.g. `8080`). Use with `ASPNETCORE_URLS` to enable dual-port mode. | *(empty)* |
| Render mode | `RenderMode` | `Auto` \| `WebAssembly` \| `Server` | `Auto` |

See [Deployment modes](documents/deployment-modes.md) for detailed guidance on all access-control and render-mode options.

---

## Building from source

```bash
# Prerequisites: .NET 10 SDK
dotnet workload install wasm-tools   # one-time

dotnet build MqttDashboard.slnx
dotnet test  MqttDashboard.slnx

# Run with Docker Compose (builds from source)
docker compose up --build
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for more details.

---

## License

[MIT](LICENSE.txt)
