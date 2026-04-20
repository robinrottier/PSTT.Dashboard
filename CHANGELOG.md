# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- **Blazor.Diagrams git submodule** — `rrSoft.Blazor.Diagrams` NuGet package replaced with a local
  `ProjectReference` to a Git submodule at `libs/Blazor.Diagrams` (fork: `robinrottier/Blazor.Diagrams`).
  Allows direct edits to the diagram engine during active dashboard development.
- **`release.ps1` — submodule test steps** — new `test-pstt` and `test-blazor-diagrams` steps run
  the submodule test suites as part of the build pipeline (also included in `-Verify` local mode).

### Changed
- **`release.ps1` — visual step groups** — the interactive step menu now groups steps under headers
  (Preflight / Build & Test / Version / GitHub Release / Deploy) with `[✓]`/`[-]`/`[ ]` indicators.
- **`release.ps1` — richer menu input** — new commands: `N-M` range toggle, `all`, `none`/`clear`,
  `exit`/`quit`, and group keywords with prefix matching (`bui` for Build & Test, `ver` for Version, etc.).
- **`release.ps1` — light background auto-detection** — terminal background colour is probed
  automatically (OSC 11 query, then PSReadLine colour heuristic) so light-theme users no longer get
  white-on-white text without passing `-LightBackground`.
- **`release.ps1` — spinner output** — long-running commands now show a braille spinner + elapsed
  time on one overwriting line while running. On success: single `✓` summary line. On failure: last
  50 lines of captured output. Terminal stays clean instead of scrolling hundreds of build lines.
- **Dockerfile** — added `COPY` steps for `libs/Blazor.Diagrams/` so Docker builds resolve the
  submodule `ProjectReference` correctly.

## [v0.1.17] - 2026-04-03

## [v0.1.17] - 2026-04-03
- Preparing release v0.1.16 (2026-04-02)

### Changed
- **`scripts/release.ps1`** — full rewrite: Linux/WSL compatible, auto-restarts in pwsh 7 when invoked from Windows PowerShell 5.1, step selection via `-From`/`-Only`/`-Skip`/`-BumpType`, interactive step-selection menu and retry/skip prompts on failure, coloured output. Bug fixes: `-WorkflowTimeoutMinutes` was silently ignored, parallel mode swallowed failures, CHANGELOG insert format was wrong, PR CI polling now uses `gh pr checks`.
- **`scripts/release.ps1`** — added `-Verify` mode (local-only check: preflight → build → publish → docker build, no git ops, no `gh` required); new steps `publish-check` (mirrors `release.yml`), `docker-build` (mirrors `docker.yml`), and `post-deploy` (SSH deploy, auto-skipped unless `DEPLOY_HOST` env var is set). Step count updated to 14.

### Added
- **`MqttDashboard.Mqtt.Tests`** — new test project with a real in-process MQTTnet broker (`MQTTnet.Server`).
  Tests cover `MqttClientService` connect/subscribe/publish, wildcard subscriptions, and unsubscribe.
- **`MqttDataCacheIntegrationTests`** — full-chain tests: broker message → `DataCache` subscriber,
  `DataCache.PublishAsync` → broker, and real two-instance round-trips through the broker.
- **`ChainedCacheTests`** in `MqttDashboard.Data.Tests` — two/three-level `DataCache` chain tests
  covering downstream value flow, upstream publish, demand-driven subscription propagation, and dispose.
- **Tier B integration tests enabled** — `MqttFlowIntegrationTests` (in `MqttDashboard.IntegrationTests`)
  now run against a real in-process broker; previously skipped pending the `MQTTnet.Server` package.


