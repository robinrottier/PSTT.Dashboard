# Configuration Reference

All configuration settings for PSTT Dashboard, with defaults and usage notes.

---

## How configuration is loaded

Settings are layered in the following priority order (higher overrides lower):

1. **`appsettings.json`** — compiled-in defaults; do not edit in production
2. **`appsettings.{Environment}.json`** — environment-specific overrides (e.g. `appsettings.Development.json`)
3. **`appsettings.user.json`** — runtime-writable settings; stored in the data directory and auto-saved by the UI
4. **Environment variables** — override any file-based setting; use `__` as the section separator (e.g. `MqttSettings__Broker`)
5. **Command-line arguments**

> **`appsettings.user.json`** is located inside `DiagramStorage:DataDirectory` (e.g. `/app/data/appsettings.user.json`).
> The UI writes to this file when you change settings from the admin interface.
> It is loaded at server startup; changes made directly to the file while the server is running are picked up on the next restart.

---

## Complete sample file

The settings below represent all supported keys with their defaults. Copy into `appsettings.json`
(or supply as environment variables) and adjust as needed.

```json
{
  // ── MQTT connection ──────────────────────────────────────────────────────
  "MqttSettings": {
    "Broker": "localhost",        // MQTT broker hostname or IP
    "Port":   "1883",             // MQTT broker port (string or int accepted)
    "Username": "",               // Leave empty if broker has no auth
    "Password": ""
  },

  // ── Data storage ─────────────────────────────────────────────────────────
  "DiagramStorage": {
    // Path to the directory where dashboards and settings are stored.
    // Relative paths resolve from the application ContentRootPath.
    // In Docker, map a named volume to this path.
    "DataDirectory": "/app/data"
  },

  // ── ASP.NET Data Protection ───────────────────────────────────────────────
  "DataProtection": {
    // Directory where ASP.NET Data Protection keys are stored.
    // Required for auth cookies to survive a container restart.
    // Must be on a persistent volume in Docker.
    "KeysDirectory": "/app/data/keys"
  },

  // ── PSTT data cache TCP server ────────────────────────────────────────────
  "CacheSettings": {
    // Set to a non-zero port to expose the internal MQTT data cache over TCP.
    // External tools (e.g. pstt-sub CLI) connect here to subscribe to topics.
    // 0 = disabled (default).
    "TcpPort": 0
  },

  // ── Application behaviour ─────────────────────────────────────────────────
  "App": {
    // Maximum number of messages retained in the in-memory Log widget history.
    // Applies per Log widget. Lower this if memory is constrained.
    "MaxMessageHistory": 500,

    // When true, the dashboard is automatically saved when the user exits edit mode.
    // Can also be toggled from the Options menu in the UI; change is persisted to
    // appsettings.user.json.
    "AutoSaveOnExit": false,

    // Optional: list of alternate dashboard instances to show as link buttons
    // in the About box. Useful when running a dual-port (read-only + admin) setup
    // or two separate containers sharing the same data volume.
    "AlternateInstances": [
      { "Label": "Read-Only View",  "Url": "http://your-host:8080" },
      { "Label": "Admin Interface", "Url": "http://your-host:8081" }
    ]
  },

  // ── Admin authentication ──────────────────────────────────────────────────
  "Auth": {
    // bcrypt hash of the admin password. Leave empty to disable authentication
    // (anyone can edit). Generate a hash via the /setup page in the UI,
    // or with any standard bcrypt tool at cost factor 12.
    // Written to appsettings.user.json by the setup wizard.
    "AdminPasswordHash": ""
  },

  // ── Startup dashboard ─────────────────────────────────────────────────────
  "Startup": {
    // "LastUsed"      — reopen the dashboard that was open when the server last stopped (default)
    // "SpecificFile"  — always open the file named by Startup:Dashboard
    // "None"          — start with an empty canvas
    "Mode": "LastUsed",
    // Used only when Mode = "SpecificFile". File stem without extension (e.g. "Default").
    "Dashboard": ""
  },

  // ── Read-only access control ──────────────────────────────────────────────
  // See documents/deployment-modes.md for full details and Docker examples.

  // When true, the entire process is permanently read-only.
  // Edit controls and login buttons are hidden; write API endpoints return 403.
  "ReadOnly": false,

  // Comma-separated list of ports that should be treated as read-only.
  // Requests arriving on these ports cannot save or change settings.
  // Requires the server to listen on multiple ports (set ASPNETCORE_URLS).
  // Example: "8080" or "8080,8082"
  "ReadOnlyPorts": "",

  // ── Render mode (WebApp host only — not WebAppServerOnly) ─────────────────
  // "Auto"         — Interactive Auto (WASM for capable browsers; server fallback) — default
  // "WebAssembly"  — always deliver WASM bundle; pure client-side after first load
  // "Server"       — Blazor Server (no WASM download); useful for constrained devices
  "RenderMode": "Auto",

  // ── Reverse proxy / path base ─────────────────────────────────────────────
  // If the app is served under a sub-path (e.g. https://host/dashboard/),
  // set this to the path prefix without a trailing slash (e.g. "/dashboard").
  // Leave empty when served from the root.
  "AllowedPathBase": "",

  // ── Remote access API ─────────────────────────────────────────────────────
  "RemoteAccess": {
    // API token for machine-to-machine access (e.g. pstt-sub CLI, remote dashboards).
    // Generated and rotated via the Admin → Remote Access Token UI.
    // Written to appsettings.user.json — do not set by hand.
    "ApiToken": ""
  },

  // ── Remote dashboard repositories ─────────────────────────────────────────
  // Configured via the Admin → Remote Repositories UI; written to appsettings.user.json.
  // Each entry is another PSTT Dashboard instance you can pull dashboards from.
  "RemoteRepositories": [
    {
      "Name": "Production server",
      "Url": "https://your-other-instance",
      "ApiToken": "token-from-that-instance"
    }
  ],

  // ── Update agent (Docker / host-managed deployments) ─────────────────────
  // Only relevant when using the "Upgrade Now" feature in the About box.
  // See appsettings.Development.UpdateAgent.example.json for full documentation.
  "UpdateAgent": {
    // URL of the host-local update agent
    "Url": "http://host.docker.internal:8080/update",
    // Pre-configured token (client can also supply one interactively)
    "Token": "",
    // Docker Compose service name to update
    "Service": "psttdashboard",
    // Compose file path (defaults to docker-compose.yml in the working directory)
    "ComposeFile": "docker-compose.yml",
    // Alternative: trigger a Watchtower one-shot container instead of compose pull/up
    "WatchtowerContainer": "",
    // Working directory for docker compose commands
    "Workdir": "",
    // Allow the UI client to supply the service name in the request body.
    // Leave false for security (service name is fixed server-side).
    "AllowClientSpecifiedService": false
  },

  // ── ASP.NET logging ───────────────────────────────────────────────────────
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },

  // ── Serilog structured logging (overrides Logging: section when present) ──
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    }
  },

  // ── ASP.NET host settings ─────────────────────────────────────────────────
  "AllowedHosts": "*"
}
```

