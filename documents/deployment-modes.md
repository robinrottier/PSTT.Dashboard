# Deployment Modes

MqttDashboard supports several deployment configurations that differ in how editing is
controlled and in which Blazor rendering strategy is used. This document covers all options,
when to choose each, and the future direction for even finer-grained control.

---

## Access control modes

### 1. Default — single port, fully read-write (no config needed)

The simplest setup. Anyone who can reach the server can view **and** edit dashboards.
Suitable for personal use, local networks, or development.

```yaml
# Nothing extra required — just run the container.
```

### 2. Single port with admin authentication

Edit controls are visible, but only authenticated admins can save changes. Set
`Auth:AdminPasswordHash` to a bcrypt hash of the admin password. All other users
see the dashboard in read-only view mode (no edit controls shown).

```
Auth__AdminPasswordHash=<bcrypt hash>
```

To generate a hash: run the app and use the **Setup** page at `/setup`, or use
any bcrypt tool with cost factor 12.

### 3. Single port, fully read-only (`ReadOnly=true`)

The entire process is locked into read-only mode. The edit toggle, login/logout buttons,
and admin setup alert are hidden from the UI. All write API endpoints return `HTTP 403`.
No password is accepted or needed — anyone accessing the URL sees the live view only.

Best for: public displays, embedded monitors, or situations where the dashboard is
pre-configured and should never be changed via the UI.

```
ReadOnly=true
```

When `ReadOnly=true`:
- Edit mode toggle is hidden
- Login/logout buttons are hidden
- Admin setup prompt is hidden
- `POST /api/dashboard/*`, `POST /api/settings/*`, `POST /api/update/*` → 403
- `GET /api/auth/status` returns `{ isAdmin: false, authEnabled: false, readOnly: true }`

### 4. Dual-port — one process, two listener ports (`ReadOnlyPorts`)

A single process listens on **two ports**. One port is read-only (public); the other
is read-write (admin). All server-side singletons — the MQTT connection, data cache,
and SignalR hub — are shared between both ports, so there is no duplication of broker
connections or memory for the incoming data stream.

```
ASPNETCORE_URLS=http://+:8080;http://+:8081
ReadOnlyPorts=8080
ReadOnly=false
```

| Port | Audience | Behaviour |
|---|---|---|
| 8080 | Public / read-only | No edit UI; write APIs return 403 |
| 8081 | Admin / editable | Full functionality; optionally protected by auth |

Docker Compose example:

```yaml
services:
  mqttdashboard:
    image: ghcr.io/robinrottier/mqttdashboard:latest
    ports:
      - "8080:8080"   # public
      - "8081:8081"   # admin
    environment:
      - ASPNETCORE_URLS=http://+:8080;http://+:8081
      - ReadOnlyPorts=8080
      - ReadOnly=false
      - MqttSettings__Broker=your-mqtt-broker
      - DiagramStorage__DataDirectory=/app/data
    volumes:
      - ./data:/app/data
    restart: unless-stopped
```

`ReadOnlyPorts` accepts a comma-separated list for multiple read-only ports:
```
ReadOnlyPorts=8080,8082
```

**How it works internally:** `HttpContext.Connection.LocalPort` tells each request which
port it arrived on. The read-only check runs before auth and before any write logic.
Admin auth cookies are origin-scoped by the browser (scheme + host + port), so a login
cookie from port 8081 is never sent to port 8080 — the isolation is enforced by the browser.

**Dashboard layout coherency:** both ports share the same in-process state. A dashboard
save on port 8081 is immediately visible to clients on port 8080 without any reload — they
are served from the same in-memory diagram state.

### 5. Dual-process — two separate containers (alternative to dual-port)

Run two instances of the image with different env vars, sharing the same data volume:

```yaml
services:
  mqttdashboard-admin:
    image: ghcr.io/robinrottier/mqttdashboard:latest
    ports: ["8081:8080"]
    environment:
      - ReadOnly=false
      - MqttSettings__Broker=your-mqtt-broker
      - DiagramStorage__DataDirectory=/app/data
    volumes: ["./data:/app/data"]

  mqttdashboard-public:
    image: ghcr.io/robinrottier/mqttdashboard:latest
    ports: ["8080:8080"]
    environment:
      - ReadOnly=true
      - MqttSettings__Broker=your-mqtt-broker
      - DiagramStorage__DataDirectory=/app/data
    volumes: ["./data:/app/data"]   # ← same volume
```

Pros: complete process isolation; a crash in the admin instance doesn't affect the public one.  
Cons: two MQTT broker connections; two copies of the data cache in memory; dashboard layout
changes saved by the admin instance are NOT automatically reflected in the read-only instance's
in-memory state — clients on the read-only port would need to reload the page to see layout updates.
(MQTT live data updates still work independently on each instance since both subscribe to the broker.)

---

## Render mode options

### WebApp image — configurable via `RenderMode`

The standard Docker image (`MqttDashboard.WebApp`) supports three render modes selectable at
runtime via the `RenderMode` environment variable:

| `RenderMode` value | Blazor strategy | WASM bundle delivered? | Notes |
|---|---|---|---|
| `Auto` *(default)* | Interactive Auto | Yes, for capable browsers | Best experience; server-side fallback for old browsers |
| `WebAssembly` | Interactive WebAssembly | Yes, always | Pure client-side after first load |
| `Server` | Interactive Server | No | No WASM download; all rendering server-side; same image, different config |

Example:
```
RenderMode=Server
```

Note: in `Server` mode the WASM bundle files are present inside the Docker image (they are built
unconditionally) but are never requested or sent to clients.

### WebAppServerOnly — dedicated Blazor Server project

`MqttDashboard.WebAppServerOnly` is a dedicated Blazor Server project with no WASM dependency.
Its Docker image is **smaller** (no WASM bundle files), builds **faster** (no `wasm-tools` workload
required), and uses `InteractiveServer` render mode directly rather than `InteractiveAuto` falling
through to Server.

Prefer `WebAppServerOnly` when:
- Deploying to Raspberry Pi or other resource-constrained devices
- You want the smallest possible image and fastest build
- You are confident that WASM is never desired for this deployment

Use `RenderMode=Server` on the WebApp image when:
- You want a **single Docker image** that can toggle between WASM and Server mode via config
- You are experimenting with render modes without a separate build

---

## Future: compile-time read-only (planned, not yet implemented)

The current `ReadOnly=true` approach is runtime — all editing code is compiled into the binary
but disabled at runtime. A future compile-time option would produce a smaller binary / WASM bundle
with editing code stripped entirely.

This becomes worthwhile as part of a broader project restructure:

```
MqttDashboard.Core          ← pure C#, no Blazor (models, services, MQTT logic)
MqttDashboard.View          ← Blazor components for viewing only (no editing UI)
MqttDashboard.Client        ← adds editing UI on top of .View
```

A read-only host would reference only `.Core` + `.View`, resulting in a genuinely minimal
deployment with no editing surface at all — neither at runtime nor in the binary. This is
particularly valuable for Blazor WASM deployments where the bundle is downloaded by every client.

The `.Core` project (pure C#, no Blazor) is distinct from `.View` (Blazor view components).
Both differ from the current `MqttDashboard.Client` which bundles everything together.
This split is not yet scheduled but is a natural evolution of the architecture.