- **`$DASHBOARD/*` virtual topics** — new `DashboardMetricsPublisher` background service publishes live diagnostic data into `ServerDataCache` every second without touching the MQTT broker. Topics: `$DASHBOARD/TIME`, `UPTIME`, `VERSION`, `VERSION/LATEST`, `VERSION/UPDATE_AVAILABLE`, `MQTT/STATUS`, `MQTT/BROKER`, `MQTT/TOPIC_COUNT`, `CLIENTS/COUNT`. Any widget or dialog can subscribe to these like any other topic.
- **`IDataCache.HasServer`** — bool property; `true` when a data server is already registered. Used to prevent double-registration in same-process hosts.
- **`AddMqttDashboardSameProcess()`** DI helper — call after the standard server+client DI setup for a same-process host (MAUI Blazor, combined desktop, embedded). Wires `ApplicationState.DataCache` directly to `ServerDataCache` (no per-circuit copy, no bridge, no SignalR transport needed).
- SSR pre-render snapshot seeding: during the server-side pre-render phase, `MqttInitializationService` now seeds `AppState.DataCache` from `ServerDataCache` so widgets render with real data in the initial HTML instead of blank values.

### Changed
- **`AboutDialog`** — connected-client count now subscribes to `$DASHBOARD/CLIENTS/COUNT` via `AppState.DataCache` (live, reactive) instead of a one-shot `IMqttDiagnostics.GetConnectedClientCountAsync()` RPC call.
- **`ApplicationState`** — `DataCache` is now constructor-injectable (`IDataCache? dataCache = null`; defaults to `new DataCache()`). Same-process hosts can inject `ServerDataCache` directly.
- **Hub naming & file organisation** — SignalR hub renamed `MqttDataHub` → `DataHub`; hub route `/mqttdatahub` → `/datahub`. `HubDataSubscriptionStore` → `HubSubscriptionStore`. `ClientConnectionTracker` → `HubConnectionTracker` (moved to `Hubs/`). `MqttTopicSubscriptionManager` moved from `Hubs/` to `Services/` (it is pure MQTT broker logic, not hub logic).
- **`IDataCache.PublishAsync` / `IDataServer.PublishAsync`** — publish is now a first-class operation on the cache/server abstraction. `DataCache.PublishAsync` updates locally first then forwards upstream. `IMqttPublisher` interface removed; `SwitchNodeWidget` now calls `AppState.DataCache.PublishAsync` directly.
- **Lazy broker unsubscribe** — `MqttTopicSubscriptionManager` now holds the broker subscription alive for a configurable grace period (default 30 s) after the last subscriber leaves, cancelling the pending unsubscribe if any client resubscribes within that window.
- **`MqttDashboard.Mqtt` project extracted** — `MqttClientService`, `MqttConnectionMonitor`, `MqttTopicSubscriptionManager`, `IMqttClientService` now live in a dedicated project with no SignalR/Blazor dependencies. `MqttDashboard.Server` references it; MQTTnet NuGet dependency moved there.
- **`MqttClientService` decoupled from SignalR** — new `MqttStatusBroadcaster` singleton owns the `MqttConnectionMonitor` → SignalR broadcast. `MqttClientService` now has zero SignalR references, unblocking future extraction into a standalone `MqttDashboard.Mqtt` project.
- **`MqttInitializationService`** — removed `IMqttDiagnostics` dependency; connection status is now delivered purely via the reactive `StatusChanged` event. Skips `RegisterServer()` if the cache already has a server wired.

### Removed
- **`IMqttDiagnostics`** interface and all implementations (`MqttDataServer`, `SignalRDataServer`) — superseded by `$DASHBOARD/*` virtual topics.
- `GetMqttBrokerInfo`, `GetMqttConnectionStatus`, `GetConnectedClientCount` SignalR hub methods — replaced by topic subscriptions.

### Changed
- **`MqttDataHub`** — SignalR hub fan-out now uses `ServerDataCache.Subscribe()` callbacks instead of `MqttTopicSubscriptionManager.GetInterestedClients()`. Each client subscription stores a `IDisposable` handle in the new `HubDataSubscriptionStore` singleton; disposing it on disconnect automatically ref-counts the broker subscription down via `DataCache`. Clients receive the current cached value immediately on subscribe (no wait for next MQTT message).
- **`MqttClientService.HandleIncomingMessageAsync`** — removed direct SignalR dispatch; fan-out is now handled by the hub's `DataCache` subscriber callbacks.

