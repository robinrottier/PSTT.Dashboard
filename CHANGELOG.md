# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added
- Dashboard JSON files now use compact sequential 1-based integers for node, port, page, and link IDs instead of raw GUIDs. IDs are remapped on save only; runtime model is unchanged. Makes saved files significantly more readable and diff-friendly.

## [v0.1.6] - 2026-04-28

## [v0.1.6] - 2026-04-28

### Added
- TCP cache server endpoint (FEAT-H first step): set `CacheSettings:TcpPort` to a non-zero port to expose the PSTT cache over TCP. External tools can subscribe to topics and publish values using `RemoteCacheBuilder<string>.WithTcpTransport(host, port).WithUtf8Encoding().Build()`. Disabled by default (port 0).
- `pstt-sub` CLI tool (PSTT submodule): subscribe to topics/wildcards on a PSTT TCP cache server; prints `topic=value` to stdout, supports multiple `--topic` args and `--timestamp` flag.
- `pstt-pub` CLI tool (PSTT submodule): publish a single value to a PSTT TCP cache server topic and exit.
- TreeView widget now persists expansion/collapse state per node: collapsed paths are saved in the dashboard file and restored after tab switches or page reload. Explicitly collapsed branches stay collapsed; newly discovered MQTT topics still auto-expand.
- New "Unsaved Changes" dialog (Save / Discard / Cancel) replaces the plain confirmation prompt when leaving edit mode. Includes an "Auto-save in future" checkbox — ticking it suppresses the dialog for all subsequent exits.
- Floating Node Properties panel now follows the selection: stays visible when you switch to a different node (updates to show the new node's properties), and shows a "Select a single node" hint when nothing or multiple nodes are selected.
- Remote repository entries can now be edited in place (click Edit icon in Configure Remotes dialog). Previously only Add + Delete were supported.
- Circular self-remote integration tests (15 tests): local CRUD, Bearer token auth (valid/invalid/none), circular proxy CRUD (list/get/save/delete), remote repo CRUD (add duplicate, edit, rename conflict).

### Fixed
- `release.ps1`: step failure error messages showed the action name rather than the actual error — fixed `$_` clobbering inside `switch` inside `catch`.
- `release.ps1`: "The property 'Count' cannot be found" crash after a step failure — fixed by using `[string[]]@(...)` wrappers.
- Open and Save As dialogs now open centred on the window instead of top-left.
- Server logs now show the reason for 401/403 on dashboard write endpoints (token mismatch, read-only mode, no auth header), making remote save failures diagnosable.
- Remote proxy (RemoteController) no longer silently converts 200 responses with empty bodies to 204 No Content — write operations (save/delete via remote) now correctly return 200.

### Added
- `release.ps1` spinner now shows last build output line + elapsed time (live feedback during long builds).
- `release.ps1` stuck-command warning: ⚠ appears in spinner after 90 s with no new output.
- `release.ps1` `[L]ogs` option at failure prompt to review full captured output without re-running.
- `release.ps1` transitive dependency resolution in step menu and dep+retry prompt (BFS, not just direct parents).

## [v0.1.5] - 2026-04-25

### Added
- **Remote Dashboard Sharing** (MVP): Server can be configured to accept connections from other
  PSTT.Dashboard instances via HTTPS. Access is protected by a generated API token. Remote repositories
  can be registered in Settings → Remote Repositories. Dashboards can be opened from or saved to remote
  instances via the Open/Save dialogs (coming soon). Each server generates an access token that can be
  shared with other instances that wish to access its dashboards. Full proxy forwarding is transparent
  to the browser (token never leaves the server).
- **Serialization Metadata**: Dashboard file info now records:
  - `AppVersion` — version of Dashboard app that saved the file
  - `WrittenByServer` — hostname of the server that saved it
  - `WrittenAt` — timestamp of save (previously unused)
  - `Filename` — name of the file (previously unused)
- **Node Properties as FloatingPanel**: Node Properties is now a modeless floating panel (drag,
  resize, persist position/size via localStorage) instead of a blocking modal dialog. The panel
  can stay open while navigating the canvas; Cancel reverts size/font changes; the X button closes
  without reverting. Position is persisted per node-type across reloads.
- **FEAT-H1 lazy-unsubscribe grace period wired into Dashboard**: `MqttCacheBuilder` (PSTT) now
  exposes `WithUnsubscribeGracePeriod()`; both the MQTT cache and the scoped Blazor-circuit cache
  are configured with a 30-second grace period. Broker and bridge subscriptions survive short
  circuit reconnects without re-subscribing.

- Fixed SaveAs dialog showing empty remote repositories list on first page load
- Fixed remote file list not loading when selecting remote destination
- Improved error feedback for failed remote dashboard operations
### Fixed
- **Remote token regeneration UI**: Clicking "Regenerate" in Remote Repositories settings now
  correctly displays the new token. Previously, the button would respond but the token text would
  not update.
- **TreeView widget `#` root topic** shows all available data. Previously, setting the root
  topic to `#` yielded an empty tree — the widget was reading from the dashboard's scoped
  `BridgedDataCache` which (per MQTT §4.7.2) never matches `$`-prefixed system topics via `#`.
  Now, when the root topic is the global wildcard, the widget reads directly from `DataCache`
  (the full broker namespace).

### Changed
- **PSTT data layer — sentinel tag replaces `TTag` generic**:`InvokeCallback`, `OnInvokeCallback`,
  and the upstream-publish helper now use `object?` instead of a generic `TTag` type parameter.
  `CacheItemWithWildcards` uses a private static sentinel object and `ReferenceEquals` to suppress
  tree walks — no pattern match, no boxing, cleaner method signatures throughout.

## [v0.1.4] - 2026-04-23

### Fixed
- **`release.ps1` `.Count` error after test failure**: `Set-StrictMode -Version Latest` caused a
  secondary "The property 'Count' cannot be found on this object" error to appear in the outer `catch`
  when `Invoke-Cmd`'s failure-output display encountered an empty line collection. Fixed by wrapping
  the pipeline assignment with `@()` to guarantee an array.
- **Flaky Remote.Tests timeouts on CI**: Two TCP-server integration tests were intermittently timing
  out on slow CI agents. Increased `WaitForAsync` timeout for `Standalone_ExistingValue_DeliveredOnSubscribe`
  to 10s; added a 500ms initial delay + extended deadline to 20s for `MultiClient_DisconnectOneDoesNotAffectOther`.
- **Wildcard tree-walk regression in PSTT data library**: The previous dual-delivery fix incorrectly
  suppressed the `OnInvokeCallback` tree walk for all upstream callbacks, breaking wildcard delivery
  on caches where the upstream doesn't support wildcards (`supportsWildcards: false`). Tree-walk
  suppression is now applied only when the upstream supports wildcards — preserving the only delivery
  path for wildcard subscribers in the local-wildcard scenario.
- **Wildcard dual-delivery in PSTT data library**: A `#` subscriber on a `CacheWithWildcards`
  cache with `supportsWildcards: true` upstream received the same upstream value twice — once via
  the exact-key item's `OnInvokeCallback` tree walk, and again via the `#` item's own upstream
  subscription. Fixed by suppressing the tree walk when an upstream callback is the origin of a
  publish (`PublishFromUpstreamAsync`). Also fixed `InitialInvokeAsync` for newly-added wildcard
  subscribers: when the cache has a wildcard upstream sub, the item-tree walk is skipped to avoid
  overlap with the upstream's independent initial replay.
- **Production "no data on F5" bug**: On a remote deployment (real network latency), widgets showed
  no data after a fresh browser load (F5). Root cause was `MqttInitializationService` calling
  `SetSubscribedTopics` (→ `BridgeCache.SetBridges` → `_local.Clear()`) after Blazor had already
  rendered widgets and set up their subscriptions — orphaning all `CacheItem` references. Fixed by
  removing the redundant `LoadDashboardAsync` + `SetSubscribedTopics` call from
  `MqttInitializationService` (Display already handles this correctly and earlier in the lifecycle).
- **`BridgeCache.SetBridges` idempotency**: Calling `SetBridges` with the same patterns no longer
  clears `_local` or disposes bridge subscriptions — preserving live widget subscriptions when the
  scope hasn't changed.
- **Widgets re-subscribe on runtime scope change**: If dashboard properties are changed at runtime
  (changing MQTT subscriptions), widgets now detect the scope change via a `BridgeGeneration` counter
  and re-subscribe to the fresh cache items automatically.

## [v0.1.3] - 2026-04-22

### Fixed
- **`FilterNode` now delegates to `IWildcardMatcher`** (`CacheWithWildcards`, PSTT): `FilterNode.Matches()`
  previously contained its own hardcoded `#`/`+` matching logic, making the configured `IWildcardMatcher`
  irrelevant for live subscription routing. It now delegates entirely to the matcher via a new `FullPattern`
  property (the reconstructed full pattern string, e.g. `sensors/+/temp`). Custom matchers (e.g. using `*`)
  now work end-to-end. The `$` exclusion and all MQTT semantics come from `MqttWildcardMatcher` as the single
  source of truth.
- **Test data corrected** (`CacheWithPatternsTests`): three test cases expected `a/+/#` not to match `a/b`
  and similar patterns not to match their parent topic. This was based on a bug in the old `FilterNode` code —
  per MQTT 3.1.1 §4.7.1, `#` always matches the parent level (0 additional levels), so `a/+/#` correctly
  matches `a/b`. Test expectations updated to reflect the spec.

### Added
- **Data Explorer — multi-pattern input** (`DataExplorerPanel`): the "Data topics" field now
  accepts comma-separated patterns (e.g. `#,$DASHBOARD/#`). Each pattern subscribes
  independently and results are unioned in the topic tree.
- **Data Explorer — prepopulated history** (`DataExplorerPanel`): the dropdown now starts with
  `#` (all real MQTT topics) and `$DASHBOARD/#` (internal dashboard metrics) so users can
  switch without typing.

### Fixed
- **`$DASHBOARD` topics no longer sent to MQTT broker** (`MqttCache`):`SendToBrokerAsync` now
  returns early for any topic beginning with `$`. Internal virtual topics (`$DASHBOARD/*`) stay
  server-local; they cannot echo back from the broker as duplicate subscription updates. Status
  reporting to an external broker should use regular (non-`$`) topic names.
- **MQTT wildcard spec compliance** (`MqttWildcardMatcher`): `#` and `+` in the first filter
  segment no longer match topics whose first segment begins with `$`, per MQTT 3.1.1 §4.7.2.
  Explicit prefix patterns such as `$DASHBOARD/#` continue to work correctly.
- **Data Explorer — scrollbar** (`DataExplorerPanel`):added `scrollbar-gutter: stable` so the
  vertical scrollbar never shifts or obscures row content when it appears.
- **Data Explorer — tooltip z-index** (`app.css`): raised `--mud-zindex-popover` to 2500 so MudBlazor
  tooltips and menus always appear above floating panels (which sit at z-index 2000).
- **Data Explorer — "Assign" button** (`TopicTreeNode`): the AddLink button is now always visible;
  it is disabled (greyed out) when no dashboard node is selected and its tooltip reads
  "Assign to selected node — No item selected" in that state.
- **Data Explorer — label** (`DataExplorerPanel`): renamed text field label from "MQTT Pattern" to
  "Data topics".
- **Data Explorer — history icon** (`DataExplorerPanel`): replaced the history clock icon on the
  pattern history menu with a standard dropdown arrow (`ArrowDropDown`).
- **Data Explorer — auto-expand** (`DataExplorerPanel`): on open, all branch nodes (non-leaf) are
  now recursively expanded; leaf-only (data value) nodes are collapsed by default.
- **Data Explorer — initial position** (`FloatingPanel`): on first open, the panel is now clamped to
  the viewport via JS so it can never appear partially or fully off-screen.
- **Uptime format** (`DashboardMetricsPublisher`): simplified `FormatUptime` to always emit
  `hh:mm:ss` (total hours : minutes : seconds) — no more switching between formats at the 1-hour
  or 1-day boundaries.
- **release.ps1 — tag order**: moved `tag` step before `restore-submodules` so the release tag
  always points to the `origin/main` merge commit, not the "restore submodule" housekeeping commit.
  Release workflow runs on GitHub now show the correct release context. The restore commit now also
  includes `[skip ci]` to suppress unnecessary CI runs.

## [v0.1.2] - 2026-04-22

### Added
- **BridgeCache — dashboard topic scoping** (`PSTT.Data`): new `BridgeCache<TKey,TValue>` class that
  bridges specific subscription patterns from a source cache into an isolated local view.
  Widget subscriptions satisfy locally; they do not propagate upstream through the cache chain.
  Publish on `BridgeCache` reaches the broker; publish on `BridgeCache.Local` stays session-local.
- **Dashboard-scoped data cache** (`ApplicationState`): `BridgedDataCache` wraps `DataCache` and
  bridges the dashboard's configured MQTT topics + `$DASHBOARD/#`. All widget subscriptions, the
  Data Explorer, About dialog, and message-history log now target `BridgedDataCache`. Widgets only
  see data within the dashboard's topic scope; topics from other dashboards or sessions are never
  delivered even if they exist in the upstream broker.
- **`SwitchNodeModel.PublishGlobally`**: new boolean property (default `true`). When unchecked, the
  switch publishes only to the current dashboard session (not to the MQTT broker). Configurable in
  the node property editor under the **Publish** category.

## [v0.1.1] - 2026-04-21

### Added
- **Floating Add Node panel** — replaces the modal dialog. In edit mode, a draggable floating panel
  with a 6-type node picker (Text, Gauge, Switch, Battery, Log, TreeView).
  Toggle via the toolbar button in the tab row, "Add Node" menu, or `Ctrl+Shift+A`. Panel stays open for repeated use.
- **Floating Data Explorer panel** — shows all live MQTT topics as a collapsible tree with current values.
  Enter any MQTT wildcard pattern (default `#`) to filter the subscription; history dropdown remembers last 10 patterns.
  Select a node on the canvas then click the assign button on any leaf to add that topic to the node.
  Toggle via toolbar in tab row, "Data Explorer" menu, or `Ctrl+Shift+D`.
- **Edit-mode toolbar in tab row** — `+` (Add Node) and tree (Data Explorer) icon buttons now live at the
  right end of the page tab row, separated by a divider. No longer overlaid on the canvas.
- **`ICache.GetSnapshot()`** — new method on `ICache<TKey,TValue>` and `Cache<TKey,TValue>` returning
  a point-in-time snapshot of all non-pending entries. Used to seed the Data Explorer on open.

### Changed
- **`release.ps1` — group names in `-Only`/`-From`** — `-Only deploy` runs all Deploy group steps;
  `-From bui` starts from the first Build & Test step. Prefix matching uses the same `$GroupKeywords`
  table as the interactive menu. Numeric refs still work.
- **`release.ps1` — `post-deploy` standalone** — removed `post-deploy → wait-workflows` dependency;
  deploy can be re-run independently any number of times.
- **`release.ps1` — submodule steps** — two new automated steps (`prep-submodules`, `restore-submodules`)
  handle the PSTT submodule branch switch around a release: merges `develop→main` in PSTT, pins the
  Dashboard submodule pointer to PSTT `main` for the release PR, then restores tracking to `develop`
  after merge. The `sync` step now gracefully skips `git pull` when the remote branch doesn't exist yet
  (first push scenario).
- **`release.ps1` — `.gitmodules` `branch` tracking** — PSTT submodule now tracks `develop` during
  normal development and is automatically switched to `main` during the release window.

### Fixed
- **Flaky Release-build tests** — `MultiClient_DisconnectOneDoesNotAffectOther` (PSTT) and
  `WildcardSubscription_MatchesMultipleTopics` / `Publish_Via_Broker_ClientReceivesData`
  (Dashboard integration) used single-shot publishes that raced against server-side subscription
  registration in optimised builds. Replaced with retry-publish loops (200 ms interval, 10 s deadline).

## [v0.1.0] - 2026-04-20

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