---

## Section-by-section reference

### `MqttSettings`

| Key | Default | Description |
|-----|---------|-------------|
| `Broker` | `"localhost"` | MQTT broker hostname or IP address |
| `Port` | `"1883"` | TCP port of the MQTT broker |
| `Username` | `""` | MQTT username; leave empty if the broker has no auth |
| `Password` | `""` | MQTT password |

Environment variable equivalents:
```
MqttSettings__Broker=mqtt-broker
MqttSettings__Port=1883
MqttSettings__Username=myuser
MqttSettings__Password=secret
```

---

### `DiagramStorage`

| Key | Default | Description |
|-----|---------|-------------|
| `DataDirectory` | `""` | Path to dashboard storage. Empty = current working directory. In Docker use `/app/data`. |

The data directory contains:
- `*.json` — saved dashboard files
- `appsettings.user.json` — runtime-persisted settings (auto-maintained by the app)
- `keys/` — ASP.NET Data Protection keys (if `DataProtection:KeysDirectory` points here)

---

### `DataProtection`

| Key | Default | Description |
|-----|---------|-------------|
| `KeysDirectory` | `""` | Directory for ASP.NET Data Protection key ring. Must be on a persistent volume in Docker so auth cookies survive container restarts. |

---

### `CacheSettings`

| Key | Default | Description |
|-----|---------|-------------|
| `TcpPort` | `0` | Port for the PSTT data cache TCP server. `0` = disabled. Set a non-zero port to allow `pstt-sub` or other tools to subscribe to live MQTT data. |

---

### `App`

| Key | Default | Description |
|-----|---------|-------------|
| `MaxMessageHistory` | `500` | Max messages kept per Log widget in memory |
| `AutoSaveOnExit` | `false` | Auto-save dashboard when leaving edit mode |
| `AlternateInstances` | `[]` | List of `{ "Label": "...", "Url": "..." }` shown as link buttons in the About box |

`AlternateInstances` is most useful in dual-port or dual-process deployments where you want a quick "switch to admin view" / "switch to read-only view" button. The label is free text; the URL is opened in a new browser tab.

```json
"App": {
  "AlternateInstances": [
    { "Label": "Read-Only Port",  "Url": "http://myhost:8080" },
    { "Label": "Admin Port",      "Url": "http://myhost:8081" }
  ]
}
```

`AutoSaveOnExit` is persisted to `appsettings.user.json` when changed via the Options menu; no manual file edit needed.

---

### `Auth`

| Key | Default | Description |
|-----|---------|-------------|
| `AdminPasswordHash` | `""` | bcrypt hash of the admin password. Empty = no auth required. |