### Added — FEAT-H: Data layer refactor (Phases 1–3)
- **`MqttDashboard.Data` project** — new pure-C# library (no Blazor/ASP.NET/MQTT dependencies) holding the entire topic pub/sub infrastructure: `IDataCache`, `DataCache`, `IDataServer`, `CacheBridgeDataServer`, `TopicMatcher`, `XmlPayloadHelper`. Enables future non-Blazor hosting and isolated unit testing.
- **`IDataCache` / `DataCache`** — thread-safe in-memory topic store with wildcard subscribe (`+` / `#`), demand-driven upstream subscription (first subscriber triggers `IDataServer.SubscribeAsync`; last subscriber triggers `UnsubscribeAsync`).
- **`IDataServer`** — upstream data-provider contract. Implementations push `ValueUpdated`, `StatusChanged`, `Reconnected` events into the cache.
- **`SignalRDataServer`** (Client/WASM) — `IDataServer` + `IMqttPublisher` over SignalR. Replaces `SignalRService`.
- **`MqttDataServer`** (Server singleton) — `IDataServer` + `IMqttPublisher` wired directly to `MqttClientService.OnMessagePublished`. Broker-level subscribe/unsubscribe via `MqttTopicSubscriptionManager`.
- **`ServerDataCache`** (Server singleton) — `DataCache` subclass registered with `MqttDataServer`. Accumulates all MQTT values for the entire server process; shared across all Blazor Server circuits. `MqttDataHub.GetCurrentValuesForTopics` reads from here.
- **`CacheBridgeDataServer`** — pure-C# `IDataServer` bridge: subscribes to an upstream `IDataCache` and forwards data/status events downstream. Used by each Blazor Server circuit to subscribe to `ServerDataCache` without independently hooking MQTT events. Idempotent subscribe; forwards status from an optional status-source server.
- **`IMqttPublisher`** — publish-only interface; `SwitchNodeWidget` injects this instead of the full `IDataServer`.
- **55 unit + integration tests** covering `DataCache`, `TopicMatcher`, `CacheBridgeDataServer`, hub flow, and REST API.

### Changed — FEAT-H
- Blazor Server circuits now receive data from `ServerDataCache` via `CacheBridgeDataServer`; widgets see cached values immediately on mount (no wait for next broker message).
- `ApplicationState.DataCache` typed as `IDataCache`; `DataServer` property replaces `SignalRService`.
- `MqttInitializationService` wires `IDataServer` events and calls `DataCache.RegisterServer()` on startup.
- All widget `Watch()` calls renamed to `Subscribe()`.

### Removed — FEAT-H
- `ISignalRService` / `SignalRService` / `ServerSignalRService` — replaced by the above.
- `ITopicCache` / `TopicCache` — renamed to `IDataCache` / `DataCache`.
- `InProcessDataServer` — replaced by `MqttDataServer` (singleton) + `CacheBridgeDataServer` (scoped).
- `MqttClientService.LastKnownValues` — superseded by `ServerDataCache`.

### Added
- **`MqttDataServer`** (Server) — singleton `IDataServer` + `IMqttPublisher` + `IMqttDiagnostics`; hooks `MqttClientService` events and feeds all MQTT data into `ServerDataCache`. Replaces `InProcessDataServer`.
- **`ServerDataCache`** (Server) — singleton `DataCache` that accumulates all MQTT values for the entire server process; shared across all Blazor Server circuits. `MqttDataHub` now reads current values from here.
- 12 new unit tests for `CacheBridgeDataServer` (added Moq to `Data.Tests` project).

### Changed
- Blazor Server circuits now receive MQTT data from `ServerDataCache` via `CacheBridgeDataServer` rather than each circuit independently hooking `MqttClientService.OnMessagePublished`. Widgets see cached values immediately on mount without waiting for the next broker message.
- `MqttDataHub.GetCurrentValuesForTopics` reads from `ServerDataCache` instead of `IMqttClientService.LastKnownValues`.

### Fixed
- **`/healthz` probe in Playwright fixture** — now uses `?ignoreMqtt` so probe returns 200 (not 503) when broker absent; fixture fails immediately on unexpected non-2xx (no 60s timeout).

### Changed
- **`/healthz` endpoint** — replaced `MapHealthChecks` with a custom minimal API. Adds `?ignoreMqtt` query param that skips the MQTT check and always returns 200. Full check still returns 503 when MQTT is disconnected. Response body now includes per-check JSON details.
- **Server log capture** — Playwright fixture now captures server stdout/stderr using async `BeginOutputReadLine` (no pipe-buffer risk). `ServerLog` property exposed for assertions.

### Added
- **Custom app icon** — new flow-chart SVG icon (`mqttdashboard-icon.svg`) with three squares connected by lines. Replaces the Material `AccountTree` icon in the AppBar title and the placeholder circles in the PWA manifest.
- **PWA icons consolidated** — `manifest.webmanifest`, `icon-192.png`, and `icon-512.png` are now in the `MqttDashboard.Client` RCL (`wwwroot/`) rather than duplicated in each host project.
- **Automatic PNG icon generation** — MSBuild target in `MqttDashboard.Client.csproj` regenerates `icon-192.png` and `icon-512.png` from the SVG source whenever the SVG is modified (incremental; requires `pwsh`).
- **Roslyn source generator for icon constant** — `AppIcons.MqttDashboard` (inner SVG elements as a C# string for MudBlazor `Icon` parameter) is now generated at compile time from the SVG source via `MqttDashboard.SourceGenerators`. No committed generated file; updating the SVG and rebuilding is all that's needed.
- **Test category filtering** — Playwright tests are tagged `[Trait("Category","Playwright")]`; `MqttDashboard.runsettings` excludes them by default in VS Test Explorer so fast tests run without waiting for browser startup.

### Added
- **`ServerLog_HasNoUnexpectedErrors` Playwright test** — asserts no unexpected `[ERR]` lines after a page load (whitelists known MQTT-connection-refused warnings).

### Added
- **Integration test project** (`MqttDashboard.IntegrationTests`) — 12 server-side integration tests using `WebApplicationFactory` and a real `SignalR.Client.HubConnection`. Covers the full MQTT→SignalR data path with a `FakeMqttClientService` (no broker needed): hub connect/subscribe/receive/unsubscribe, per-client topic isolation, cached-value query, broker info, client count. Plus REST API smoke tests (health check, dashboard list, default dashboard GET). 3 Tier-B tests (real in-process broker) are scaffolded and skipped pending a MQTTnet server package addition.
- **Playwright UI test project** (`MqttDashboard.PlaywrightTests`) — headless Chromium E2E tests via `PlaywrightWebAppFixture` (starts server via `dotnet run`). Covers: home page load/title/MQTT icon, hamburger always visible at narrow viewport (320px), edit toggle hidden at narrow width, edit toggle visible at desktop, hamburger menu opens on click.
- **`IMqttClientService` interface** — extracted from `MqttClientService`; `MqttDataHub` now depends on the interface, not the concrete class. Required for test-double injection.

### Fixed
- **AppBar hamburger position** — removed `position:absolute` from `.appbar-menu-pin`; now uses `flex-shrink:0` so it stays in-flow and flush to the right edge at all widths.
- **Mobile two-line title CSS** — missing `@media (max-width:599px)` opening bracket caused mobile title overrides to never apply.

### Added
- **PWA / Web App Manifest** — added `manifest.webmanifest` to both host projects. Firefox (and Chrome/Edge) will show an "Install as app" prompt; once installed the app opens in standalone mode without browser chrome (no address bar, menus). Includes `favicon.png`, `icon-192.png`, and `icon-512.png` icons.
- **Favicon on main WebApp host** — the primary `WebApp` project now has a `wwwroot/` folder serving `favicon.png` (was missing; the ServerOnly host already had one).
- **Auto-save on exit edit mode** — new Options menu item "Auto-save on Exit" (visible while in edit mode). When enabled, exiting edit mode saves automatically without prompting. Setting is system-wide, persisted server-side in `appsettings.user.json` (`App:AutoSaveOnExit`). Loaded from server on every page load.
- **Edit mode and login/logout in Options menu** — always accessible from the hamburger menu regardless of screen width. "Edit Mode" toggles edit mode with a checkmark indicator; "Logout" / "Login as Admin" appear when auth is configured.
- **Theme preference persisted** — selected theme (Light/Dark/Auto) is now saved to localStorage and restored on page load.
- **Read-only deployment mode (`ReadOnly=true`)** — set as env var or config to disable all edit UI and block all write APIs. Ideal for public displays.
- **Dual-port read-only mode (`ReadOnlyPorts`)** — single process listens on two ports; specific ports are read-only while others remain editable. Shares MQTT connection and data cache between both ports. Example: `ReadOnlyPorts=8080` with `ASPNETCORE_URLS=http://+:8080;http://+:8081`.
- **`RenderMode=Server` for WebApp image** — run the standard Docker image in Blazor Server mode (no WASM download) by setting `RenderMode=Server`.
- **Deployment modes guide** — new `documents/deployment-modes.md` covering all access-control and render-mode options including future plans.

### Fixed
- **Gauge arc alignment** — background track arc used incorrect coordinates, causing it to render at a slightly different radius than the value arc (two visible concentric rings). Both arcs now share exactly radius 55, giving a single-arc appearance with a coloured filled portion over a grey track.
- **Node without a title no longer grows indefinitely**— set `ControlledSize = true` on `TextNodeModel` so Blazor.Diagrams' ResizeObserver is never activated for our nodes. We manage all node sizes explicitly via CSS and the resize handle; the observer was creating a sub-pixel feedback loop.
- **Grid no longer visible in view mode** — `GridSize` is now cleared to `null` on the diagram options when leaving edit mode.
- **Import dialog "Import" button now enables correctly** — replaced the conflicting `@bind-Value` + `Immediate` + `@oninput` triple on `MudTextField` with a clean `Value` / `ValueChanged` pattern that reliably triggers JSON parsing on every change.
- **Grid snap-to-centre setting is now correctly saved and restored** — was previously lost on reload because the negative-sign convention was decoded before `GridSnapToCenter` was set.
- **TreeView no longer collapses or loses focus on MQTT updates** — replaced MudTreeView/MudTreeViewItem with a lightweight custom div-based renderer; expansion state lives on the model, not inside MudBlazor component state. Added 80 ms debounce to coalesce rapid message bursts into a single render.
- **Import dialog no longer grows when status message appears** — reserved a fixed-height area for the parse-result alert so the dialog stays the same height whether an alert is visible or not.
- **Update-available banner removed from main layout** — was too intrusive; the About dialog already provides version info and the Restart button.

### Changed
- **Import / Export moved to File menu** — was in Edit menu; now in File menu (still gated on edit mode).
- **Grid size enforced to 5–100 px (step 5) in edit mode** — the old negative-value convention replaced by an explicit `gridSnapToCenter` boolean.
- **TreeView root topic now uses standard DataTopics** — the separate "Root Topic" property has been removed; set the topic via the standard MQTT Topics field (same as all other widgets). Existing saved dashboards migrate automatically.
- **TreeView visual improvements** — font reduced to 0.7 rem; value is now bold and right-aligned on each row; updated topics briefly highlight for 2 seconds.

### Added
- **Import / Export via JSON clipboard (FEAT-E)** — Export shows JSON for selected nodes or the current page; Import accepts that JSON (or pastes from clipboard) and adds to the current page or a new page.


- **Data topic management in Dashboard Properties** — MQTT topics are now managed via the Dashboard Properties dialog (topic list with add/remove controls). Dashboard is marked dirty when topics change, so topics are always saved with the dashboard.
- **"No data topics" banner on Display page** — when no data topics are configured, a warning banner at the top of the canvas guides users to Dashboard Properties. In edit mode shows a "Configure Topics" action button.
- **"Add Port → All" option** — new menu item adds all 4 ports (Top, Bottom, Left, Right) to the selected node at once.
- **Same Width / Same Height alignment** — two new buttons in the multi-select alignment toolbar resize all selected nodes to the widest/tallest node's dimensions.
- **Dashboard file metadata** — each saved dashboard now includes a `FileInfo` object with `WrittenAt` (ISO timestamp) and `Filename` at the end of the file.
- **Settings persistence to data directory (FEAT-M)** — `appsettings.user.json` (admin password hash, startup mode) is now written to and loaded from the volume-mounted data directory instead of the container root. Settings survive Docker container restarts and redeployments. Includes one-time migration from old location.
- **Restart from web UI (FEAT-N)** — Docker deployments now show a "Restart Now" button in the update notification banner. After running `docker compose pull`, clicking the button gracefully stops the app and Docker's restart policy brings it back on the new image.

### Fixed
- **No-data topics message** is now a slim banner at the top of the canvas instead of a centred overlay card.
- **`appsettings.user` no longer appears** in the Open Dashboard list.
- **Grid size defaults to 20 px** in edit mode when no grid has been explicitly configured.

### Changed
- **Dashboard Properties** dialog now includes Grid Size (px) and Snap-to-Centre controls (were accidentally dropped in the last refactor).
- **Options > Show > Dashboard Name** menu item removed — setting is now in the Dashboard Properties dialog.
- **Page > Home** menu removed — no longer relevant.
- **About dialog** now shows a "Restart App" button for admins on Docker deployments regardless of whether an update is available.

### Removed
- **"No topics" centered overlay** — replaced with a top banner (see Fixed above).
- **Options > Show > Dashboard Name** menu item — setting moved to Dashboard Properties dialog.
- **Page > Home** menu — no longer relevant.

- **Icon rendering in all node types** — StandardNodeLayout now renders Node.Icon alongside Node.Title for all visual node types (Gauge, Battery, Switch, Text). Previously only Text nodes rendered icons.
- **Node-type-specific properties auto-rendered** — NodePropertyEditor no longer has any `@if (Node is XxxModel)` blocks; properties appear from `[NpXxx]` model attributes automatically. Adding a new node type requires only annotating its model properties.
- **Battery: value topic index + color topic index** — Battery nodes now have the same DataTopicIndex and ColorTopicIndex controls as Gauge nodes.
- **Undo All** — new Edit menu item that reverts all unsaved changes in a single step.
- **Gauge: text position** — the static Text label can now be displayed above or below the gauge arc.
- **Gauge: value topic index** — specify which data topic (0-based) drives the gauge arc and displayed value.
- **Gauge / Battery: color transition topic index** — single Color Topic setting controls which data topic drives all color transition rules.
- **Log: independent column toggles** — six checkboxes (Date / Time / Full Topic / Topic Path / Topic Name / Value) independently control which log columns are visible.
- **Log node pause button** — Pause/Play icon button in the log widget header allows freezing the log.
- **Reconnect value replay** — after a SignalR reconnect, the server pushes last-known values to the client immediately.
- **Log node** — scrolling timestamped history of messages received on a topic.
- **TreeView node** — displays all live MQTT topics and values under a configurable root prefix.
- **Multi-page dashboards** — a dashboard file holds multiple named pages. Page tabs appear above the canvas.
- **Colour transition direction** — each threshold entry has a `Direction` property (>= or <=).
- **Battery colour thresholds** — Battery uses the same ordered ColorThresholds list as Gauge.
- **ColorTransitionEditor component** — reusable component for editing ordered value→colour thresholds.
- **MQTT publish: Retain + QoS** — Switch node has configurable Retain flag and QoS level (0/1/2).
- **Page tab rename** — double-click a page tab in edit mode to rename it inline.
- **Variable data topics per node** — configurable list of MQTT topics per node. Old DataTopic/DataTopic2 files auto-migrated.
- **Dashboard delete from Open dialog** — trash icon with confirmation prompt.
- **Node alignment tools** — in edit mode with 2+ nodes selected, alignment buttons appear.
- **OS clipboard integration** — copy/paste of nodes uses the browser's native clipboard.
- **Startup dashboard setting** — admin-configurable: Last Used, Specific File, or None.
- **Color transition "Else" fallback** — each Gauge/Battery color transition now has an optional "Else Color" that applies when no threshold rule matches. Previously a hardcoded percent-based default was used.
- **`ColorInputRow` component** — reusable color input row (swatch preview + editable text + Theme/Named/Custom picker buttons + optional clear) used in NodePropertyEditor background color and ColorTransitionEditor threshold rows.

### Changed
- **`MudNodeWidget` (Text node)** now uses `StandardNodeLayout` like all other visual nodes — gains multi-topic-aware tooltip, background image support, and consistent port rendering.
- **`GaugeNodeModel` and `BatteryNodeModel`** — flat MinValue/MaxValue/ArcOrigin/DataTopicIndex replaced by `NumericRangeSettings Range` group. Old dashboard files load correctly.
- **`FormatText` in base class** — format syntax (`{0:0}`, `{0:F2}`, etc.) works identically in Text, Gauge, and Battery.
- **Link animation in base class** — `TriggerLinkAnimation()` moved to `BaseNodeWithDataWidget`; all node types support Link Animation without per-widget code.
- **TreeView widget uses `MudTreeView`** — replaced hand-rolled recursive RenderFragment.
- **Edit mode indicator colour** — grey (view), orange (editing), red (editing with unsaved changes).
- **Page delete confirmation** — confirmation dialog before removing a page.
- **Default dashboard file renamed** — built-in default is now `Default.json` (was `diagram.json`). Existing files auto-renamed on first startup.
- **Display name separated from file name** — human-readable title stored separately from the file stem.
- **Service renames** — IDiagramService → IDashboardService etc.
- **Switch widget uses `MudSwitch`** — replaced custom chip+icon-button.
- **Log widget uses `MudSimpleTable`**.
- **Exit-edit prompt** — shows Save / Discard / Cancel when exiting edit mode with unsaved changes.
- File/Save now saves to the currently-open filename.

### Removed
- **`BackgroundImageFromData` property** — removed from all active code paths (kept as legacy null field in `NodeState` for file compatibility).
- **Duplicate icon/tooltip/port code** in `MudNodeWidget` — replaced by `StandardNodeLayout` (~90 lines removed).
- **Hardcoded node-type `@if` blocks** in `NodePropertyEditor` (~135 lines across 5 blocks) — replaced by 4-line `NodePropertyRenderer` loop.
- **Image node** — removed as a separate node type. Use any node with the background image URL property. Old `Image` nodes load correctly with the image URL preserved.
- **Grid node** — removed. Wildcard topic → row/column binding is deferred to a future release.
- **MRU (recent files)** — removed; the Open dialog is the sole entry point.

### Fixed
- **InvalidCharacterError on hover/render** — SVG `MarkupString` injection in Gauge and Battery widgets used `HtmlEncode` which does not strip null bytes. SVG is XML-strict and rejects null bytes even when HTML-encoded. Fixed by: (1) sanitizing all incoming MQTT string values at `MqttDataCache.UpdateValue` (client-side gateway, covers live and server-replayed cached values), and (2) switching Gauge/Battery SVG text injection to `XmlStringHelper.XmlSafeEncode` (strips invalid XML chars then HTML-encodes). Previous fix only sanitized at the server source; values already in the server's in-memory cache bypassed that.
- **InvalidCharacterError crash** — MQTT payloads containing null bytes or other HTML-invalid characters killed the Blazor circuit. Fixed by sanitizing at source in `MqttClientService`, HTML-encoding `BatteryNodeWidget` MarkupString content, and sanitizing `DataValueTooltipContent` output.
- **Image title not hidden correctly** — `StandardNodeLayout` now checks `ShowTitle` for both title positions.
- **Node infinite resize loop** — clearing the Title field caused the node to grow indefinitely. Fixed by always rendering the header hidden with `display:none` when empty.
- **Ports invisible on all non-Text nodes** — `overflow:hidden` was clipping ports; `pa-1` padding created a blank border ring. Both removed.
- **Alignment toolbar buttons unclickable** — canvas was intercepting clicks. Fixed with `position:relative` and `z-index:1000` on the toolbar overlay.
- **File → New incorrectly enables Save** — Save is now disabled when no filename is set.
- **Save As overwrites without confirmation** — Save As now checks the file list and shows an Overwrite? dialog.
- **Link animations not shown on startup** — `TriggerLinkAnimation` now called in `OnAfterRenderAsync(firstRender:true)` after the SVG is in the DOM.
- **MQTT reconnect storm** — interlocked flag prevents cascading parallel reconnect loops.
- **Battery / Gauge `Text` format syntax** — `{0:0}` format now handled via shared `FormatText()` base class method.
- **Log node wildcard topics** — `MqttDataCache.Watch()` now supports MQTT wildcard patterns (`#`, `+`).
- **TreeView node no longer collapses on every update** — per-topic watchers update only the changed value in-place.
- **Auth on clean start** — auth services now always registered unconditionally.
- **Gauge colour transitions compare raw value** — was comparing `Math.Abs(value − arcOrigin)`; now compares the actual data value.
- Various dirty-flag, discard, and edit-mode prompt fixes (see DEVCHANGELOG for details).

---
## [0.1.2] - 2026-03-18

### Fixed
- Browser tab title was "MqttBashboard.client" — now reads "Mqtt Dashboard"
- About box was not showing the application version — now reads from `AssemblyInformationalVersionAttribute` (MinVer), git SHA suffix trimmed
- All user-facing references to "diagram/diagrams" renamed to "dashboard/dashboards" throughout the UI (Save As dialog, menus, snackbars, property editor, etc.)
- Dashboard files moved from the root data directory into a `dashboards/` subdirectory; existing files are auto-migrated on first startup
- MQTT subscriptions were stored separately in `applicationstate.json` — now embedded in the dashboard `.json` file itself; `applicationstate.json` and all related server/client services removed
- About box in admin mode now shows additional server deployment info: machine name, OS, .NET version, data directory, runtime identifier
- About box update checker and "Check for updates" button now only visible to admin users
- MRU (recent files) list was showing files that had since been deleted — list is now filtered against the server file list on startup and invalid entries are removed when opened
- Opening a dashboard file now correctly restores and activates its MQTT subscriptions via SignalR
- `Show dashboard name in title bar` setting was not persisted in the dashboard file — now saved and restored
- Authentication state was initialised after dashboard load; if load failed, auth was left disabled (login button hidden). Auth state is now initialised first.

### Removed
- `applicationstate.json` file format and all associated code (`ApplicationStateData`, `IApplicationStateService`, `ApplicationStateService`, `ServerApplicationStateService`, `ApplicationStateController`)

---

## [0.1.1] - initial tagged release

_No changelog entry — this predates the changelog._