Generate a hash via `/setup` in the UI. The setup wizard writes the hash to `appsettings.user.json` automatically.

If auth is enabled:
- Non-authenticated users see dashboards in read-only view (no edit controls)
- Authenticated admins can edit and save dashboards
- Auth is independent of `ReadOnly=true` (which overrides everything)

---

### `Startup`

Controls which dashboard is opened when the server starts (or when a new client session begins).

| Key | Default | Options | Description |
|-----|---------|---------|-------------|
| `Mode` | `"LastUsed"` | `LastUsed`, `SpecificFile`, `None` | Dashboard to open on start |
| `Dashboard` | `""` | — | File stem (no extension) when `Mode = SpecificFile` |

Written to `appsettings.user.json` by the Admin → Startup Dashboard UI.

---

### `ReadOnly` and `ReadOnlyPorts`

| Key | Default | Description |
|-----|---------|-------------|
| `ReadOnly` | `false` | Entire process is read-only |
| `ReadOnlyPorts` | `""` | Comma-separated list of ports that are read-only (e.g. `"8080"`) |

See [deployment-modes.md](deployment-modes.md) for full examples including dual-port Docker Compose setup.

---

### `RenderMode` *(WebApp host only)*

| Value | Description |
|-------|-------------|
| `Auto` *(default)* | Interactive Auto — WASM for capable browsers, Blazor Server fallback |
| `WebAssembly` | Always deliver WASM; pure client-side after first load |
| `Server` | Blazor Server; no WASM download |

Not applicable to `PSTT.Dashboard.WebAppServerOnly` (always uses Blazor Server).

---

### `AllowedPathBase`

Set when the app is hosted under a sub-path via a reverse proxy:

```json
"AllowedPathBase": "/dashboard"
```

Leave empty when served from the root (`/`).

---

### `RemoteAccess`

| Key | Default | Description |
|-----|---------|-------------|
| `ApiToken` | `""` | Token for API-level access (CLI tools, remote repo pull). Managed by the UI. |

Manually set only if you need to pre-configure a token before first run. Otherwise, generate it from Admin → Remote Access Token.

---

### `RemoteRepositories`

Array of remote PSTT Dashboard instances to pull dashboards from. Managed via Admin → Remote Repositories UI.

```json
"RemoteRepositories": [
  {
    "Name": "Production",
    "Url": "https://dashboard.example.com",
    "ApiToken": "token-from-that-instance"
  }
]
```

---

### `UpdateAgent` *(Docker / host deployments)*

Controls the "Upgrade Now" feature in the About box. Only needed if you use the host-agent-based auto-update workflow.

| Key | Default | Description |
|-----|---------|-------------|
| `Url` | `"http://host.docker.internal:8080/update"` | URL of the update agent |
| `Token` | `""` | Pre-configured agent token (overridden by client-supplied token if provided) |
| `Service` | `""` | Docker Compose service name to update |
| `ComposeFile` | `"docker-compose.yml"` | Compose file path |
| `WatchtowerContainer` | `""` | Watchtower container name (alternative to compose pull/up) |
| `Workdir` | `""` | Working directory for compose commands |
| `AllowClientSpecifiedService` | `false` | Allow the browser client to override the service name |

See `appsettings.Development.UpdateAgent.example.json` for a commented example.

---

## Environment variable naming

All keys can be set as environment variables. Replace `:` with `__` (double underscore):

| JSON path | Environment variable |
|-----------|---------------------|
| `MqttSettings:Broker` | `MqttSettings__Broker` |
| `App:AutoSaveOnExit` | `App__AutoSaveOnExit` |
| `App:AlternateInstances:0:Label` | `App__AlternateInstances__0__Label` |
| `Auth:AdminPasswordHash` | `Auth__AdminPasswordHash` |
| `DiagramStorage:DataDirectory` | `DiagramStorage__DataDirectory` |
| `ReadOnly` | `ReadOnly` |
| `ReadOnlyPorts` | `ReadOnlyPorts` |
| `ASPNETCORE_URLS` | `ASPNETCORE_URLS` *(ASP.NET built-in)* |
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` *(ASP.NET built-in)* |

> **Tip for Docker Compose:** use the `environment:` block for secrets and deployment-specific values. Keep stable defaults in `appsettings.json` inside the image.

---

## Settings writable at runtime

These keys are updated by the UI and written to `appsettings.user.json` automatically:

| Key | Written by |
|-----|------------|
| `Auth:AdminPasswordHash` | First-time Setup wizard (`/setup`) |
| `Startup:Mode` | Admin → Startup Dashboard |
| `Startup:Dashboard` | Admin → Startup Dashboard |
| `App:AutoSaveOnExit` | Options menu toggle |
| `RemoteAccess:ApiToken` | Admin → Remote Access Token |
| `RemoteRepositories` | Admin → Remote Repositories |

All other settings are read-only at runtime and require a server restart after editing.
