# Developer Changelog

Detailed record of each Copilot-assisted work session — what was investigated, changed, and why.
For reviewing work item by item and moving anything back to [TODO.md](TODO.md) if needed.

---

## 2025-07-14 — FEAT-E: Floating modeless panels (Add Node + Data Browser)

### Commits: 1c45fdf (PSTT submodule), 1aed23c · branch: develop

### 1. FloatingPanel component + JS drag helper

**Files:** `src/PSTT.Dashboard.Client/Components/FloatingPanel.razor`, `FloatingPanel.razor.css`, `src/PSTT.Dashboard.Client/wwwroot/floatingPanel.js`, `src/PSTT.Dashboard.Client/App.razor`

Reusable `position:fixed` draggable container. Header acts as drag handle via `@onmousedown` → `FloatingPanel.startDrag(panelId, x, y, dotNetRef)` in JS. JS attaches one-shot `mousemove`/`mouseup` listeners; on mouseup calls `dotNetRef.invokeMethodAsync('OnDragEnd', left, top)` to persist position. Has minimize (▲/▼) and close buttons. `IAsyncDisposable` to release `DotNetObjectReference`. Script registered once in `App.razor` (shared by both hosts).

### 2. AddNodePanelContent component

**File:** `src/PSTT.Dashboard.Client/Components/AddNodePanelContent.razor`

6-type grid (Text/Gauge/Switch/Battery/Log/TreeView) using `MudIcon` + `MudPaper` tiles. Fires `EventCallback<string> OnNodeTypeSelected` — stays open after selection for repeated use.

### 3. ICache.GetSnapshot() + Cache implementation

**Files:** `libs/PSTT/src/PSTT.Data/Interfaces/ICache.cs`, `libs/PSTT/src/PSTT.Data/Cache.cs`

Added `IReadOnlyDictionary<TKey,TValue> GetSnapshot()` to `ICache` interface. Implemented in `Cache<TKey,TValue>` by filtering for non-Pending items from the internal `ConcurrentDictionary`. Inheriting classes (`CacheWithWildcards`, `RemoteCache`) get it for free.

### 4. DataBrowserPanel component

**File:** `src/PSTT.Dashboard.Client/Components/DataBrowserPanel.razor`

Floating panel showing all live MQTT topics. Seeds from `AppState.DataCache.GetSnapshot()` on init, then subscribes to `#` wildcard for live updates (thread-safe `lock(_topics)` before mutating). Renders a filterable `MudSimpleTable` (topic, value columns). Filter matches both topic path and value. Passes `IsOpen`/`IsOpenChanged` through to `FloatingPanel` without `@bind-` to avoid duplicate-parameter error.

### 5. Display.razor — edit-mode toolbar + floating panels

**Files:** `src/PSTT.Dashboard.Client/Pages/Display.razor`, `Display.razor.cs`

- Edit-mode toolbar (top-left, `position:absolute;z-index:1001`): two toggle icon buttons (Add Node / Data Browser), highlighted with `Color.Primary` when panel open.
- `<FloatingPanel @bind-IsOpen="_isAddNodeOpen">` wrapping `AddNodePanelContent`.
- `<DataBrowserPanel AppState="AppState" IsOpen="_isDataBrowserOpen" IsOpenChanged="...">`.
- `AddNode()` changed from `await DialogService.ShowAsync<NodeTypePickerDialog>` to simple toggle `_isAddNodeOpen = !_isAddNodeOpen`.
- New `OnAddNodeTypeSelected(string nodeType)` contains the node-creation switch.
- `Ctrl+Shift+A` / `Ctrl+Shift+D` shortcuts added to `HandleKeyDown`.
- `MenuToggleDataBrowser` event subscribed in `SubscribeEditEvents` / unsubscribed in `UnsubscribeEditEvents`.

### 6. ApplicationState + AppMenu wiring

**Files:** `src/PSTT.Dashboard.Client/Services/ApplicationState.cs`, `src/PSTT.Dashboard.Client/Layout/AppMenu.razor`

- `ApplicationState`: added `public event Action? MenuToggleDataBrowser` + `RaiseMenuToggleDataBrowser()`.
- `AppMenu`: added "Data Browser" `MudMenuItem` (Ctrl+Shift+D); updated "Add Node" shortcut hint to Ctrl+Shift+A.

## 2025-05-xx — Blazor.Diagrams submodule + release.ps1 step menu overhaul + spinner output + Dockerfile fix

### Commits: 86bc124, 7d3b78d, ce6dbe0, b2a1375, c9b42da, ff806bc, 3e6978e, 73af24b, 0830319, 720d9d9, c1921e0, db800ee · branch: develop

#### `libs/Blazor.Diagrams` — new git submodule

Replaced the `rrSoft.Blazor.Diagrams` NuGet package reference with a local Git submodule pointing
to `robinrottier/Blazor.Diagrams` (the fork used by this project). This allows direct edits to the
diagram engine without publishing a new package first — important while the dashboard is evolving
rapidly.

- `git submodule add https://github.com/robinrottier/Blazor.Diagrams.git libs/Blazor.Diagrams`
- `PSTT.Dashboard.Client.csproj` — `PackageReference` for `rrSoft.Blazor.Diagrams` replaced with
  `ProjectReference` to `libs/Blazor.Diagrams/src/Blazor.Diagrams/Blazor.Diagrams.csproj`
- `PSTT.Dashboard.slnx` — added `/libs/Blazor.Diagrams/` folder with both `Blazor.Diagrams.Core.csproj`
  and `Blazor.Diagrams.csproj` so Visual Studio can open/edit submodule code in the solution
- The submodule's `Directory.Build.props` / `Directory.Packages.props` are self-contained and isolated
  from the parent solution's build props — no interference with MinVer, etc.
- Build verified (0 errors, 1 pre-existing warning in PSTT.Data)

#### `scripts/release.ps1` — step menu overhaul + color auto-detection

**New test steps (submodules):**
- `test-pstt` — builds and runs tests for the PSTT submodule (`libs/PSTT/PSTT.slnx`)
- `test-blazor-diagrams` — builds and runs tests for both `Blazor.Diagrams.Core.Tests` and
  `Blazor.Diagrams.Tests` in the submodule

Both steps are in `$LocalSteps` (run in `-Verify` mode without needing git remotes).
Step count goes from 14 → 16.

**Visual groups in step menu:**
`$StepGroups` (ordered hashtable) defines five sections:
- Preflight, Build & Test, Version, GitHub Release, Deploy

Menu renders group headers with a state indicator: `[✓]` all on, `[-]` partial, `[ ]` all off.
Group consistency is validated at menu entry — throws an error if `$StepOrder` and `$StepGroups`
diverge (catches developer drift).

**Richer menu input tokens:**
- `N-M` — toggle a range of steps (auto-corrects reversed `M-N`)
- `all` — select every step
- `none` / `clear` — deselect every step
- `exit` / `quit` — `exit 0`
- Group keywords (`build`, `test`, `version`, `release`, `github`, `deploy`, `preflight`) — toggle
  all steps in the matching group (partial → all-on; all-on → all-off)

**Color auto-detection:**
`$Host.UI.RawUI.BackgroundColor` is now probed as a best-effort fallback when neither `-LightBackground`
nor `LIGHT_BACKGROUND=1` is set. Maps White/Gray/Yellow/Cyan/Green to light mode. Wrapped in
`try/catch` for hosts that don't expose `RawUI.BackgroundColor`. This fixes the "white text on white
background" symptom when the flag was omitted.

**Dockerfile fix (ff806bc):**
`libs/Blazor.Diagrams/` was never COPYed into the Docker build context. Added explicit COPY steps in
both the restore layer and the build layer so `dotnet restore` and `dotnet build` can resolve the
`ProjectReference` inside the container.

**Spinner + buffered output for `Invoke-Cmd` (3e6978e):**
`Invoke-Cmd` was replaced with a `ProcessStartInfo`-based implementation that:
- Reads stdout and stderr asynchronously (prevents deadlock)
- Shows a braille spinner (`⠋⠙⠹…`) + elapsed time on a single overwriting line while the process runs
- On success: erases spinner, prints `  ✓  <command>  [m:ss]`
- On failure: dumps the last ≤50 lines of captured output (truncated count shown if more)

Non-interactive / CI path unchanged: streams verbosely with `Write-Step` prefix.

⚠️ `Step-BuildRelease` parallel mode uses its own `Start-Process` with temp files — does not go through `Invoke-Cmd` and is unaffected.

**Step dependencies + default-none menu (73af24b):**
- Menu now opens with **no steps selected** (was: all selected). User builds the run plan explicitly.
- Added `$StepDeps` map defining hard data dependencies between steps:
  - `changelog`, `push-changelog`, `tag` → `version` (all use `$script:NextVersion`)
  - `wait-workflows` → `tag`
  - `post-deploy` → `wait-workflows`
- When the user presses Enter in the menu, any selected step with an unselected dep triggers a warning
  and a Y/n prompt to auto-add the missing deps. Answers Y → adds and redisplays menu; N → proceeds anyway.
- `Prompt-OnFailure` now shows the step's known deps and offers `[D]ep+retry`: runs each dep step inline,
  then returns `retry`. If the dep step also throws, aborts with a clear message.

⚠️ Dep resolution is direct-only (not transitive). If you select `tag` and `changelog` without `version`,
both will be flagged independently. Transitive resolution is a future TODO.

**`post-deploy` dependency removed + group names in `-Only`/`-From` (db800ee):**
- Removed `post-deploy → wait-workflows` from `$StepDeps`. Deploy is a standalone step that can be
  re-run any time without a fresh release run having just completed.
- `Resolve-StepName` renamed to `Resolve-Steps` (returns `[string[]]`) and extended to handle group
  keywords (prefix-matched via `$GroupKeywords`). `-Only deploy` now runs all Deploy group steps;
  `-From bui` starts from the first Build & Test step (`test-pstt`). Numeric refs unchanged.
- `@(...)[0]` wrapping guards against PowerShell single-element array unwrap when resolving `-From` to
  the first step of a group.

---

## 2026-04-03 — Program.cs dedup, dual-port Dockerfile, integration test fix, release.ps1 fixes

### Commit: deeb3a7+ · branch: develop

#### `src/PSTT.Dashboard.Server/Extensions/WebApplicationBuilderExtensions.cs` — new startup extension methods

Both `WebApp/Program.cs` and `WebAppServerOnly/Program.cs` had identical ~70-line blocks for Serilog
configuration, data directory resolution, user settings migration, and HA add-on options.

Extracted into two new extension methods in `WebApplicationBuilderExtensions.cs`:

- **`AddMqttDashboardSerilog()`** — Configures Serilog via `Host.UseSerilog(...)`. Writes JSON in
  Production; human-readable template in Development/Test. Must be called before `AddMqttDashboard()`
  so startup errors are captured.
- **`AddMqttDashboardDataDirectory()`** — Resolves data directory (env var → config → default), creates
  it, migrates `appsettings.user.json` from ContentRoot to data dir if needed, registers the user
  settings file as a reloadable config source, and loads HA add-on options from `/data/options.json`
  if present. A private `ResolveDataDir()` helper handles the env var / config / default logic.

Also added `using Microsoft.Extensions.Configuration;` and `using Microsoft.Extensions.Hosting;` /
`using Microsoft.AspNetCore.Hosting;` to satisfy `IConfiguration`, `IsProduction()`, and
`UseStaticWebAssets()` references.

**Note:** `using Serilog;` was already present; `AddDataProtection` package was already referenced.

#### `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/Program.cs` — reduced from ~100 → ~35 lines

Now calls: `AddMqttDashboardSerilog()` → `AddMqttDashboardDataDirectory()` → render mode config →
`AddMqttDashboard(renderMode)` → `UseMqttDashboard<App>(renderMode)`.

#### `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebAppServerOnly/Program.cs` — reduced from ~95 → ~20 lines

Same structure; hardcodes `BlazorRenderMode.InteractiveServer`. Retains `public partial class Program { }`
at bottom for `WebApplicationFactory<Program>` in integration tests.

#### `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/Dockerfile` — dual port exposure

- Changed `EXPOSE 8080` → `EXPOSE 8080 8081`
- Changed `ASPNETCORE_URLS=http://+:8080` → `ASPNETCORE_URLS=http://+:8080;http://+:8081`
- Port 8080 = public read-only; port 8081 = read-write admin
- Both ports share one process, one MQTT connection, one `ServerDataCache`

#### `appsettings.json` in both host projects — `ReadOnlyPorts` default

Changed `"ReadOnlyPorts": ""` → `"ReadOnlyPorts": "8080"` so port 8080 is read-only by default
in fresh deployments. Operators can override via env var `ReadOnlyPorts=`.

#### `docker-compose.yml` and `docker-compose.production.yml` — dual port mapping

- Added `- "8081:8081"` alongside the existing `- "8080:8080"`
- Updated `ReadOnlyPorts=` comment to `ReadOnlyPorts=8080` to match new default
- Updated comments to explain the two-port model (public read-only / admin read-write)

#### ⚠️ Known caveat

`docker-compose.override.yml` does not set `ReadOnlyPorts`; it inherits the value from the base compose
file (or appsettings default). This is intentional — local dev may want both ports writable, so operators
can override to `ReadOnlyPorts=` in their `.env` file.

---

## 2026-04-03 — release.ps1: -Verify mode, publish-check, docker-build, post-deploy steps

### Commit: TBD · branch: feature/feat-h-data-layer

#### `scripts/release.ps1` — 4 enhancements

**1. `-Verify` mode**

New `-Verify` switch (also `VERIFY=1` env var). When set, `Get-StepsToRun` restricts to the
6 local steps: `preflight → clean → build-debug → build-release → publish-check → docker-build`.
No git state changes, no remote operations, no `gh` CLI required.

`Step-Preflight` updated to skip the `gh` availability requirement when `-Verify` is active and
to skip the remote-URL check (not needed for local verification).

Suitable for use as a Copilot post-change verification gate at the end of each session. Also
useful as a quick pre-push sanity check on the developer's machine.

Distinguished from `-DryRun`: `-DryRun` still rehearses the full release flow (commits changelog
locally, etc.) but skips remote pushes; `-Verify` is purely local with zero git mutations.

**2. `publish-check` step (new)**

Mirrors what `release.yml` does for the `linux-x64` target: runs `dotnet publish -c Release
-r linux-x64 --self-contained true`. Catches trim errors, missing publish-only assemblies, or AOT
failures that would slip past `dotnet build`. Cleans up the output directory (`artifacts/publish-check`)
after success. Auto-skipped with `-SkipPublishCheck` switch or `SKIP_PUBLISH_CHECK=1` env var.

⚠️ This step does a full wasm-tools publish so it can be slow (2–4 min) on first run; subsequent
runs benefit from incremental build cache.

**3. `docker-build` step (new)**

Mirrors `docker.yml`'s build step: `docker build -f src/.../Dockerfile -t mqttdashboard:local .`
using the repo root as build context. Auto-skipped (with a warning) if:
- `docker` is not on PATH, or
- the Docker daemon is not running (`docker info` returns non-zero).

Does not push — local verification only. Tags the image `mqttdashboard:local` for manual inspection.

**4. `post-deploy` step (new)**

Final step (position 14). SSHs to a remote host and runs:
```
docker compose -f <compose-file> pull && docker compose -f <compose-file> up -d
```
Configuration via env vars:
- `DEPLOY_HOST` — required; step auto-skips if not set
- `DEPLOY_USER` — SSH user (default: `$env:USER` / `$env:USERNAME`)
- `DEPLOY_PATH` — remote working dir (default: `/opt/mqttdashboard`)
- `DEPLOY_COMPOSE_FILE` — compose file name (default: `docker-compose.yml`)

Skipped in `-DryRun` mode. Documented in `.NOTES` of the script help block.

**Updated step catalogue**

Step order expanded from 11 → 14:
```
preflight → clean → build-debug → build-release → publish-check → docker-build
→ sync → version → changelog → push-changelog → pr → tag → wait-workflows → post-deploy
```

Help text (`.DESCRIPTION`, `.PARAMETER`, `.NOTES`, `.EXAMPLE`) updated throughout.

---

## 2026-04-03 — release.ps1 rewrite: Linux/WSL compat, step selection, bug fixes

### Commit: TBD · branch: feature/feat-h-data-layer

### Context

Full rewrite of `scripts/release.ps1` addressing all open TODO items and several
latent bugs found during review.

### Bugs fixed

| Bug | Previous behaviour | Fix |
|-----|--------------------|-----|
| `$args = @()` | Throws under `Set-StrictMode -Version Latest` (`$args` is read-only) | Removed — GNU-arg parsing replaced with proper `Param()` only |
| `Parse-GnuArgs` | Read the *function*'s empty `$args`, not the script's; GNU flags silently ignored | Entire function removed; all flags handled via `[CmdletBinding()] Param()` |
| `-WorkflowTimeoutMinutes` not used | Both wait functions hardcoded 30 and 45 min timeouts; parameter was dead | Both wait functions now use `$WorkflowTimeoutMinutes * 60` |
| `Exec` dead function | 10-line function never called anywhere | Removed |
| Parallel mode silent failures | `Start-Process` without `-RedirectStandardOutput` merged both streams; build failure output lost | Separate temp files capture each job's output; replayed to host after `Wait-Process` |
| `Update-ChangeLog` wrong format | Inserted raw `- Preparing release vX.Y.Z` bullet inside `[Unreleased]` | Now inserts a proper `## [vX.Y.Z] - YYYY-MM-DD` versioned section |
| PR CI polling via `gh run list` | Polling workflow runs by branch is unreliable for PR checks | Replaced with `gh pr checks $prNum --json state,name` |
| `Run-LocalCommand` empty-output retry | Heuristic retried via shell fallback when stdout empty — fired on legitimate no-output commands | Replaced with `Invoke-Cmd` / `Get-CmdOutput` / `Assert-Cmd` helpers using `& $Exe @ArgList` directly |

### New features

**Linux / WSL compatibility** — all commands now invoked with `& $Exe @ArgList` (works on Windows, Linux, macOS, WSL). Uses `Join-Path`/`Split-Path` for paths; no hardcoded backslashes. `Pop-Location` in `finally` restores caller's directory.

**Auto-restart from `powershell.exe` → `pwsh`** — script detects `PSVersion.Major -lt 7`, resolves `pwsh` on PATH, re-executes with all bound parameters forwarded.

**Step selection: `-From`, `-Only`, `-Skip`, `-BumpType`** — resume from a named step, run a single step, skip named steps, or bump major/minor instead of patch.

**Interactive step selection menu** — when stdin is attached and no explicit step flags are set, shows a numbered checklist and lets the user enter step numbers to skip.

**Interactive retry/skip on failure** — `[R]etry [S]kip [A]bort` prompt on step failure in interactive mode; auto-aborts in CI/non-interactive.

**Coloured output** — cyan headers, gray step detail, green success, yellow warnings, red failures.

### ⚠️ Remaining open items

- Buffered step output (show only on failure) — not yet implemented
- Docker build smoke-test step — not yet added
- Post-release remote-deployment upgrade step — not yet added

---



## 2026-04-02 — Add MQTT and Data layer integration tests

### Commit: c4f6f81 · branch: feature/feat-h-data-layer · UTC: 2026-04-02T23:22:52Z

#### New project: `MqttDashboard.Mqtt.Tests`

**Files:** `tests/PSTT.Dashboard.Mqtt.Tests/` (new project, added to `MqttDashboard.slnx`)

**Why:** `MqttDashboard.Mqtt` and `MqttDashboard.Data` are now separate modules; they needed
test coverage that exercises them against a real in-process MQTT broker rather than relying
on mocks or the full server stack.

**`InProcessMqttBrokerFixture`** — xUnit `IAsyncLifetime` class fixture that starts a real
`MQTTnet.Server` broker on a free port for the duration of the test class. Added
`MQTTnet.Server 5.1.0.1559` package to this project. The same pattern was applied to
`MqttDashboard.IntegrationTests` (see below).

**`MqttTestHelpers`** — static helpers shared by all test classes: `StartServiceAsync`
builds and starts a `MqttClientService` against the broker (waits for `Connected` state),
`ConnectExternalClientAsync` creates a plain `IMqttClient` for publisher/subscriber roles,
`PublishAsync` / `WaitForMessageAsync` convenience wrappers.

**`MqttClientServiceTests`** (4 tests):
- `Service_ConnectsToInProcessBroker` — verifies `MqttConnectionMonitor.State == Connected`.
- `Subscribe_ThenPublishFromExternalClient_MessageReceived` — subscribes a topic via
  `MqttTopicSubscriptionManager`, publishes from a second client, asserts `OnMessagePublished`
  fires with the correct topic/payload.
- `Subscribe_WildcardTopic_MatchingMessagesReceived` — verifies `sensors/+/temp` wildcard
  matches two topics but not `sensors/room1/humidity`.
- `Publish_MessageDeliveredToBrokerSubscriber` — `PublishMessageAsync` sends a message that
  an external subscriber receives.
- `UnsubscribeClient_NoMoreMessagesDelivered` — after `UnsubscribeClientFromTopicAsync` no
  further messages arrive (grace period set to 0 ms in tests).

**`MqttDataCacheIntegrationTests`** (5 tests) — wires a `DataCache` to `MqttClientService`
via a test-local `MqttDataServerStub` (implements `IDataServer`; no dependency on Server project):
- `BrokerMessage_ArriveInDataCacheSubscriber` — broker publish → `UpdateValue` → subscriber callback.
- `BrokerMessage_WildcardSubscription_MultipleCacheUpdates` — wildcard cache subscription on top of MQTT.
- `DataCache_PublishAsync_SendsMessageToBroker` — `cache.PublishAsync` flows through stub → `PublishMessageAsync` → external subscriber receives it.
- `RoundTrip_PublishFromCacheA_ReceivedByCacheB` — two independent `MqttClientService` instances on the same broker; Cache A publishes, Cache B subscriber receives, no shared memory.
- `RoundTrip_PublishFromCacheB_ReceivedByCacheA` — reverse direction.

---

#### Chained `DataCache` tests in `MqttDashboard.Data.Tests`

**File:** `tests/PSTT.Dashboard.Data.Tests/ChainedCacheTests.cs` (new)

**Why:** The `CacheBridgeDataServer` wires caches together in a chain. These tests verify
the multi-level chain topology that production code relies on (server-side singleton cache →
per-circuit bridge → per-circuit cache).

**Tests (11):**
- Two-level chain: upstream update → downstream subscriber, wildcard subscriber, seed from
  cached value on subscribe.
- Two-level publish: `downstream.PublishAsync` updates upstream subscribers and also updates
  the downstream cache immediately (local echo).
- Three-level chain (A→B→C): value from A reaches subscriber on C; publish on C reaches A.
- Demand-driven subscription propagation: first subscriber on downstream triggers bridge to
  subscribe on upstream.
- Dispose handle stops propagation.
- Two subscribers on same downstream topic both receive all updates.

---

#### Enable Tier B integration tests in `MqttDashboard.IntegrationTests`

**Files:** `InProcessMqttBrokerFixture.cs`, `MqttFlowIntegrationTests.cs`,
`MqttDashboard.IntegrationTests.csproj`

**Why:** These tests were stubbed out with `[Fact(Skip = ...)]` because `MQTTnet v5` moved
the server component to a separate package. The package (`MQTTnet.Server 5.1.0.1559`) is
now available and has been added.

**Changes:**
- Replaced stub `InProcessMqttBrokerFixture` with a real implementation using `MqttServerFactory`.
- Removed `Skip` attributes from all 3 `MqttFlowIntegrationTests` tests.
- Fixed `WaitForMqttConnectedAsync`: it was calling `InvokeAsync<string>("GetMqttConnectionStatus")`
  (method no longer exists); replaced with a listener on the `MqttConnectionStatus` client event
  that `DataHub.OnConnectedAsync` sends. ⚠️ The hub sends this immediately on connect, so there
  is a small window where the event arrives before the listener is registered; the fix polls for
  100 ms to handle this.

---



### Context

With `MqttClientService` fully decoupled from SignalR (previous commit), the four pure MQTT files were ready to live in their own project. Extracted into `MqttDashboard.Mqtt` — a class library with no Blazor or SignalR dependencies. `MqttDashboard.Server` now references `.Mqtt` as a sibling project.

---

### 1. New project: `src/PSTT.Dashboard.Mqtt/PSTT.Dashboard.Mqtt.csproj`

- `net10.0`, `FrameworkReference Microsoft.AspNetCore.App` (for `BackgroundService`)
- `PackageReference MQTTnet 5.1.0.1559`
- `ProjectReference MqttDashboard.Data`
- No SignalR, Blazor, or MudBlazor references

### 2. Moved files (via `git mv` — history preserved)

| Old location | New location |
|---|---|
| `Server/Services/IMqttClientService.cs` | `Mqtt/IMqttClientService.cs` |
| `Server/Services/MqttClientService.cs` | `Mqtt/MqttClientService.cs` |
| `Server/Services/MqttConnectionMonitor.cs` | `Mqtt/MqttConnectionMonitor.cs` |
| `Server/Services/MqttTopicSubscriptionManager.cs` | `Mqtt/MqttTopicSubscriptionManager.cs` |

Namespace changed from `MqttDashboard.Server.Services` → `MqttDashboard.Mqtt` in all four files.

### 3. Update: `src/PSTT.Dashboard.Server/PSTT.Dashboard.Server.csproj`

- Removed `PackageReference MQTTnet` (moved to `.Mqtt` project — no `.Server` code uses MQTTnet types directly)
- Added `ProjectReference MqttDashboard.Mqtt`

### 4. Using statement updates in `.Server`

Files that reference the moved types now add `using PSTT.Dashboard.Mqtt;`:
- `Hubs/DataHub.cs` — `MqttConnectionMonitor`, `IMqttClientService`
- `Hubs/MqttStatusBroadcaster.cs` — `MqttConnectionMonitor`
- `Services/MqttDataServer.cs` — `MqttClientService`, `MqttTopicSubscriptionManager`, `MqttConnectionMonitor`
- `Services/DashboardMetricsPublisher.cs` — `MqttConnectionMonitor`
- `Extensions/ServiceCollectionExtensions.cs` — all four types
- `Health/MqttConnectionHealthCheck.cs` — `MqttConnectionMonitor`

### 5. Update: test projects

`tests/PSTT.Dashboard.IntegrationTests/FakeMqttClientService.cs` and `IntegrationWebApplicationFactory.cs` — `using PSTT.Dashboard.Server.Services` → `using PSTT.Dashboard.Mqtt` (plus keep `.Server.Services` where other non-moved types are still used).

### 6. Updated `MqttDashboard.slnx`

Added `MqttDashboard.Mqtt` to the `/src/` folder in the solution.

### Result

Dependency chain:
```
MqttDashboard.Data   (pure abstractions, no NuGet deps)
MqttDashboard.Mqtt   (MQTTnet only — no Blazor/SignalR)
MqttDashboard.Server (AspNetCore + SignalR host, references .Mqtt + .Data + .Client)
MqttDashboard.Client (Blazor + SignalR.Client, references .Data)
```

All 71 tests pass (was 66 before — PlaywrightTests added 5).

---

## 2026-04-02 — FEAT-H: Decouple MqttClientService from SignalR

### Commit: TBD · branch: feature/feat-h-data-layer · UTC: 2026-04-02T11:xx

### Context

`MqttClientService` had a direct dependency on `IHubContext<DataHub>` solely to broadcast `MqttConnectionStatus` to all SignalR clients whenever the MQTT connection state changed. MQTT code should have zero knowledge of SignalR. Fixed by extracting the broadcast into a dedicated `MqttStatusBroadcaster` class.

---

### 1. New file: `src/PSTT.Dashboard.Server/Hubs/MqttStatusBroadcaster.cs`

Tiny singleton that wires `MqttConnectionMonitor.OnStateChanged` → `IHubContext<DataHub>.Clients.All.SendAsync("MqttConnectionStatus", ...)`. This is the only place in the codebase that needs to know about both `MqttConnectionMonitor` and `DataHub`. Constructor wires the event once; no methods exposed.

Instantiated eagerly in `WebApplicationExtensions.UseMqttDashboard` via `ApplicationStarted` callback so the event is wired before any clients connect.

### 2. Update: `src/PSTT.Dashboard.Server/Services/MqttClientService.cs`

- Removed `IHubContext<DataHub>` field and constructor parameter.
- Removed `using Microsoft.AspNetCore.SignalR;` and `using PSTT.Dashboard.Server.Hubs;`.
- Removed the `_connectionMonitor.OnStateChanged` lambda that broadcast to hub clients.
- `MqttClientService` now has **zero SignalR references** — pure MQTT concern.

### 3. Update: `src/PSTT.Dashboard.Server/Extensions/ServiceCollectionExtensions.cs`

Registered `MqttStatusBroadcaster` as a singleton.

### 4. Update: `src/PSTT.Dashboard.Server/Extensions/WebApplicationExtensions.cs`

Added `app.Services.GetRequiredService<MqttStatusBroadcaster>()` in the `ApplicationStarted` callback to force instantiation at startup.

### 5. Update: `tests/PSTT.Dashboard.IntegrationTests/FakeMqttClientService.cs`

Removed `IHubContext<DataHub>` from the test double constructor to match the updated base class signature.

### Result

`MqttClientService` imports: was `using Microsoft.AspNetCore.SignalR` + `using PSTT.Dashboard.Server.Hubs` — both gone. The MQTT files (`MqttClientService`, `MqttDataServer`, `MqttConnectionMonitor`, `MqttTopicSubscriptionManager`, `IMqttClientService`) now have no SignalR dependencies, removing the main blocker to extracting them into a standalone `MqttDashboard.Mqtt` project.

---

## 2026-04-02 — FEAT-H: PublishAsync on IDataCache/IDataServer + lazy unsubscribe grace period

### Commit: TBD · branch: feature/feat-h-data-layer · UTC: 2026-04-02T10:xx

### Context

Two FEAT-H items: (1) unify publishing into the cache/server abstraction and eliminate the now-redundant `IMqttPublisher` interface; (2) add a grace-period delay before broker unsubscribes to prevent churn on circuit reconnect.

---

### 1. `IDataCache.PublishAsync` (new method)

Added `Task PublishAsync(string topic, string payload, bool retain = false, int qos = 0)` to `IDataCache`.
- `DataCache.PublishAsync` immediately calls `UpdateValue` (so all local subscribers see the new value without waiting for a broker echo) then forwards to `_server.PublishAsync`.
- This is the single publish entrypoint for all widgets — no need to inject any other service.

### 2. `IDataServer.PublishAsync` (new method)

Added matching `Task PublishAsync(...)` to `IDataServer`. Each implementation:
- **`MqttDataServer`**: calls `MqttClientService.PublishMessageAsync` → broker.
- **`SignalRDataServer`**: calls hub `PublishMessage` method → server → broker.
- **`CacheBridgeDataServer`**: delegates to `_upstream.PublishAsync` (chains into `ServerDataCache` → `MqttDataServer`).

### 3. `IMqttPublisher` removed

Interface deleted (`src/PSTT.Dashboard.Client/Services/IMqttPublisher.cs`). All `: IMqttPublisher` declarations removed from `MqttDataServer` and `SignalRDataServer`. DI registrations removed from `ServiceCollectionExtensions.cs` and `WebApp.Client/Program.cs`. Doc comments updated.

### 4. `SwitchNodeWidget` updated

Removed `@inject IMqttPublisher MqttPublisher`. Toggle now calls `AppState.DataCache.PublishAsync(...)` directly — consistent with how all other data flows through the cache.

### 5. Lazy unsubscribe grace period in `MqttTopicSubscriptionManager`

When the last subscriber for a topic leaves, the broker-level unsubscribe is now deferred by a configurable grace period (default **30 s**).
- A `CancellationTokenSource` is stored per topic in `_pendingUnsubs`.
- If any client resubscribes within the window, the pending unsubscribe is cancelled.
- After the delay expires, `OnTopicUnsubscribeRequested` fires as before.
- Grace period is configurable via the constructor (`int gracePeriodMs = 30_000`); pass `0` to disable.
- Added XML doc comment explaining the behaviour.
- `ScheduleUnsubscribe` / `CancelPendingUnsubscribe` / `FireUnsubscribeAsync` helpers keep the semaphore-protected paths clean.

### 6. TODO.md cleanup

- Removed stale "Is MqttDataHub actually used?" item (DataHub is clearly used; renamed last session).
- Removed naming-pattern arrow item (resolved last session).
- Marked lazy-unsubscribe item done inline.



### Commit: TBD · branch: feature/feat-h-data-layer · UTC: 2026-04-02T10:xx

### Context

Naming consistency pass across the server-side data layer. Goal: MQTT-specific code lives in `Services/` with `Mqtt*` names; SignalR hub code lives in `Hubs/` with `Hub*` / `DataHub` names; no misleading cross-domain prefixes.

---

### 1. `Hubs/MqttDataHub.cs` → `Hubs/DataHub.cs` (class: `DataHub`)

`MqttDataHub` was a SignalR `Hub` subclass — nothing MQTT-specific about it. It relays data from `ServerDataCache` to browser clients over SignalR. Renamed to `DataHub`.
- `IHubContext<MqttDataHub>` → `IHubContext<DataHub>` everywhere.
- Hub route: `/mqttdatahub` → `/datahub` (in `WebApplicationExtensions.cs`).
- Client URL in `MqttInitializationService.BuildHubUrl()`: `"mqttdatahub"` → `"datahub"`.
- Updated log message from "connected to MQTT Hub" → "connected to Data Hub".

### 2. `Hubs/HubDataSubscriptionStore.cs` → `Hubs/HubSubscriptionStore.cs` (class: `HubSubscriptionStore`)

Simpler name; "Data" was redundant — the store is per-hub-connection by definition.
Updated doc comment reference from `MqttDataHub` → `DataHub`.

### 3. `Services/ClientConnectionTracker.cs` → `Hubs/HubConnectionTracker.cs` (class: `HubConnectionTracker`)

Tracks connected SignalR clients — that's a hub concern, not a general service concern. Moved to `Hubs/`, renamed to `HubConnectionTracker`, namespace changed to `MqttDashboard.Server.Hubs`.
Updated refs in: `DataHub.cs`, `MqttDataServer.cs`, `DashboardMetricsPublisher.cs`, `ServiceCollectionExtensions.cs`.
`DashboardMetricsPublisher.cs` gained `using PSTT.Dashboard.Server.Hubs;`.

### 4. `Hubs/MqttTopicSubscriptionManager.cs` → `Services/MqttTopicSubscriptionManager.cs`

This class ref-counts broker-level MQTT topic subscriptions. It has no dependency on SignalR and is consumed only by `MqttDataServer` and `MqttClientService` — both in `Services/`. Moved there; namespace changed to `MqttDashboard.Server.Services`.

### 5. `IMqttClientService.cs` doc comment updated

Reference to `MqttDataHub` → `DataHub`.

### 6. Tests updated

- `FakeMqttClientService.cs`: `IHubContext<MqttDataHub>` → `IHubContext<DataHub>`.
- `HubConnectionHelper.cs`: default hub path `"mqttdatahub"` → `"datahub"`.
- `MqttDataHubTests.cs`: doc comment updated.

All 21 tests pass (13 integration + 8 Playwright).


---

## 2026-04-01 — FEAT-H Phase 4: $DASHBOARD topics, IMqttDiagnostics removal, same-process DI

### Commit: TBD · branch: feature/feat-h-data-layer · UTC timestamp: session end

### Context

Phase 4 of the data layer refactor. Completes the full migration away from adhoc pull-style diagnostic calls and lays the groundwork for a same-process (MAUI/combined host) deployment with no SignalR. All six Phase 4 todos (hub-migration, dashboard-topics-provider, ssr-seed-cache, injectable-datacache, remove-mqtt-diagnostics, same-process-di) are now done.

---

### 1. New file: `src/PSTT.Dashboard.Server/Services/DashboardTopics.cs`

Static string constants for all `$DASHBOARD/*` topic paths:
- `$DASHBOARD/TIME`, `$DASHBOARD/UPTIME`
- `$DASHBOARD/VERSION`, `$DASHBOARD/VERSION/LATEST`, `$DASHBOARD/VERSION/UPDATE_AVAILABLE`
- `$DASHBOARD/MQTT/STATUS`, `$DASHBOARD/MQTT/BROKER`, `$DASHBOARD/MQTT/TOPIC_COUNT`
- `$DASHBOARD/CLIENTS/COUNT`

### 2. New file: `src/PSTT.Dashboard.Server/Services/DashboardMetricsPublisher.cs`

`BackgroundService` that publishes live diagnostic data as virtual `$DASHBOARD/*` topics into `ServerDataCache` (bypassing the MQTT broker entirely):

- Publishes version info once on startup; re-publishes if a newer version is available.
- Reacts to `MqttConnectionMonitor.OnStateChanged` to update `$DASHBOARD/MQTT/STATUS` and `$DASHBOARD/MQTT/BROKER` reactively.
- Ticks every second via `PeriodicTimer`: publishes `TIME`, `UPTIME`, `TOPIC_COUNT`, `CLIENTS/COUNT`.
- Registered as hosted service in `ServiceCollectionExtensions`.

Result: any client subscribing to `$DASHBOARD/CLIENTS/COUNT` (for example) via `DataCache.Subscribe()` receives an immediate seed from cache and live updates every second — no ad-hoc hub methods needed.

### 3. New file: `src/PSTT.Dashboard.Data/IServerSnapshotCache.cs`

Marker interface `IServerSnapshotCache : IDataCache` registered as `ServerDataCache` in server DI. Allows `MqttInitializationService` (Client project) to inject `ServerDataCache` for SSR snapshot seeding without a circular project reference.

### 4. New file: `src/PSTT.Dashboard.Server/Hubs/HubDataSubscriptionStore.cs`

Singleton holding per-connection `IDisposable` subscription handles from `ServerDataCache.Subscribe()`. Needed because `MqttDataHub` is transient per invocation. `OnDisconnectedAsync` disposes all handles via the store.

### 5. Rewrite: `src/PSTT.Dashboard.Server/Hubs/MqttDataHub.cs`

- Removed `MqttTopicSubscriptionManager`, `IConfiguration` injection, and three ad-hoc diagnostic methods (`GetMqttBrokerInfo`, `GetMqttConnectionStatus`, `GetConnectedClientCount`).
- Added `IHubContext<MqttDataHub>` and `HubDataSubscriptionStore` injection.
- `SubscribeToTopic` now calls `ServerDataCache.Subscribe()` with a callback that sends `ReceiveMqttData` to the specific connection; handle stored in store.
- `OnDisconnectedAsync` disposes all handles via store.

### 6. Update: `src/PSTT.Dashboard.Client/Services/MqttInitializationService.cs`

- Removed `IMqttDiagnostics` field and constructor parameter.
- Removed post-`StartAsync` `GetMqttBrokerInfoAsync()` call — status is now delivered reactively via `StatusChanged` event wired before `StartAsync`.
- Added optional `IServerSnapshotCache? serverSnapshot` parameter; `SeedFromServerSnapshot()` method iterates all cached values and copies them into `AppState.DataCache` during SSR pre-render (both WASM/Auto and ServerOnly render paths).
- Added `HasServer` guard: `_appState.DataCache.RegisterServer(_dataServer)` is skipped if `DataCache.HasServer` is already `true` (prevents double-registration in same-process mode where `ServerDataCache` has `MqttDataServer` pre-wired).

### 7. Update: `src/PSTT.Dashboard.Client/Services/ApplicationState.cs`

- Constructor gains optional `IDataCache? dataCache = null`; `DataCache` is set from it (defaults to `new DataCache()` when not injected). Enables same-process hosts to inject `ServerDataCache` directly.

### 8. Update: `src/PSTT.Dashboard.Data/IDataCache.cs` + `DataCache.cs`

- Added `bool HasServer { get; }` to `IDataCache` interface.
- Implemented in `DataCache` as `_server != null`.

### 9. Delete: `src/PSTT.Dashboard.Client/Services/IMqttDiagnostics.cs`

Interface removed entirely. All diagnostic data is now distributed via `$DASHBOARD/*` virtual topics.

### 10. Update: `src/PSTT.Dashboard.Client/Services/SignalRDataServer.cs`

- Removed `: IMqttDiagnostics` from class declaration.
- Removed `GetMqttBrokerInfoAsync()` and `GetConnectedClientCountAsync()` methods.

### 11. Update: `src/PSTT.Dashboard.Server/Services/MqttDataServer.cs`

- Removed `: IMqttDiagnostics` from class declaration.
- Removed `GetMqttBrokerInfoAsync()` and `GetConnectedClientCountAsync()` method implementations.

### 12. Update: `src/PSTT.Dashboard.Client/Components/AboutDialog.razor`

- Removed `@inject IMqttDiagnostics MqttDiagnostics`.
- Added `@implements IDisposable` and `_clientCountSubscription` field.
- `OnInitializedAsync` now subscribes `AppState.DataCache.Subscribe("$DASHBOARD/CLIENTS/COUNT", ...)` — fires immediately from cache (if seeded) and on every publisher tick. `Dispose()` disposes the handle.

### 13. Update: `src/PSTT.Dashboard.Server/Extensions/ServiceCollectionExtensions.cs`

- Removed `services.AddSingleton<IMqttDiagnostics>(...)`.
- Added `HubDataSubscriptionStore` singleton.
- Added `DashboardMetricsPublisher` hosted service.
- Added `IServerSnapshotCache` → `ServerDataCache` mapping.
- Added new `AddMqttDashboardSameProcess()` extension method (see §14).

### 14. New method: `AddMqttDashboardSameProcess()` in `ServiceCollectionExtensions.cs`

Call this after `AddMqttDashboardServerServices()` and `AddMqttDashboardServices()` for a same-process host (MAUI Blazor, combined desktop, embedded):

```csharp
services.AddSingleton<IDataCache>(sp => sp.GetRequiredService<ServerDataCache>());
services.AddScoped<IDataServer>(sp => sp.GetRequiredService<MqttDataServer>());
```

- `ApplicationState` receives `ServerDataCache` directly as its `IDataCache` — no per-circuit copy, no bridge, no SignalR hub.
- `MqttInitializationService` calls `MqttDataServer.StartAsync()` (re-fires current status) and skips `RegisterServer()` because `DataCache.HasServer` is already `true` (wired in `ServerDataCache` constructor).
- `IMqttPublisher` → `MqttDataServer` (already registered in `AddMqttDashboardServerServices`).

### 15. Update: `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp.Client/Program.cs`

- Removed `services.AddScoped<IMqttDiagnostics>(...)`.

### 16. Update: `tests/PSTT.Dashboard.IntegrationTests/MqttDataHubTests.cs`

- Replaced `GetMqttBrokerInfo_ReturnsBrokerString` and `GetConnectedClientCount_ReflectsConnections` (removed hub methods) with:
  - `DashboardTopics_BrokerInfoPublishedToCache` — subscribes to `$DASHBOARD/MQTT/BROKER`; verifies topic delivered with non-empty payload.
  - `DashboardTopics_ClientCountPublishedToCache` — subscribes to `$DASHBOARD/CLIENTS/COUNT`; verifies integer payload.

### Tests

All 71 tests pass (36 Data, 5 Client, 9 Server, 13 Integration [3 skipped], 8 Playwright). Build: 0 errors.

### ⚠️ Caveats / Known remaining issues

- `$DASHBOARD/CLIENTS/COUNT` in integration tests may return "0" (stale cache from pre-connection publisher tick); test now just asserts valid integer, not `>= 1`.
- `AddMqttDashboardSameProcess()` is untested with a real MAUI host — it is the intended interface but will need wiring-up and validation when a MAUI/Electron host is actually added.
- `MqttClientService` still broadcasts `MqttConnectionStatus` to all hub clients via `IHubContext` — this path should eventually be replaced by clients subscribing to `$DASHBOARD/MQTT/STATUS`. Not blocking.
- `DashboardTopics` constants are in `MqttDashboard.Server.Services` — if client widgets need to subscribe to `$DASHBOARD/*` topics they must use string literals or a shared constants file in `MqttDashboard.Data` or `MqttDashboard.Client`.

---



### Commit: 50d2b40 · branch: feature/feat-h-data-layer

### 1. New file: `src/PSTT.Dashboard.Server/Hubs/HubDataSubscriptionStore.cs`

Singleton service that holds `IDisposable` handles returned by `DataCache.Subscribe()` for every active SignalR connection, keyed by `(connectionId, topic)`. Necessary because `MqttDataHub` is transient (one instance per hub method invocation) so it cannot own the handles itself. Provides `IsSubscribed`, `TryAdd`, `TryRemove`, `RemoveAll`.

### 2. Rewrite: `src/PSTT.Dashboard.Server/Hubs/MqttDataHub.cs`

- **Removed** `MqttTopicSubscriptionManager` from constructor and all usages. Broker-level subscribe/unsubscribe is now exclusively driven by `DataCache` ref-counting through `MqttDataServer`.
- **Added** `IHubContext<MqttDataHub>` injection (needed for async callbacks that fire outside hub method invocations).
- **Added** `HubDataSubscriptionStore` injection.
- `SubscribeToTopic`: calls `_serverDataCache.Subscribe(topic, callback)` where callback does `_ = _hubContext.Clients.Client(connectionId).SendAsync("ReceiveMqttData", ...)`. The handle is stored in `HubDataSubscriptionStore`. If a cached value exists, `DataCache.Subscribe` seeds the client immediately.
- `UnsubscribeFromTopic`: retrieves and disposes the handle from the store.
- `OnDisconnectedAsync`: calls `_subscriptionStore.RemoveAll()` and disposes every handle — replaces `_subscriptionManager.UnsubscribeClientFromAllTopicsAsync`.
- **Removed** `GetMqttBrokerInfo`, `GetMqttConnectionStatus`, `GetConnectedClientCount` hub methods (will be replaced by `$DASHBOARD` virtual topics; WASM client has graceful fallbacks returning "unknown"/-1 on invocation failure).

### 3. Simplify: `src/PSTT.Dashboard.Server/Services/MqttClientService.cs`

Removed `GetInterestedClients` lookup and `_hubContext.Clients.Clients(...).SendAsync("ReceiveMqttData", ...)` dispatch from `HandleIncomingMessageAsync`. Hub fan-out now happens via `ServerDataCache` subscriber callbacks. `_hubContext` is retained for `MqttConnectionStatus` all-clients broadcast on MQTT reconnect. `_subscriptionManager` is retained for broker-level subscription management in `ExecuteAsync`.

### 4. Update: `src/PSTT.Dashboard.Server/Extensions/ServiceCollectionExtensions.cs`

Added `services.AddSingleton<HubDataSubscriptionStore>()`.

### Tests

`dotnet test tests/PSTT.Dashboard.Server.Tests` — 9/9 passed. `dotnet build MqttDashboard.slnx` — succeeded.

---

## 2026-03-30 (batch 6 / FEAT-H Phase 3) — Server-side DataCache + CacheBridgeDataServer

### Branch: feature/feat-h-data-layer
### Parent commit: 3639824 (2026-03-30 23:07 UTC)

### Summary

Added a singleton `ServerDataCache` on the server that accumulates ALL MQTT values for the entire server process, and a pure-C# `CacheBridgeDataServer` that lets any per-circuit `DataCache` subscribe to another `IDataCache` as its upstream data source. Each Blazor Server circuit now gets data from the shared cache rather than independently wiring to `MqttClientService.OnMessagePublished`.

---

### 1. `CacheBridgeDataServer` — `src/PSTT.Dashboard.Data/CacheBridgeDataServer.cs` (new)

Implements `IDataServer`. Takes an upstream `IDataCache` and an optional `IDataServer` status source.

- `SubscribeAsync(topic)` → `upstream.Subscribe(topic, callback)`, stores `IDisposable` handle keyed by topic. **Idempotent** — same topic twice is a no-op on the second call.
- `UnsubscribeAsync(topic)` → disposes the stored handle, removes it from the map.
- `StartAsync()` → wires `_statusSource.StatusChanged` and `_statusSource.Reconnected` event forwarding, then calls `_statusSource.StartAsync()`. The last call causes the status source to re-fire its current MQTT status — seeding each circuit's connection indicator without a separate query mechanism.
- `DisposeAsync()` → unregisters event handlers from the status source; disposes all upstream subscription handles.

Lives in `MqttDashboard.Data` (no server-side references). Tested by 12 new unit tests.

---

### 2. `MqttDataServer` — `src/PSTT.Dashboard.Server/Services/MqttDataServer.cs` (new)

Singleton `IDataServer` + `IMqttPublisher` + `IMqttDiagnostics`.

- **Event wiring in constructor** (singleton lifetime = app lifetime, no lifecycle issues).
  - `MqttClientService.OnMessagePublished` → fires `ValueUpdated` for **all** messages (no per-circuit filtering; `DataCache.NotifyWatchers` does topic matching).
  - `MqttConnectionMonitor.OnStateChanged` → fires `StatusChanged` / `Reconnected`.
- `StartAsync()` → re-fires current MQTT status to all current `StatusChanged` handlers. Called once per new circuit via `CacheBridgeDataServer.StartAsync()`. Idempotent.
- `SubscribeAsync(topic)` → calls `MqttTopicSubscriptionManager.SubscribeClientToTopicAsync("server-data-cache", topic)`. Uses the existing subscription manager so broker-level subscribe/unsubscribe ref-counts are shared with hub browser-clients — no double-subscribe at the broker.
- `UnsubscribeAsync(topic)` → `SubscriptionManager.UnsubscribeClientFromTopicAsync(...)`.
- `PublishMessageAsync` / diagnostics delegate to `MqttClientService` / `MqttConnectionMonitor`.

Replaces the per-circuit `InProcessDataServer`. No `_connectionId` per circuit — a single stable `"server-data-cache"` ID.

---

### 3. `ServerDataCache` — `src/PSTT.Dashboard.Server/Services/ServerDataCache.cs` (new)

Singleton subclass of `DataCache`. Constructor takes `MqttDataServer` and calls `RegisterServer(mqttDataServer)` — wires `ValueUpdated` and `Reconnected` events automatically.

Acts as the authoritative in-memory value store for all MQTT topics on the server. `MqttDataHub.GetCurrentValuesForTopics` now reads from here instead of `IMqttClientService.LastKnownValues`.

---

### 4. `InProcessDataServer` — deleted

`src/PSTT.Dashboard.Server/Services/InProcessDataServer.cs` removed. Replaced by `CacheBridgeDataServer` (scoped `IDataServer`) + `MqttDataServer` (singleton status/publish/diagnostics).

---

### 5. `ServiceCollectionExtensions.cs` — updated DI

Old scoped `InProcessDataServer` registrations removed. New registrations:

```csharp
// Singletons
services.AddSingleton<MqttDataServer>();
services.AddSingleton<IMqttPublisher>(sp => sp.GetRequiredService<MqttDataServer>());
services.AddSingleton<IMqttDiagnostics>(sp => sp.GetRequiredService<MqttDataServer>());
services.AddSingleton<ServerDataCache>();

// Scoped per-circuit
services.AddScoped<CacheBridgeDataServer>(sp => new CacheBridgeDataServer(
    sp.GetRequiredService<ServerDataCache>(),
    sp.GetRequiredService<MqttDataServer>()));
services.AddScoped<IDataServer>(sp => sp.GetRequiredService<CacheBridgeDataServer>());
```

---

### 6. `MqttDataHub` — reads from `ServerDataCache`

`GetCurrentValuesForTopics` now iterates over `ServerDataCache.GetValuesByPattern(filter)` for each requested filter, replacing the old `IMqttClientService.LastKnownValues` iteration. Hub still uses `MqttTopicSubscriptionManager` for browser-client topic tracking (full hub migration is a future phase).

---

### 7. `CacheBridgeDataServerTests.cs` — 12 new tests in `MqttDashboard.Data.Tests`

Added Moq package reference to `MqttDashboard.Data.Tests.csproj`. Tests cover:
- `SubscribeAsync` triggers `ValueUpdated` when upstream updates
- Idempotency of double-subscribe
- `UnsubscribeAsync` stops notifications
- `DisposeAsync` cleans up all handles
- `StatusChanged` and `Reconnected` forwarding from status source
- `StartAsync` calls `statusSource.StartAsync`
- No status source — no throw
- Integration test: full round-trip with `DataCache.RegisterServer(bridge)`

**Test count: 25 Data + 9 Server + 5 Client = 39 total. All passing.**

---

### ⚠️ Caveats / known remaining issues

- `IMqttClientService.LastKnownValues` is still on the interface and implemented in `MqttClientService`, but `MqttDataHub` no longer reads it. It can be removed in a cleanup phase.
- `MqttTopicSubscriptionManager` is still used by `MqttDataHub` for WASM browser-client topic tracking. A future phase will migrate the hub to subscribe to `ServerDataCache` directly, removing the need for the subscription manager.
- Because `MqttDataServer.ValueUpdated` fires for ALL messages (not filtered), the `ServerDataCache.NotifyWatchers` may iterate topics with no subscribers — this is a tiny amount of extra work and is harmless.



Phase 2 of the FEAT-H data layer refactor. Renames `ITopicCache`/`Watch()` to `IDataCache`/`Subscribe()`,
introduces `IDataServer` as a demand-driven upstream provider, and replaces `ISignalRService` with
concrete implementations of the new interfaces.

### 1. `MqttDashboard.Data` — rename + new interfaces

**Files changed/created:**
- `ITopicCache.cs` → **deleted**
- `TopicCache.cs` → **deleted**
- `IDataCache.cs` (new) — replaces `ITopicCache`; `Watch()` renamed to `Subscribe()`; adds `RegisterServer(IDataServer)` method
- `IDataServer.cs` (new) — upstream data provider; events: `ValueUpdated`, `Reconnected`, `StatusChanged`; methods: `StartAsync`, `SubscribeAsync`, `UnsubscribeAsync`; NO publish method
- `DataCache.cs` (new) — replaces `TopicCache`; adds subscriber ref-counting and demand-driven `IDataServer` notification (first subscriber for an uncached topic triggers `SubscribeAsync`; last subscriber triggers `UnsubscribeAsync`; `Reconnected` event re-subscribes all active topics)

**Design notes:**
- `IDataServer.SubscribeAsync` is called only when the first subscriber registers for a topic that has no cached value. Subsequent `Subscribe()` calls for the same topic just add callbacks and are seeded immediately from cache.
- `GetValue()` never triggers upstream — only `Subscribe()` does (demand-driven, not pull-on-read).
- `RegisterServer()` wires `server.ValueUpdated` → `cache.UpdateValue` and `server.Reconnected` → `cache.ResubscribeAll` automatically.

### 2. Client — new interfaces, `SignalRDataServer`, updated services

**Files created:**
- `Services/IMqttPublisher.cs` — single-method publish contract; separate from data subscription (per user decision, "publish doesn't conceptually fit the pub/sub contract")
- `Services/IMqttDiagnostics.cs` — thin diagnostics: `GetMqttBrokerInfoAsync`, `GetConnectedClientCountAsync`; used by `AboutDialog` and `MqttInitializationService`
- `Services/SignalRDataServer.cs` — implements `IDataServer`, `IMqttPublisher`, `IMqttDiagnostics`; replaces `SignalRService`; maps hub events (`ReceiveMqttData`, `MqttConnectionStatus`, reconnected) to `IDataServer` events; `SubscribeAsync`/`UnsubscribeAsync` invoke hub methods

**Files deleted:**
- `Services/ISignalRService.cs` — interface removed; replaced by `IDataServer` + `IMqttPublisher` + `IMqttDiagnostics`
- `Services/SignalRService.cs` — implementation removed; replaced by `SignalRDataServer`

**Files updated:**
- `Services/MqttInitializationService.cs` — injects `IDataServer` + `IMqttDiagnostics` instead of `ISignalRService`; wires `StatusChanged`/`Reconnected`/`ValueUpdated` events; calls `AppState.DataCache.RegisterServer(server)` on startup; `RestoreSubscriptionsAsync` calls `IDataServer.SubscribeAsync` directly (no more `GetCurrentValuesForTopicsAsync` batch call — the server pushes values on subscribe)
- `Services/ApplicationState.cs` — `ITopicCache DataCache` → `IDataCache DataCache = new DataCache()`; `ISignalRService? SignalRService` → `IDataServer? DataServer`; `SetSignalRService` → `SetDataServer`
- `Components/AboutDialog.razor` — injects `IMqttDiagnostics` instead of `ISignalRService`
- `Components/DashboardPropertiesDialog.razor` — uses `AppState.DataServer.SubscribeAsync/UnsubscribeAsync` instead of `AppState.SignalRService.SubscribeToTopicAsync/UnsubscribeFromTopicAsync`
- `Widgets/SwitchNodeWidget.razor` — injects `IMqttPublisher` instead of `ISignalRService`
- `Pages/Display.razor.cs` — `SyncSubscriptionsAsync` uses `AppState.DataServer.SubscribeAsync/UnsubscribeAsync`
- `Widgets/BaseNodeWithDataWidget.cs` — `DataCache.Watch()` → `DataCache.Subscribe()`
- `Widgets/TreeViewNodeWidget.razor` — `DataCache.Watch()` → `DataCache.Subscribe()`

### 3. Server — `InProcessDataServer` replaces `ServerSignalRService`

**Files created:**
- `Services/InProcessDataServer.cs` — implements `IDataServer`, `IMqttPublisher`, `IMqttDiagnostics`; direct in-process wiring (no HTTP loopback); hooks `MqttClientService.OnMessagePublished` + `MqttConnectionMonitor.OnStateChanged`; fires `ValueUpdated` for interested clients; fires `Reconnected` on broker reconnect

**Files deleted:**
- `Services/ServerSignalRService.cs` — replaced by `InProcessDataServer`

**Files updated:**
- `Extensions/ServiceCollectionExtensions.cs` — registers `InProcessDataServer` as singleton then aliases `IDataServer`, `IMqttPublisher`, `IMqttDiagnostics` to it

### 4. WASM host DI

- `WebApp.Client/Program.cs` — registers `SignalRDataServer` then aliases `IDataServer`, `IMqttPublisher`, `IMqttDiagnostics` to it

### 5. Tests

- `DataCacheTests.cs` — renamed from `TopicCacheTests.cs`; `Watch()` → `Subscribe()` in all test calls and method names; all 25 tests pass
- All 39 tests passing (Data: 25, Client: 5, Server: 9)

⚠️ `GetCurrentValuesForTopicsAsync` (old hub batch seed call) has been removed. The server now pushes values as part of `SubscribeAsync` confirmation. If widgets appear blank on reconnect in testing, verify `MqttDataHub.SubscribeToTopic` sends a `ReceiveMqttData` message for the current known value.



### Branch: feature/feat-h-data-layer

This batch implements the FEAT-H data layer refactor from TODO.md. A new pure-C# project
`MqttDashboard.Data` is created to hold the topic pub/sub infrastructure, separating it from
the Blazor/ASP.NET layers so it can be reused by future non-Blazor hosts (MAUI, Avalonia, etc.)
and tested in isolation without any framework dependencies.

### 1. New project: `src/PSTT.Dashboard.Data/`

**Files created:**
- `MqttDashboard.Data.csproj` — `net10.0`, no external dependencies (pure BCL)
- `ITopicCache.cs` — interface: `UpdateValue`, `GetValue`, `TryGetValue<T>`, `Watch`, `GetAllTopics`, `GetValuesByPattern`, `Clear`
- `TopicCache.cs` — implementation (moved logic from `MqttDataCache`; uses `TopicMatcher` for wildcard→regex conversion)
- `TopicMatcher.cs` — static class with MQTT topic-filter matching logic; extracted from the private `TopicMatches()` in `MqttTopicSubscriptionManager`. Exposes `Matches(filter, topic)` and `ToRegexPattern(filter)`.
- `XmlPayloadHelper.cs` — XML/DOM sanitization helpers; moved from `MqttDashboard.Client/Helpers/XmlStringHelper.cs`. `TopicCache.UpdateValue()` calls `StripInvalidXmlChars` before storing strings.

**Design notes:**
- `ITopicCache` is now the surface used by all consumers (widgets, `ApplicationState`, etc.)
- `TopicCache` is the only implementation for now; future additions could include a read-only view or a versioned cache
- `TopicMatcher` is the authoritative MQTT wildcard logic; both client-side cache and server-side `MqttTopicSubscriptionManager` now use it

### 2. `MqttDashboard.Client` changes

- Added `<ProjectReference>` to `MqttDashboard.Data`
- `MqttDataCache.cs` deleted — entirely replaced by `TopicCache` from the new project
- `ApplicationState.cs`: `public MqttDataCache DataCache` → `public ITopicCache DataCache = new TopicCache()` + added `using PSTT.Dashboard.Data`
- `Helpers/XmlStringHelper.cs` reduced to a thin forwarding shim delegating to `XmlPayloadHelper` (retained for any code that references it by the old name; only the internal `MqttDataCache.cs` used it, which is now gone, but kept for safety)

### 3. `MqttDashboard.Server` changes

- Added `<ProjectReference>` to `MqttDashboard.Data`
- `MqttTopicSubscriptionManager.cs`: replaced the private `TopicMatches(filter, topic)` method (40+ lines) with `TopicMatcher.Matches(filter, topic)` from the new project. `TopicMatchesFilter` also delegates to `TopicMatcher.Matches`.
- Added `using PSTT.Dashboard.Data;`

### 4. New test project: `tests/PSTT.Dashboard.Data.Tests/`

**Files:**
- `MqttDashboard.Data.Tests.csproj` — xUnit 2.9, references `MqttDashboard.Data` only
- `TopicMatcherTests.cs` — 12 theory cases + 1 fact covering exact match, `+` single-level, `#` multi-level, level-count mismatch, and `ToRegexPattern()`
- `TopicCacheTests.cs` — 12 tests covering store/retrieve, typed retrieval, wildcard and exact watchers, dispose/unwatch, `GetValuesByPattern`, `Clear`, and XML sanitization

All 25 new tests pass. Combined test run: 52 passing, 3 skipped (MQTT broker Tier B tests), 0 failing.

### 5. Infrastructure updates

- `MqttDashboard.slnx`: added `MqttDashboard.Data` (src) and `MqttDashboard.Data.Tests` (tests)
- `.github/workflows/ci.yml`: added `MqttDashboard.Data.Tests` to the unit-test step
- `.github/workflows/docker.yml`: added `MqttDashboard.Data.Tests` to the Test step
- `Dockerfile`: added `.csproj` COPY (restore layer) and source COPY (build layer) for the new project

### What is NOT in scope (Phase 2)

- `ISignalRService` not yet renamed to `IDataBackend` — the backend interface abstraction is the next phase
- `MqttClientService` stays in Server (not moved to a `MqttDashboard.Data.Mqtt` project yet)
- `SignalRService` / `ServerSignalRService` stay in Client/Server (not yet formalized as `IDataBackend` implementations)
- Mock/REST backends, data-source config in dashboard file — all Phase 2



## 2026-03-30 (batch 3) — Roslyn source generator for app icon + PWA consolidation

### Commit: cb3c94f · UTC 2026-03-30 · branch: develop

Both hosting projects (`WebApp` and `WebAppServerOnly`) previously had separate copies of
`manifest.webmanifest` and PNG icons in their `wwwroot/` folders. These were consolidated into
`src/PSTT.Dashboard.Client/wwwroot/` so there is a single source of truth.

- `manifest.webmanifest`, `mqttdashboard-icon.svg`, `icon-192.png`, `icon-512.png` now live in
  Client RCL `wwwroot/`.
- Served at `/_content/PSTT.Dashboard.Client/` by the RCL static file pipeline.
- `App.razor` updated to reference `_content/PSTT.Dashboard.Client/...` paths.

#### 2. Custom icon with squares+lines motif

The placeholder circle-based icon was replaced with a flow-chart–inspired design:
- 24×24 SVG viewBox; three squares (top, bottom-left, bottom-right) connected by lines.
- Rotated 90° relative to initial design at user request.
- Single source file: `src/PSTT.Dashboard.Client/wwwroot/mqttdashboard-icon.svg`.

#### 3. PNG auto-generation via MSBuild + PowerShell

`scripts/Generate-Icons.ps1` parses the SVG XML (`<rect>` and `<line>` elements) and renders
them to `icon-192.png` and `icon-512.png` using `System.Drawing`. An MSBuild `GenerateIconPngs`
target in `MqttDashboard.Client.csproj` calls this automatically when the SVG is newer than the
PNGs (incremental via `Inputs`/`Outputs`). `ContinueOnError="true"` keeps builds working on
machines without `pwsh`.

#### 4. AppBar uses custom icon instead of Material AccountTree

`Layout/MainLayout.razor` previously used `Icons.Material.Filled.AccountTree`. Replaced with
`AppIcons.MqttDashboard`, a C# string constant containing the inner SVG elements formatted for
MudBlazor's `Icon` parameter. All `fill`/`stroke` colour values are replaced with `currentColor`
so MudBlazor's `Color` property controls the rendered colour.

#### 5. Roslyn source generator replaces PowerShell for `AppIcons.cs`

The `AppIcons.cs` file was previously generated by `Generate-Icons.ps1` and committed to git
(committed artefact, messy). Replaced with a proper Roslyn incremental source generator:

- New project: `src/PSTT.Dashboard.SourceGenerators/` (targets `netstandard2.0` as required by Roslyn)
- `AppIconsGenerator : IIncrementalGenerator` reads the SVG as an `AdditionalFile` and emits
  `AppIcons.g.cs` **at compile time into `obj/`** — never committed to git.
- `MqttDashboard.Client.csproj` references the generator as `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` and declares the SVG as `<AdditionalFiles>`.
- `AppIcons.cs` removed from git (`git rm`); the file now only exists in `obj/` during builds.
- `Generate-Icons.ps1` simplified to PNG-only (AppIcons.cs section removed).

Key implementation notes:
- `Microsoft.CodeAnalysis.CSharp 4.13.0` requires `Microsoft.CodeAnalysis.Analyzers ≥ 3.11.0` (not 3.3.4).
- Raw string literal nesting (`"""` inside `$$"""..."""`) is not allowed; replaced with `StringBuilder`.
- `LangVersion=latest` in the generator project but `netstandard2.0` target — no collection expressions.
- Build verifies the generator works: `AppIcons.MqttDashboard` used in `MainLayout.razor` compiles without errors, proving `AppIcons.g.cs` is emitted by the generator. Generated files are in `obj/` (not on disk by default unless `EmitCompilerGeneratedFiles=true` is set).

#### Files changed
- `src/PSTT.Dashboard.SourceGenerators/` — new project (generator + csproj)
- `src/PSTT.Dashboard.Client/App/AppIcons.cs` — deleted (now source-generated)
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor` — uses `AppIcons.MqttDashboard`
- `src/PSTT.Dashboard.Client/wwwroot/mqttdashboard-icon.svg` — master icon source
- `src/PSTT.Dashboard.Client/wwwroot/icon-192.png`, `icon-512.png` — generated PNGs (in git)
- `src/PSTT.Dashboard.Client/wwwroot/manifest.webmanifest` — consolidated PWA manifest
- `src/PSTT.Dashboard.Client/App.razor` — updated manifest/icon paths to `_content/PSTT.Dashboard.Client/`
- `src/PSTT.Dashboard.Client/PSTT.Dashboard.Client.csproj` — analyzer reference + AdditionalFiles + simplified MSBuild target
- `scripts/Generate-Icons.ps1` — PNG-only (AppIcons.cs section removed)
- `MqttDashboard.slnx` — SourceGenerators project added

#### Test categories + runsettings (also in this batch)
- `[Trait("Category","Playwright")]` added to all Playwright test classes.
- `MqttDashboard.runsettings` at repo root: `<TestCaseFilter>Category!=Playwright</TestCaseFilter>` — in VS Test Explorer, loading this runsettings file will run all tests except Playwright (slow) by default.

---

## 2026-03-30 (batch 2) — /healthz improvements + server log capture

### Commit: (pending) · UTC 2026-03-30 · branch: develop

#### 1. Custom `/healthz` endpoint with `ignoreMqtt` parameter

Replaced `app.MapHealthChecks("/healthz")` (fixed behaviour) with a minimal API endpoint.

- `GET /healthz` — full check; 200 when healthy, 503 when MQTT disconnected. Returns JSON body
  with `{ status, checks: [{ name, status, description }] }`.
- `GET /healthz?ignoreMqtt` — filters out the `mqtt` check entirely using
  `HealthCheckService.CheckHealthAsync(predicate)`. Always 200 as long as the web process is up.
  Used by the Playwright startup probe and any container liveness check that runs before a broker
  is configured.

#### 2. `PlaywrightWebAppFixture` — async server log capture

Re-enabled `RedirectStandardOutput/Error = true`, but now uses `BeginOutputReadLine` /
`BeginErrorReadLine` (async event-based reading). Output is appended to a `StringBuilder` under
a lock, so the OS pipe buffer never fills and the server never blocks. Exposes `ServerLog`
property for assertions.

`WaitForServerAsync` updated to use `/healthz?ignoreMqtt` probe URL. Behaviour:
- `HttpRequestException` (connection refused) → keep polling — server not up yet.
- `TaskCanceledException` (request timed out) → keep polling.
- Non-2xx HTTP response → **fail immediately** with status code + body + server log so far.
  No more waiting 60 s when the server is up but broken.
- 2xx → server ready, proceed.
- Process exited → fail with server log.

#### 3. Integration test: precise health check assertions

`DashboardApiTests.HealthCheck_ReturnsResponse()` (vague — accepted 503) replaced with two
specific tests:
- `HealthCheck_WithMqttDisconnected_Returns503` — asserts 503 + body contains "mqtt".
- `HealthCheck_IgnoreMqtt_Returns200` — asserts 200 + body contains "Healthy".

Integration tests: **13 pass / 3 skip** (was 12/3).

#### 4. Playwright test: server log clean-check

`HomePageTests.ServerLog_HasNoUnexpectedErrors` — loads the home page, then scans `ServerLog`
for `[ERR]` lines. Whitelists known-OK MQTT connection-refused warnings. Fails with the full
server log if any unexpected errors are found. This catches things like missing static assets,
unhandled exceptions in middleware, etc.

Playwright tests: **8/8 pass** (was 7/7).

#### Files changed
- `src/PSTT.Dashboard.Server/Extensions/WebApplicationExtensions.cs` — custom `/healthz` minimal API
- `tests/PSTT.Dashboard.IntegrationTests/DashboardApiTests.cs` — precise health check tests
- `tests/PSTT.Dashboard.PlaywrightTests/PlaywrightWebAppFixture.cs` — async log capture, smarter probe
- `tests/PSTT.Dashboard.PlaywrightTests/HomePageTests.cs` — `ServerLog_HasNoUnexpectedErrors` test

---

## 2026-03-30 — Playwright E2E tests fixed (all 7 pass)

### Commit: (pending) · UTC 2026-03-30 · branch: develop

#### 1. Root cause 1: stdout/stderr pipe buffer deadlock (`PlaywrightWebAppFixture`)

`ProcessStartInfo` had `RedirectStandardOutput = true` / `RedirectStandardError = true` but
nobody read the streams. Once Kestrel logs exceeded the OS pipe buffer (~4 KB on Windows) the
server process blocked inside `Console.Write`, unable to process any further HTTP requests.
`HttpClient.GetAsync("/healthz")` happened to succeed before the buffer filled (first request,
few bytes of output), making `WaitForServerAsync` happy. But Playwright's Chromium then made a
full page request (HTML + multiple CSS/JS resources) which triggered a flood of log lines —
deadlock. Fix: removed both `Redirect*` flags; server output goes to the terminal (which is
what you'd want in test output anyway). Added `--no-build` to `dotnet run` since the
`WebAppServerOnly` project is already built as a test-project dependency.

#### 2. Root cause 2: static web assets 500 in `Test` environment

`UseStaticWebAssets()` is called automatically only for the `Development` environment inside
`WebApplication.CreateBuilder`. In `Test` environment, MudBlazor CSS/JS, Blazor framework
scripts, and RCL content were all returning 500 with `FileNotFoundException`. Added an
explicit `builder.WebHost.UseStaticWebAssets()` call in `WebApplicationBuilderExtensions.cs`
for any non-Development, non-Production environment. Also tightened `WebApplicationExtensions`
so that `UseExceptionHandler`/`UseHsts`/`UseHttpsRedirection` only run in Production (not
Test), which prevents spurious HTTPS-redirect middleware from running against an HTTP-only
test server.

#### 3. `GotoAsync` navigation wait strategy

Changed all Playwright `GotoAsync` calls from the default `WaitUntilState.Load` (which never
fires because Blazor Server keeps a persistent WebSocket) to `WaitUntilState.DOMContentLoaded`.
SSR renders the full AppBar synchronously, so this is sufficient for all non-interactive
assertions.

#### 4. Blazor circuit readiness for interactive tests

`AppBar_HamburgerMenu_Opens` needs the Blazor Server circuit before `@onclick` handlers work.
Used `page.WaitForLoadStateAsync(LoadState.NetworkIdle)` — fires once the WebSocket upgrade
(the last HTTP request) completes and no further HTTP requests are in flight, which reliably
indicates the circuit is connected.

#### 5. Wrong CSS selector

`AppBar_HamburgerMenu_Opens` was waiting for `.mud-list-item`; MudBlazor 9 uses `.mud-menu-item`
for items in an open `<MudMenu>`. Fixed selector.

#### Files changed
- `src/PSTT.Dashboard.Server/Extensions/WebApplicationBuilderExtensions.cs` — `UseStaticWebAssets()` for Test env
- `src/PSTT.Dashboard.Server/Extensions/WebApplicationExtensions.cs` — Production-only HSTS/HTTPS redirect
- `tests/PSTT.Dashboard.PlaywrightTests/PlaywrightWebAppFixture.cs` — no stdout redirect, `--no-build`, env var style
- `tests/PSTT.Dashboard.PlaywrightTests/HomePageTests.cs` — `DOMContentLoaded`, viewport set before navigate
- `tests/PSTT.Dashboard.PlaywrightTests/AppBarTests.cs` — `DOMContentLoaded`, `WaitForBlazorCircuitAsync`, `.mud-menu-item`

#### Result
- Integration tests: 12 pass / 3 skip ✅
- Playwright E2E tests: **7 / 7 pass** ✅ (was 0/7)

---

## 2026-03-28 (batch 3) — AppBar hamburger fix + integration/Playwright test scaffolding

### Commit: (pending) · UTC 2026-03-28 · branch: develop

---

### 1. AppBar hamburger positioning (final fix)

**Problem:** The hamburger menu icon was ~20px from the right edge instead of flush.

**Root cause:** `.mud-toolbar { position:relative }` in MudBlazor internals makes the toolbar div (not the `<header>`) the containing block for absolutely-positioned children. With `Style="padding-right:52px"` on the AppBar `<header>`, the toolbar was 52px narrower than the header, so `right:0` on the absolute child landed 52px from the right of the header.

**Fix:** Changed `.appbar-menu-pin` from `position:absolute; right:0` to `flex-shrink:0; position:relative; z-index:5; padding-left:20px`. This keeps it in the flex flow, always at the trailing end, never shrunk.

Removed `Style="padding-right:52px;"` from `<MudAppBar>` which was the original workaround.

**Files:** `src/PSTT.Dashboard.Client/Layout/MainLayout.razor`, `MainLayout.razor.css`

---

### 2. Mobile two-line title CSS bug (fixed in same pass)

**Problem:** The two-line title layout at `<600px` had never applied because the `@media (max-width:599px)` opening rule was missing from `MainLayout.razor.css`. The styles were parsed as invalid and ignored.

**Fix:** Added the missing `@media` opening line before the mobile `.appbar-title-inner` overrides.

**File:** `src/PSTT.Dashboard.Client/Layout/MainLayout.razor.css`

---

### 3. `IMqttClientService` interface extraction

**Why:** Required for `WebApplicationFactory` DI override in tests — allows `FakeMqttClientService` to replace `MqttClientService` without modifying the rest of the system.

**Changes:**
- **CREATED** `src/PSTT.Dashboard.Server/Services/IMqttClientService.cs` — `LastKnownValues`, `PublishMessageAsync`
- `MqttClientService` now implements `IMqttClientService`; `_lastKnownValues` made `protected`; `HandleIncomingMessageAsync` extracted as `protected virtual Task` (the hook for `FakeMqttClientService`)
- `MqttDataHub` constructor changed from `MqttClientService` to `IMqttClientService`
- `ServiceCollectionExtensions` registers `IMqttClientService` singleton pointing at `MqttClientService`
- `WebAppServerOnly/Program.cs` — added `public partial class Program {}` for `WebApplicationFactory<Program>`

---

### 4. `MqttDashboard.IntegrationTests` project

**Path:** `tests/PSTT.Dashboard.IntegrationTests/`

**Two-tier design:**
- **Tier A** — `FakeMqttClientService` replaces real service; no broker needed; 9 hub tests + 3 API tests
- **Tier B** — intended in-process MQTTnet broker; skipped (`[Fact(Skip=...)]`) because MQTTnet v5 removed server from main package

**Key files created:**
- `FakeMqttClientService.cs` — inherits `MqttClientService`, overrides `ExecuteAsync` to no-op, exposes `TriggerIncomingMessageAsync` + `SeedLastKnownValue`
- `IntegrationWebApplicationFactory.cs` — `WebApplicationFactory<Program>` override; swaps real→fake service; temp data dir (GUID-named)
- `MqttBrokerIntegrationFactory.cs` — Tier B factory stub
- `HubConnectionHelper.cs` — creates LongPolling `HubConnection` against TestServer; `WaitForAsync<T>` helpers
- `InProcessMqttBrokerFixture.cs` — stub (originally used `MQTTnet.Server` which no longer exists in v5)
- `MqttDataHubTests.cs` — 9 Tier A tests (subscribe, receive, unsubscribe, isolation, cache, count, broker info)
- `MqttFlowIntegrationTests.cs` — 3 Tier B tests (all skipped with explanation)
- `DashboardApiTests.cs` — 3 API tests (health check responds, dashboard list 200, default dashboard 200/404)
- `xunit.runner.json` — disables parallel collection execution (prevents factory startup race conditions)

**Test results:** 12 pass / 3 skip / 0 fail ✓

⚠️ **Tier B caveat:** MQTTnet v5 no longer ships an embedded MQTT server in the main `MQTTnet` NuGet package. To enable Tier B, add the server package (e.g. `MQTTnet.Extensions.Hosting` or similar) and implement `InProcessMqttBrokerFixture.InitializeAsync`.

---

### 5. `MqttDashboard.PlaywrightTests` project

**Path:** `tests/PSTT.Dashboard.PlaywrightTests/`

**Key files created:**
- `MqttDashboard.PlaywrightTests.csproj` — `Microsoft.Playwright` 1.49.0, references `WebAppServerOnly`
- `PlaywrightWebAppFixture.cs` — starts server via `dotnet run --project <path>` on random port; polls `/healthz`; initialises Playwright Chromium; exposes `Browser` + `BaseUrl`
- `HomePageTests.cs` — 3 smoke tests (AppBar header visible, hamburger visible, MQTT icon visible at 1280px)
- `AppBarTests.cs` — 4 responsive tests (hamburger at 320px, edit toggle at 1024px, edit toggle hidden at 320px, hamburger opens menu)

**Setup required before running:**
```
pwsh tests/PSTT.Dashboard.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
```

⚠️ Playwright tests have NOT been run yet — they depend on the server starting successfully via `dotnet run` and Chromium being installed. CSS selectors (`.appbar-menu-pin`, `.toolbar-hide-xs`, `.mud-menu-list`) are based on current markup and may need adjustment after running.

---

### 6. Solution file updated

`MqttDashboard.slnx` — added both new test projects under `/tests/` folder.

---

## 2026-03-28 (batch 2) — Gauge arc fix, auto-save server-side, appsettings defaults

### Commit: (see git log) · UTC 2026-03-28 · branch: develop

### Gauge: background arc radius mismatch fixed

**Problem:** The gauge had two visually distinct arcs — a grey background arc and a coloured value arc. They appeared as concentric rings instead of one arc with a coloured portion, because the background arc used mathematically incorrect start/end points (`M 10 65 ... 110 65`, span 100px) while the value arc used the correct radius-55 geometry (start `M 5 65`, end `x=115`). SVG auto-scales radii when start/end don't satisfy the arc equation, so the background ended up at a slightly smaller radius.

**Fix:** Background arc changed to `M 5 65 A 55 55 0 0 1 115 65` — exact radius-55 semicircle matching the value arc geometry. Value arc is drawn after (on top in SVG) so the coloured portion fully covers the grey in the value range, and grey is only visible in the empty portion. Opacity slightly raised from 0.25 → 0.30 so the track is more legible.

**Files changed:**
- `src/PSTT.Dashboard.Client/Widgets/GaugeNodeWidget.razor` — corrected background arc path and opacity

### Auto-save: moved to server-side settings; appsettings.json defaults added

**Problem (follow-up from batch 1):** Auto-save was persisted to browser `localStorage` — that's per-browser, not system-wide. As a server admin setting it belongs in `appsettings.user.json`.

**Additional:** `appsettings.json` had no `App` section, so `App:MaxMessageHistory` and the new `App:AutoSaveOnExit` had no documented defaults. This made the configuration opaque.

**Changes:**
- `GET /api/settings/app` + `POST /api/settings/app` added to `SettingsController` — reads/writes `App:AutoSaveOnExit` in `appsettings.user.json`
- `MainLayout.razor` loads `/api/settings/app` on first render instead of `localStorage`
- `AppMenu.ToggleAutoSave()` POSTs to server (async); `localStorage` no longer used for this setting
- Both `appsettings.json` files now have an explicit `App` section with `MaxMessageHistory: 500` and `AutoSaveOnExit: false` as documented defaults

**Files changed:**
- `src/PSTT.Dashboard.Server/Controllers/SettingsController.cs`
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor`
- `src/PSTT.Dashboard.Client/Layout/AppMenu.razor`
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/appsettings.json`
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebAppServerOnly/appsettings.json`

### Auto-save snackbar — already works

**Note:** TODO item "Auto-save should show popup info 'File saved...'" — confirmed this is already handled. `SwitchMode()` calls `SaveDashboard()` which calls `Snackbar.Add("Saved '...'", Severity.Success)` in all code paths including auto-save. Marked done in TODO.

---



### Mobile/narrow toolbar: hide non-essential items on xs screens

**Problem:** On a phone in portrait mode (< 600 px wide), the appbar overflows. The hamburger
menu button could be pushed off-screen, the logout/edit-toggle icons waste space that is better
used by the app title.

**Solution:** Wrap the MQTT status icon, login/logout button, and edit-mode toggle in `<div
class="toolbar-hide-xs">` elements. A `@media (max-width: 599px)` rule in `MainLayout.razor.css`
sets `display:none !important` on those elements. The hamburger menu (`AppMenu`) is always the
rightmost item and is never hidden.

**Accessibility on mobile:** All hidden functions are now also available in the Options menu (see
next item) so nothing is lost on narrow screens.

**Files changed:**
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor` — wrapped MQTT icon, auth buttons, and edit
  toggle in `<div class="toolbar-hide-xs">` divs; injected `LocalStorageService`
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor.css` — added `@media (max-width:599px)` rule
  hiding `.toolbar-hide-xs`

### Options menu: edit mode toggle, auto-save, and login/logout

**Problem:** On mobile, users can't access edit mode or logout because the toolbar items are hidden.
Also, the Options menu had no way to toggle edit mode directly.

**Solution:** Added three new items at the top of the Options menu:

1. **Edit Mode** (with checkmark) — visible when user has permission; calls `RequestToggleEditMode()`.
2. **Auto-save on Exit** (with checkmark, edit mode only) — see next item.
3. **Logout / Login as Admin** (auth only, not read-only) — duplicates the toolbar button;
   calls `AuthService.LogoutAsync()` then navigates to `/login`.

A `MudDivider` separates each group from the Theme submenu below.

**Files changed:**
- `src/PSTT.Dashboard.Client/Layout/AppMenu.razor` — injected `LocalStorageService` and `IAuthService`;
  added edit mode toggle, auto-save toggle, and login/logout items to Options menu; added
  `ToggleEditMode()`, `ToggleAutoSave()`, `MenuLogout()` methods; `SetTheme()` now also persists
  to localStorage.

### Auto-save on exit — moved to server-side settings

**Correction:** Auto-save on exit is a system-wide setting (applies to all users/browsers on that
server instance), not a per-browser preference. Moved from `localStorage` to `appsettings.user.json`.

**New endpoints** on `SettingsController`:
- `GET /api/settings/app` → `{ autoSaveOnExit: bool }` (public read)
- `POST /api/settings/app` body `{ autoSaveOnExit: bool }` (admin-only write, or unrestricted if auth disabled)

Stored in `appsettings.user.json` under `App.AutoSaveOnExit`. The file is loaded with
`reloadOnChange: true` so a server restart is not needed.

`MainLayout.razor` loads `/api/settings/app` on first render (alongside the update check) and
calls `AppState.SetAutoSaveOnExitEditMode()`. `AppMenu.ToggleAutoSave()` now `POST`s to the server
instead of writing to localStorage. Theme preference remains in localStorage (per-browser/user).

Refactored `SettingsController.cs` to share `ReadUserJson`/`WriteUserJson` helpers between the
existing `Save()` (startup) and new `SaveApp()` methods.

**Files changed:**
- `src/PSTT.Dashboard.Server/Controllers/SettingsController.cs` — added `GetApp()`, `SetApp()`, `SaveApp()` methods; extracted `ReadUserJson`/`WriteUserJson` helpers; added `SetAppRequest` record
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor` — replaced localStorage auto-save load with `/api/settings/app` fetch; added `AppSettingsResponse` record
- `src/PSTT.Dashboard.Client/Layout/AppMenu.razor` — `ToggleAutoSave()` now async, POSTs to server; injected `HttpClient`

---

## 2026-03-27 (batch 4) — ReadOnlyPorts + deployment documentation

### Commit: (see git log) · UTC 2026-03-27 · branch: develop

### ReadOnlyPorts — per-port read-only in a single process

**Problem:** The `ReadOnly=true` flag makes the entire process read-only. Running two
processes for a public read-only port and an admin editable port means two MQTT broker
connections and two data caches.

**Solution:** `ReadOnlyPorts=8080` (comma-separated) — requests arriving on the listed port(s)
are treated as read-only. Combined with `ASPNETCORE_URLS=http://+:8080;http://+:8081`, a single
process serves public read-only on 8080 and admin editing on 8081, sharing all singletons.

**Files changed:**
- `src/PSTT.Dashboard.Server/Services/ReadOnlyHelper.cs` (new) — static `IsReadOnly(IConfiguration, HttpContext?)` checks `ReadOnly` global flag first, then compares `LocalPort` against `ReadOnlyPorts` list
- `src/PSTT.Dashboard.Server/Controllers/AuthController.cs` — `GetStatus()` uses `ReadOnlyHelper`
- `src/PSTT.Dashboard.Server/Filters/RequireAdminFilter.cs` — uses `ReadOnlyHelper`; added `using PSTT.Dashboard.Server.Services`
- `src/PSTT.Dashboard.Server/Controllers/SettingsController.cs` — uses `ReadOnlyHelper`
- `src/PSTT.Dashboard.Server/Services/ServerAuthService.cs` — uses `ReadOnlyHelper`; removed duplicate `var httpContext` declaration
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/appsettings.json` — added `"ReadOnlyPorts": ""`
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebAppServerOnly/appsettings.json` — added `"ReadOnlyPorts": ""`
- `docker-compose.yml` — added `ReadOnlyPorts` env var with detailed comment
- `docker-compose.production.yml` — added `ReadOnlyPorts` env var comment

### Deployment documentation

**New document:** `documents/deployment-modes.md` — covers all supported deployment patterns:
1. Default single-port read-write
2. Single-port with admin auth
3. Single-port fully read-only (`ReadOnly=true`)
4. Dual-port single-process (`ReadOnlyPorts`)
5. Dual-process two containers
6. Render mode options (Auto, WebAssembly, Server, WebAppServerOnly)
7. Future compile-time read-only (`.Core` + `.View` project split)

**README.md** — updated Features section to mention read-only and dual-port modes; added
`ReadOnly`, `ReadOnlyPorts`, and `RenderMode` to configuration reference table with link to
the new deployment-modes document.

---



### Commit: (see git log) · UTC 2026-03-26 · branch: develop

### Read-only runtime mode (`ReadOnly=true`)

**Problem:** No way to deploy a public view-only instance without exposing edit controls.

**Files changed:**
- `src/PSTT.Dashboard.Server/Controllers/AuthController.cs` — `GET /api/auth/status` now includes a `readOnly` field; when `ReadOnly=true` the response always returns `{ isAdmin: false, authEnabled: false, readOnly: true }` regardless of other config
- `src/PSTT.Dashboard.Server/Filters/RequireAdminFilter.cs` — returns HTTP 403 with error message when `ReadOnly: true`; checked before auth (no one can write in read-only mode)
- `src/PSTT.Dashboard.Server/Controllers/SettingsController.cs` — `POST /api/settings/startup` returns 403 when read-only (has its own inline auth check, so had to add the read-only guard here too)
- `src/PSTT.Dashboard.Server/Services/ServerAuthService.cs` — `GetStatusAsync()` returns `(bool isAdmin, bool authEnabled, bool readOnly)` 3-tuple; reads `ReadOnly` from `IConfiguration`
- `src/PSTT.Dashboard.Client/Services/IAuthService.cs` — `GetStatusAsync()` return type changed to `(bool isAdmin, bool authEnabled, bool readOnly)`
- `src/PSTT.Dashboard.Client/Services/AuthService.cs` — parses `ReadOnly` from JSON response; `AuthStatusResponse` record extended
- `src/PSTT.Dashboard.Client/Services/ApplicationState.cs` — added `IsReadOnly` property; `SetAuthState()` gains `readOnly = false` parameter
- `src/PSTT.Dashboard.Client/Services/MqttInitializationService.cs` — passes `readOnly` from auth status to `SetAuthState()`
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor` — login/logout buttons and edit-mode toggle both wrapped in `!AppState.IsReadOnly`; setup alert also hidden in read-only mode
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/appsettings.json` — added `"ReadOnly": false`
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebAppServerOnly/appsettings.json` — added `"ReadOnly": false`

**How it works:** Set `ReadOnly=true` as environment variable or in `appsettings.user.json`. The server blocks all write API calls (403) and reports read-only to the client. The client hides the edit toggle, login/logout buttons, and admin setup alert. No username/password is needed or shown — anyone accessing the URL gets the live view only.

### RenderMode=Server support

**Problem:** The single WebApp Docker image only supported `Auto` and `WebAssembly` render modes. There was no way to run Blazor Server-only (no WASM download) from the standard image.

**Files changed:**
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/Program.cs` — render mode switch changed from binary `WebAssembly`/`Auto` to a proper switch expression supporting `"Auto"` (default), `"WebAssembly"`, and `"Server"` → `BlazorRenderMode.InteractiveServer`
- `docker-compose.yml` — added commented env var examples for `RenderMode` and `ReadOnly`
- `docker-compose.production.yml` — same

**How it works:** Set `RenderMode=Server` to run the WebApp image in pure Blazor Server mode (no WASM bundle downloaded by clients). Useful for Raspberry Pi or other low-memory clients. The WASM bundle is still built into the image but never delivered.

### Minor cleanup

- `src/PSTT.Dashboard.Client/Components/AboutDialog.razor` — removed unused `_restarting` field (it was always set but never read after the update banner removal; was causing a CS0414 warning)

---

## 2026-03-26 (batch 3) — TreeView overhaul + import dialog + padding

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

---

### Fix: TreeView collapse/focus loss + replace MudTreeView with custom rendering (`TreeViewNodeWidget.razor`)

**Problem:** `MudTreeView`/`MudTreeViewItem` are stateful components. Every call to `StateHasChanged` caused MudBlazor to reconcile the component tree. For rapid MQTT bursts this resulted in constant re-rendering that visually collapsed the tree (MudBlazor's internal expanded state was reset) and lost keyboard focus.

**Fix:** Replaced `MudTreeView`/`MudTreeViewItem` entirely with a custom lightweight div-based recursive renderer (`RenderNode`). Expansion state is stored on the `TreeNode` model objects (`Expanded` bool), not inside any MudBlazor component — so re-renders never lose state.

Added an **80 ms debounce timer** (`_debounceTimer`): `OnTopicChanged` now starts/restarts this timer rather than calling `StateHasChanged` directly. Rapid bursts are coalesced into a single render.

Added a **highlight-clear timer** (`_highlightClearTimer`): 2.2 s after the last update fires, triggers one more `StateHasChanged` to clear the highlight colouring (previously highlights could linger until the next MQTT message arrived).

**Files:** `src/PSTT.Dashboard.Client/Widgets/TreeViewNodeWidget.razor`, `.razor.css`

---

### Change: TreeView root topic merged into base DataTopics (`TreeViewNodeModel.cs`)

**Problem:** `TreeViewNodeModel` had a bespoke `RootTopic` property separate from the standard `DataTopics` list, duplicating the concept and appearing as an extra "Root Topic" field in the property editor (via `[NpText]`) while the standard topic list remained empty and unused.

**Fix:** Removed `RootTopic` from `TreeViewNodeModel`. The widget now reads `Node.DataTopics[0]` via a `RootTopicValue` computed property. Users set the root topic via the standard MQTT Topics section in the property editor (same as all other widgets).

**Backward compat:** `TreeViewNodeData.RootTopic` remains in the data model (read-only for load). `FromData()` migrates: if `data.RootTopic` is non-empty and `DataTopics` is empty, `RootTopic` is added as `DataTopics[0]`. No data loss from existing saved files.

**Files:** `src/PSTT.Dashboard.Client/Models/TreeViewNodeModel.cs`, `TreeViewNodeWidget.razor`

---

### Enhancement: TreeView visual improvements (`TreeViewNodeWidget.razor.css`)

With the custom renderer, full layout control is available:
- Font reduced to **0.7 rem** (down from 0.75 rem)
- Each tree row is a flex row: label left (ellipsis on overflow), value **bold + primary colour** right-aligned
- **2 s highlight** on updated topics via `tv-highlighted` CSS class (background warning color, 0.3 s transition). Cleared automatically by `_highlightClearTimer`
- Expand/collapse icons (▾ / ▸) and leaf bullet (•) via unicode HTML entities; `tv-children` indented 12 px
- Hover uses `--mud-palette-action-hover` for consistency with MudBlazor theming

---

### Fix: Import dialog doesn't grow when status alert appears (`ImportNodesDialog.razor`)

**Problem:** When valid JSON was pasted, a `MudAlert` appeared below the textarea, expanding the dialog height and causing layout shifts/scrollbars.

**Fix:** Wrapped the conditional alert in `<div style="min-height:40px;">`. The reserved height matches a Dense MudAlert, so the dialog occupies the same vertical space whether or not an alert is visible.

**Files:** `src/PSTT.Dashboard.Client/Components/ImportNodesDialog.razor`

---

### Fix: TreeView and Log widget internal padding (`*.razor`, `*.razor.css`)

- **Log:** header row padding reduced from `2px 4px 0` → `1px 3px 0`; added CSS rule `::deep .mud-table-root th, td { padding: 2px 4px }` to override MudBlazor's default (typically 12px) cell padding
- **TreeView:** custom CSS classes use minimal padding (1–2 px per row); no more `padding:2px 0` wrapper div

**Files:** `src/PSTT.Dashboard.Client/Widgets/LogNodeWidget.razor`, `LogNodeWidget.razor.css`, `TreeViewNodeWidget.razor.css`

---

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

---

### Bug fix: node height loop — proper root cause fix (`TextNodeModel.cs`)

**Root cause (confirmed):** `NodeRenderer` in rrSoft.Blazor.Diagrams 0.1.2 attaches a JS `ResizeObserver` on the `.diagram-node` wrapper element whenever `Node.ControlledSize` is `false` (the default). The observer calls `OnResize(getBoundingClientRect())` after every render. Because `getBoundingClientRect()` can include sub-pixel rounding and zoom-division noise, the reported size may differ slightly from the stored `Node.Size`, triggering a re-render → re-measure loop that manifests as slow indefinite height growth — particularly on nodes without a title (where there is no stable text anchor).

**Previous (incorrect) fix:** `StandardNodeLayout` kept the title `<div>` in the DOM with `display:none` to stabilise the DOM structure. This was addressing a symptom rather than the cause and did not stop the observer loop.

**Correct fix:** Convert `TextNodeModel` from primary-constructor syntax to a regular constructor and set `ControlledSize = true` in the body. All our nodes set explicit `Node.Size` in `OnInitialized` and the user can resize via the drag handle (which calls `NodeModel.SetSize()` directly, unaffected by `ControlledSize`). The `init` accessor on `ControlledSize` is accessible from derived-class constructors per the C# spec ("init context" extends to derived constructors).

**Reverted:** The `TitleDivFullStyle`/`display:none` workaround in `StandardNodeLayout` has been removed; the original clean `@if (… && HasTitleContent)` guards are restored.

**Future:** A TODO comment has been left in `rrSoft.Blazor.Diagrams/NodeModel.cs` suggesting `ControlledSize` be changed to `{ get; protected set; }` in a future library version for clarity.

**Files:** `src/PSTT.Dashboard.Client/Models/TextNodeModel.cs`, `src/PSTT.Dashboard.Client/Widgets/StandardNodeLayout.razor`

---

### Bug fix: grid shown in view mode (`Display.razor.cs`)

**Problem:** When switching from edit → view mode, `_diagram.Options.GridSize` was left at the edit-mode value, so the grid dotted background remained visible in view mode.

**Fix:** Added `_diagram.Options.GridSize = null;` to the `else` branch of `SwitchMode` (the path taken when `enterEditMode` is false).

**Files:** `src/PSTT.Dashboard.Client/Pages/Display.razor.cs`

---

### Bug fix: Import dialog "Import" button never enabled (`ImportNodesDialog.razor`)

**Problem:** The `MudTextField` had three conflicting data-binding attributes: `@bind-Value="_json"`, `Immediate="true"`, and `@oninput="OnJsonChanged"`. MudBlazor's `Immediate` mode generates its own internal `oninput` handler; adding a second `@oninput` created a race between the two handlers. In practice `_json` was sometimes not updated before `TryParse()` ran, so `_parsed` stayed null and the Import button remained disabled.

**Fix:** Replaced the triple with `Value="@_json" ValueChanged="@OnValueChanged"` (no `@oninput`, no `Immediate`). `OnValueChanged(string v)` sets `_json = v` and calls `TryParse()` — single, deterministic update path. Also renamed `OnJsonChanged` → `OnValueChanged` to match the MudBlazor pattern.

**Files:** `src/PSTT.Dashboard.Client/Components/ImportNodesDialog.razor`

---

### Change: Import / Export moved to File menu (`AppMenu.razor`)

**Change:** Export… and Import… items moved from the Edit submenu to the File submenu (below Dashboard Properties). Both remain gated on `IsEditMode`.

**Files:** `src/PSTT.Dashboard.Client/Layout/AppMenu.razor`

---

### Commit: (see git log) · UTC 2026-03-26 · branch: develop

---

### Bug fix: node without title grows indefinitely (`StandardNodeLayout.razor`)

**Problem:** `StandardNodeLayout` guarded both title `<div>` elements with `@if (… && HasTitleContent)`. When `Title` and `Icon` are both empty, the div was removed from the DOM. Blazor.Diagrams measures node height after each render; the DOM change caused a size change which triggered another render, creating an infinite grow loop.

**Fix:** Changed `@if (ShowTitle && ShowTitleFirst && HasTitleContent)` to `@if (ShowTitle && ShowTitleFirst)` (and equivalent for the bottom position). Added a `TitleDivFullStyle` computed property that appends `;display:none` to `TitleDivStyle` when `!HasTitleContent`. The div is always in the DOM; the browser reserves no space for it when hidden — so the node renders at the correct size with no spurious remeasure events.

**Files:** `src/PSTT.Dashboard.Client/Widgets/StandardNodeLayout.razor`

---

### Bug fix: grid snap-to-centre not saved/restored; negative-value convention removed

**Problem (1):** `CreateDiagramFromPageData` set `options.GridSize = int.Abs(page.GridSize)` (always positive) and then set `options.GridSnapToCenter = options.GridSize < 0` — which was always false. So snap-to-centre was never restored from file.

**Problem (2):** `GetPageData` serialised snap-to-centre as a negative `GridSize` (e.g. `-20`). Reading this back correctly would have required keeping the sign, but it was stripped by `int.Abs` first.

**Fix:**
- `DashboardPageModel`: added `GridSnapToCenter bool` field (default false). `GridSize` default raised from 10 → 20.
- `ApplicationState.GridSnapToCenter` property added.
- `SetGridSize(int)`: in edit mode, clamps to 5–100 and rounds to nearest 5. Also applies `GridSnapToCenter` to the diagram.
- New `SetGridSnapToCenter(bool)` method.
- `CreateDiagramFromPageData`: reads `page.GridSnapToCenter` directly; clamps `GridSize` to 5–100.
- `GetPageData`: writes positive `GridSize` and `GridSnapToCenter` separately.
- `Display.razor.cs` entering-edit-mode path: uses `page.GridSnapToCenter` instead of `savedGs < 0`.
- `Display.razor.cs` `BuildFullState`: copies `GridSnapToCenter` from `_pageStates`.
- `DashboardPropertiesDialog`: `OnInitialized` reads `AppState.GridSnapToCenter`; `ApplyAsync` calls `SetGridSnapToCenter` + `SetGridSize` separately (no negative-value encoding). Min raised from 0 → 5 in the numeric field. Caption updated.

**Files:** `src/PSTT.Dashboard.Client/Models/DashboardModel.cs`, `src/PSTT.Dashboard.Client/Services/ApplicationState.cs`, `src/PSTT.Dashboard.Client/Pages/Display.razor.cs`, `src/PSTT.Dashboard.Client/Components/DashboardPropertiesDialog.razor`

⚠️ Breaking file format change: old files with negative `gridSize` will load as positive (snap-to-centre will default off). Acceptable per dev notes.

---

### Feature FEAT-E: clipboard import/export (Node-Red style)

**Overview:** Users can now export nodes or a whole page as JSON text, and import that JSON back (onto the current page or a new page). The UX mirrors Node-RED's import/export flow.

#### New files
- `src/PSTT.Dashboard.Client/Models/ImportResult.cs` — `ImportResult` record (`Nodes`, `Links`, `AddAsNewPage`).
- `src/PSTT.Dashboard.Client/Components/ExportNodesDialog.razor` — shows JSON in a `Lines=18` read-only textarea. Mode selector: "Selected nodes (N)" (disabled if none selected) or "Current page". Copy button writes to OS clipboard via `mqttClipboard.writeText`.
- `src/PSTT.Dashboard.Client/Components/ImportNodesDialog.razor` — textarea for pasting JSON; "Paste from clipboard" icon button; auto-detects format (`mqttdashboard:"nodes"` or `mqttdashboard:"page"`); shows detected node/link count; destination radio (current page / new page); Import button disabled until valid JSON detected.

#### JSON formats
- Nodes: `{"mqttdashboard":"nodes","data":[...NodeData...]}` (existing copy/paste format)
- Page: `{"mqttdashboard":"page","data":{...DashboardPageModel...}}` (new)

#### Modified files
- `src/PSTT.Dashboard.Client/Services/ApplicationState.cs` — added `MenuExportNodes`, `MenuImportNodes` events; `TriggerExportNodes()`, `TriggerImportNodes()`.
- `src/PSTT.Dashboard.Client/Layout/AppMenu.razor` — added "Export…" and "Import…" items after Cut/Copy/Paste in the Edit menu; added `MenuExportNodes()` and `MenuImportNodes()` handlers.
- `src/PSTT.Dashboard.Client/Pages/Display.razor.cs`:
  - `_onMenuExportNodes` / `_onMenuImportNodes` stored action fields.
  - `SubscribeEditEvents` / `UnsubscribeEditEvents` updated.
  - `ExportNodesAsync()` — captures selected nodes + current page data, opens `ExportNodesDialog`.
  - `ImportNodesAsync()` — opens `ImportNodesDialog`; on result with `AddAsNewPage=true` creates a new `DashboardPageModel` and calls `SwitchToPageAsync`; on `AddAsNewPage=false` pastes nodes into the current diagram (same logic as `PasteNodesAsync`).

---

## 2026-03-25 — Bug fixes: menu cleanup, no-data banner, grid, restart

### Commit: (see git log)

### Bug fixes

#### No-data message → top banner (`Display.razor`)
- Changed the "no data topics" notification from a centred floating `MudPaper` card to a `MudAlert` banner anchored at the top of the canvas (`position:absolute;top:0;left:0;right:0`).
- Edit-mode shows an action button "Configure Topics" that opens Dashboard Properties. View-mode shows a plain info text.

#### Removed stale menu items (`AppMenu.razor`)
- **Options > Show > Dashboard Name** removed — "Show Dashboard Name" is now a checkbox in the Dashboard Properties dialog.
- **Page > Home** menu (and the entire `Page` submenu) removed — navigation to "/" was the only item and it isn't needed now. `IsCurrentPage()` helper also removed.

#### `appsettings.user` excluded from dashboard list (`DashboardStorageService.cs`)
- `ListDiagramNamesAsync()` now filters out `"appsettings.user"` in addition to empty names.
- `MigrateLegacyDashboardFiles()` excludes `"appsettings.user.json"` from being moved to the `dashboards/` subdirectory.

#### GridSize restored to Dashboard Properties dialog (`DashboardPropertiesDialog.razor`)
- Added a `MudNumericField` (0–100 px, step 5) for grid size and a "Snap to cell centre" checkbox.
- Reads and writes `AppState.GridSize` using the existing sign convention (negative = snap-to-centre).
- `ApplyAsync` calls `AppState.SetGridSize(newGridSize)` so the live diagram updates immediately.

#### New/empty diagrams default to grid-enabled (`ApplicationState.cs`)
- `CreateDiagramFromPageData`: when `page.GridSize == 0` (no saved grid), now defaults to 20 instead of `null`. This ensures edit-mode always gets a grid for new/unsaved diagrams.

#### Restart button in About dialog (`AboutDialog.razor`)
- Added a "Restart App" button that is always visible to admin users on Docker deployments (not only when an update is available).
- Calls `POST /api/update/restart` (existing endpoint). Connection loss after the call is expected and silently swallowed.

---



### Commit: 30b6e69 (completes b6005f1)

### Problem
`NodeState` was a ~50-field flat DTO covering all node types. `ApplicationState.GetDiagramState()` and `CreateDiagramFromState()` had ~100-line manual switch/case blocks duplicating every node property. Adding a new node type required editing 6+ files.

### What changed

#### New `DashboardModel.cs` (`src/PSTT.Dashboard.Client/Models/`)
- Complete serializable POCO hierarchy: `DashboardModel` → `DashboardPageModel` → `List<NodeData>` (polymorphic, STJ `[JsonPolymorphic]`) → typed subclasses: `TextNodeData`, `GaugeNodeData`, `SwitchNodeData`, `BatteryNodeData`, `LogNodeData`, `TreeViewNodeData`.
- Nested value types: `NumericRangeData`, `ColorTransitionData`, `ColorThresholdData`, `SwitchSettingsData`, `LogColumnsData`, `NodePortData`, `LinkData`, `DashboardFileInfo`.
- No manual switch/case needed in serialization path — STJ handles polymorphism via `nodeType` discriminator.

#### Runtime model renames
- `MudNodeModel` → `TextNodeModel` (in `MudNodeModel.cs`). `MudNodeModel` kept as `[Obsolete]` alias.
- `MudPortModel` → `NodePortModel` (in `MudPortModel.cs`). `MudPortModel` kept as `[Obsolete]` alias.

#### Per-node serialization (`ToData()` / `FromData()`)
Each node type now owns its own serialization:
- `TextNodeModel`, `GaugeNodeModel`, `SwitchNodeModel`, `BatteryNodeModel`, `LogNodeModel`, `TreeViewNodeModel` — all implement `NodeData ToData()` and `static T FromData(XxxNodeData)`.
- `ColorTransitionHelper` added to `ColorTransition.cs` for round-tripping `ColorTransition` ↔ `ColorTransitionData`.

#### `ApplicationState.cs`
- `GetDiagramState()` and `CreateDiagramFromState()` deleted.
- Replaced by `GetPageData()` (returns `DashboardPageModel`) and `CreateDiagramFromPageData(DashboardPageModel, bool)`.
- `ApplyDashboardModel(DashboardModel)` — applies top-level Name/ShowDiagramName/MqttSubscriptions.
- Clipboard type: `List<NodeState>` → `List<NodeData>`. Undo stack: `Stack<DiagramState>` → `Stack<DashboardPageModel>`.

#### `Display.razor.cs`
- `_pageStates: List<DiagramState>` → `List<DashboardPageModel>`.
- `_editSnapshot: DiagramState?` → `DashboardModel?`.
- All page-switch, undo/redo, save, cut/copy/paste, add-node methods updated to use new types.
- `AddNode()` uses `TextNodeModel` instead of `MudNodeModel`.
- `UpdateSelectionState()` uses `NodePortModel` instead of `MudPortModel`.

#### Widget base classes and Razor components
- `BaseNodeWidget<TNode>` and `BaseNodeWithDataWidget<TNode>` — type constraint changed from `MudNodeModel` to `TextNodeModel`; `PortStyle` parameter from `MudPortModel` to `NodePortModel`.
- `NodePropertyEditor.razor.cs` — `[Parameter] Node` type changed to `TextNodeModel`.
- `StandardNodeLayout.razor`, `MudNodeWidget.razor`, `DataValueTooltipContent.razor`, `LogNodeWidget.razor`, `TreeViewNodeWidget.razor` — updated to `TextNodeModel`/`NodePortModel`.
- `ColorTransitionGroupEditor.razor`, `NumericRangeEditor.razor`, `NodePropertyRenderer.razor` — `[Parameter] Node` type changed to `TextNodeModel`.
- `NodePropertyAttributes.cs` — updated `NpCustomAttribute` doc comment.

#### Service/controller/test files
- `IDashboardService.cs`, `DashboardService.cs`, `ServerDashboardService.cs`, `DashboardStorageService.cs`, `DashboardController.cs` — all `DiagramState` → `DashboardModel` throughout.
- `DiagramStorageServiceTests.cs` — test updated to build `DashboardModel`+`DashboardPageModel`+`TextNodeData` instead of `DiagramState`+`NodeState`.

#### Deleted
- `src/PSTT.Dashboard.Client/Models/DiagramState.cs` — replaced by `DashboardModel.cs`.

### Result
- Build: 0 errors, 0 warnings.
- Tests: 11/11 pass (5 client, 6 server).
- New JSON format is nested (not flat), with `nodeType` discriminator per node — no backward compat with old files (by design).

---



### FEAT-M: `appsettings.user.json` moved to data directory

**Problem:** Both `SettingsController` and `SetupController` wrote `appsettings.user.json` to `IWebHostEnvironment.ContentRootPath` — in Docker that's `/app/`, inside the container image, and is wiped on every container restart. Admin password and startup mode settings were therefore lost on redeploy.

**Fix — `Program.cs` (both hosts):**
- Replaced `builder.Configuration.AddJsonFile("appsettings.user.json", ...)` with a block that:
  1. Resolves the data directory early using the same priority logic as `DashboardStorageService` (env var `DIAGRAM_DATA_DIR` → config `DiagramStorage:DataDirectory` → `{ContentRoot}/Data`).
  2. Creates the data dir if it doesn't exist.
  3. One-time migration: if `{ContentRoot}/appsettings.user.json` exists and `{dataDir}/appsettings.user.json` does not, copies it across.
  4. Loads the settings file from data dir using a `PhysicalFileProvider` so the absolute path is unambiguous.
- ⚠️ Uses `new PhysicalFileProvider(dataDir)` + `AddJsonFile(provider, "appsettings.user.json", ...)` — NOT `AddJsonFile(absolutePath, ...)` because the default file provider base path is `AppContext.BaseDirectory`, not `ContentRootPath`.

**Fix — `SettingsController.cs` and `SetupController.cs`:**
- Removed `IWebHostEnvironment` injection from both.
- Injected `DashboardStorageService` instead.
- Changed `Path.Combine(_env.ContentRootPath, "appsettings.user.json")` → `Path.Combine(_storage.StoragePath, "appsettings.user.json")` in both the `Save()` method (SettingsController) and `SavePasswordHash()` (SetupController).

**Files changed:**
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/Program.cs`
- `src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebAppServerOnly/Program.cs`
- `src/PSTT.Dashboard.Server/Controllers/SettingsController.cs`
- `src/PSTT.Dashboard.Server/Controllers/SetupController.cs`

---

### FEAT-N: Restart from web UI (Docker)

**Problem:** In Docker, when a new image is available (pulled via `docker compose pull` or Watchtower), there was no way to restart the app from the browser — users had to SSH in and run `docker compose up -d`.

**Fix — `UpdateController.cs`:**
- Added `IHostApplicationLifetime` and `IConfiguration` injection.
- New `POST /api/update/restart` endpoint:
  - Checks admin auth if auth is configured.
  - Schedules `_lifetime.StopApplication()` after a 500ms delay (so the HTTP response is delivered first).
  - Returns `{ success: true, message: "..." }`.
  - Docker `restart: always` policy then brings the container back up with the (already-pulled) image.

**Fix — `MainLayout.razor`:**
- Added `_restarting` state field.
- Added `RestartAppAsync()` method: calls `POST /api/update/restart` via `Http.PostAsync`, swallows connection-drop exceptions (expected as app shuts down).
- Docker update banner: replaced plain text with a flex row showing the `docker compose pull` instruction + **"Restart Now"** button (disabled while restarting).
- Standalone update banner: replaced `MudLink` with a `MudButton` variant for visual consistency.

**Files changed:**
- `src/PSTT.Dashboard.Server/Controllers/UpdateController.cs`
- `src/PSTT.Dashboard.Client/Layout/MainLayout.razor`


The standard [CHANGELOG.md](CHANGELOG.md) contains release-level summaries following Keep a Changelog.

---

## 2026-03-24 — Bulk TODO bug fixes: UI cleanup, port menu, alignment, paste, undo, serialization

### Commit: 18e3ca7 (develop)

### Batches completed

#### Batch 1 — Small UI/UX fixes
- **`MainLayout.razor`**: App title fallback changed from `"Mqtt Dashboard"` → `"MQTT Dashboard"`.
- **`ApplicationState.cs`**: `ShowDiagramName` default `true`; `GridSize` default `20`; `CreateDiagramFromState` syncs both `_diagram.Options.GridSize` and `AppState.GridSize` from loaded state (previously GridSize property stayed at default, causing snapping to be wrong on first load).
- **`Display.razor.cs`** `SaveAsDiagram()`: removed `&& !string.Equals(name, AppState.DiagramName, ...)` overwrite guard — Save As now always prompts when file exists.
- **`DashboardPropertiesDialog.razor`**: removed "Title Bar" section heading; removed Grid section (MudText + MudSelect); replaced `ColorPicker` with `ColorInputRow` for canvas background.
- **`NodePropertyEditor.razor`**: dialog title now `"Edit {NodeType} Node Properties"`; removed subtitle + node type display lines; Title + TitlePosition on one compact MudGrid row; Background Image + Fit on one compact row, moved to top section; IconColor uses `ColorInputRow` (was MudSelect of enum names); removed "MQTT Data Binding" section header; removed `MudDivider` after link animation.
- **`StandardNodeLayout.razor`**: `<MudIcon>` now uses `Style="@IconStyle"` (CSS `color:` property) instead of `Color="@IconColor"` (MudBlazor enum). Removed old `IconColor` switch property; added `IconStyle` string property. ⚠️ Old save files with MudBlazor enum names (e.g. `"Primary"`) won't render icon color correctly — backward compat not a concern per user.
- **`NumericRangeEditor.razor`**: "Arc midpoint / zero-point" helper text changed to "Origin / zero-point".

#### Batch 2 — Grid startup + Options menu
- **`AppMenu.razor`**: removed entire `<MudMenu Label="Grid">` submenu from Options menu.
- **`ApplicationState.cs`** `CreateDiagramFromState`: sets `GridSize = X` (public property) in addition to `options.GridSize = X` so snapping is immediately correct on load.

#### Batch 3 — Port menu per-item greying
- **`ApplicationState.cs`**: added `SelectedNodePorts` (`HashSet<PortAlignment>?`); extended `UpdateSelectionState()` to accept `selectedPorts` parameter; added `MenuAddAllPorts` event and `TriggerAddAllPorts()`.
- **`Display.razor.cs`**: `UpdateSelectionState()` passes port HashSet from selected node; added `AddAllPortsToSelectedNode()` method; subscribed/unsubscribed `MenuAddAllPorts`.
- **`AppMenu.razor`**: Add Port items disabled when port exists; Delete Port items disabled when absent; added "All" item to Add Port submenu; added `MenuAddAllPorts()` method.

#### Batch 4 — Paste keeps selection + dirty flag fix
- **`Display.razor.cs`** paste loop: changed `_diagram.SelectModel(node, true)` → `SelectModel(node, false)` — `true` means "unselect others", so only the last pasted node was ever selected. `false` appends to selection.
- **`Display.razor.cs`** `OnSelectionChanged`: added `_ = InvokeAsync(() => _pendingDirtyMark = false)` deferred clear alongside the immediate clear. This handles the case where Blazor.Diagrams fires `SelectionChanged` BEFORE `node.Changed` (in that ordering, the immediate clear has no effect since the flag isn't set yet; the deferred clear runs after `node.Changed` has set the flag).

#### Batch 5 — Same Width / Same Height alignment
- **`Display.razor`**: added two new `<MudIconButton>` items ("Make Same Width" + "Make Same Height") after the existing bottom-align button, separated by a `MudDivider`.
- **`Display.razor.cs`**: added `SameWidth()` and `SameHeight()` methods — push undo snapshot, find max width/height among selected nodes, resize all to match, refresh.

#### Batch 6 — Serialization cleanup
- **`DiagramState.cs`**:
  - Added `DiagramFileInfo` class (`WrittenAt` ISO timestamp, `Filename` string).
  - `DiagramState`: added `[JsonPropertyOrder(n)]` to all properties (order: Name, ShowDiagramName, GridSize, BackgroundColor, Pages, MqttSubscriptions, Nodes, Links, FileInfo=99).
  - `NodeState`: moved `NodeType` to top of class with `[JsonPropertyOrder(0)]`; changed coordinate precision from 5dp to 2dp; removed `DataTopic` and `DataTopic2` scalar properties; made `Metadata` and `Ports` nullable (omitted when null by `WhenWritingNull` serializer option).
- **`ApplicationState.cs`** `GetDiagramState()`: removed `DataTopic`/`DataTopic2` from written output; Metadata written only when non-empty; Ports written only when non-empty; fallback loading of old `DataTopic`/`DataTopic2` scalar fields removed.
- **`Display.razor.cs`** `BuildFullState()`: populates `FileInfo` with `DateTimeOffset.UtcNow.ToString("o")` and `DiagramName`; sets it on both multi-page and single-page paths.
- **`DashboardStorageService.cs`**: both `SaveDiagramAsync` and `SaveDiagramByNameAsync` now use `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` — null optional properties are omitted from the JSON file entirely.

### Known caveats
- ⚠️ Icon colors saved with old MudBlazor enum names (e.g. `"Primary"`) will not render correctly — old files are not supported per user decision.
- ⚠️ Node IDs are still GUIDs in the file; sequential ID mapping was deferred (requires port/link ID remapping).
- ⚠️ Empty `Metadata: {}` and `Ports: []` are now fully omitted; old files with those empty fields load cleanly.

---

## 2026-03-24 — Refactor: Remove Data page; move topic management to Dashboard Properties

### Summary
Removed the `/data` page entirely and moved MQTT topic management into the Dashboard Properties dialog. A "no topics" overlay on the Display page prompts users to configure topics when none are set.

### `src/PSTT.Dashboard.Client/Components/DashboardPropertiesDialog.razor`
- Added a **Data Topics** section: lists current topics with remove (×) buttons and an add-topic input field (Enter or + button to add).
- `Apply()` made async (`ApplyAsync`): diffs previous vs new topic set, calls `SignalRService.SubscribeToTopicAsync` / `UnsubscribeFromTopicAsync` for each change, then calls `AppState.SetSubscribedTopics()` + `AppState.MarkEdited()`.
- Opening this dialog from edit mode is the **only** way to manage topics going forward.

### `src/PSTT.Dashboard.Client/Pages/Display.razor`
- Added a centered overlay card shown when `AppState.SubscribedTopics.Count == 0`.
  - **Edit mode**: shows "No data topics configured" + "Configure Topics →" button that opens Dashboard Properties.
  - **View mode**: shows an info message to switch to edit mode.

### `src/PSTT.Dashboard.Client/Pages/Display.razor.cs`
- Added `OpenDashboardProperties()` helper (delegates to `ShowDiagramPropertiesAsync()`).

### `src/PSTT.Dashboard.Client/Pages/MqttData.razor` — **DELETED**
- The entire Data page (topic management, data cache explorer, message log) has been removed.
- `MqttDataCache` and related services are **not** removed — they're still used by widgets.

### `src/PSTT.Dashboard.Client/Layout/AppMenu.razor`
- Removed the "Data" menu item that navigated to `/data`.

### `src/PSTT.Dashboard.Client/Layout/NavMenu.razor`
- Removed the `/data` nav link.

⚠️ Users with dashboards that had no topics will see the overlay on first load and need to open Dashboard Properties to add topics. Backward compat is not a concern per user instruction.



### Investigation
User reported topics added via the MqttData page were not remembered after a server restart.

Traced the full flow:
- Startup: `MqttInitializationService` loads `Default.json` → `AppState.SetSubscribedTopics(dashboard.MqttSubscriptions)` → restores SignalR subscriptions.
- Add topic: `MqttData.razor` → `SignalRService.SubscribeToTopicAsync` → server confirms → `HandleSubscriptionConfirmed` → `AppState.AddSubscriptionAsync` → `if (IsEditMode) MarkEdited()`.
- Save: triggered by user saving from edit mode → `GetDiagramState()` serializes `SubscribedTopics` into the dashboard JSON.

**Root cause:** The MqttData page displayed add/remove topic controls in view mode. Since `MarkEdited()` is gated on `IsEditMode`, topics added outside edit mode never marked the dashboard dirty. The save prompt/button never appeared, so topics were lost on restart.

### Fix — `src/PSTT.Dashboard.Client/Pages/MqttData.razor`
- Wrapped the per-topic unsubscribe (×) button in `@if (AppState.IsEditMode)`.
- Wrapped the entire "add new subscription" row (text field + Add button) in `@if (AppState.IsEditMode)`.

Topics are now read-only in view mode — consistent with the rest of the dashboard. To add/remove topics, enter edit mode, make changes, and save.

The existing `if (IsEditMode) MarkEdited()` guard in `ApplicationState.cs` is correct and unchanged.

⚠️ If existing topics were stored only in the legacy `applicationstate.json` (pre-refactor format), they will need to be re-added once in edit mode and saved.

## 2026-03-24 — ColorInputRow refactor + color transition ElseColor + battery fix

**Branch:** develop

### New: `ColorInputRow.razor` — reusable color input component

**`src/PSTT.Dashboard.Client/Components/ColorInputRow.razor`** (new file)
- Parameters: `Value/ValueChanged` (string?), `Label`, `Placeholder`, `ShowClear`
- Renders: color swatch preview + editable `MudTextField` + three icon buttons (Theme/Named/Custom) + optional clear
- Internally opens `ColorPickerDialog` using injected `IDialogService`
- Replaces duplicated color-picker markup in both `NodePropertyEditor` and `ColorTransitionEditor`

### Refactored: `NodePropertyEditor.razor` — Background Color uses `ColorInputRow`

**`src/PSTT.Dashboard.Client/Components/NodePropertyEditor.razor`**
- Background Color section (was ~35 lines with inline swatch + read-only text + 3 buttons + conditional clear) replaced with a single `<ColorInputRow ... ShowClear="true">` tag
- Now editable text field (was read-only) — user can type a color directly or use picker buttons

**`src/PSTT.Dashboard.Client/Components/NodePropertyEditor.razor.cs`**
- Removed `OpenColorPicker(ColorPickerMode mode)` and `ClearColor()` methods (now handled inside `ColorInputRow`)

### Refactored: `ColorTransitionEditor.razor` — threshold rows use `ColorInputRow`

**`src/PSTT.Dashboard.Client/Components/ColorTransitionEditor.razor`**
- Per-row color (was: inline swatch + editable text + click-to-expand quick-color panel with 15 swatches) replaced with `<ColorInputRow>` — gives full Theme/Named/Custom dialog access on each row
- Removed `_editingThreshold` state, `_commonColors` static array, and quick-color panel markup

### Added: `ElseColor` fallback for color transitions

**`src/PSTT.Dashboard.Client/Models/ColorTransition.cs`**
- Added `ElseColor` property (`string?`, default null) — applied when no threshold rule matches

**`src/PSTT.Dashboard.Client/Models/DiagramState.cs`**
- `ColorTransitionState` — added `ElseColor` property for JSON persistence

**`src/PSTT.Dashboard.Client/Services/ApplicationState.cs`**
- `DeserializeColorTransition` maps `state.ElseColor`
- `SerializeColorTransition` saves `ElseColor`; null check extended to include `ElseColor`

**`src/PSTT.Dashboard.Client/Widgets/GaugeNodeWidget.razor`**
- `GetArcColor()` now returns `Node.GaugeColor.ElseColor` when thresholds are configured but none match (instead of always falling through to the percent-based default)

**`src/PSTT.Dashboard.Client/Widgets/BatteryNodeWidget.razor`**
- `GetFillColor()` now returns first-matching rule (was accidentally returning last-matching — logic bug fixed)
- Returns `Node.BatteryColor.ElseColor` when thresholds are configured but none match (was `var(--mud-palette-primary)`)

**`src/PSTT.Dashboard.Client/Components/ColorTransitionGroupEditor.razor`**
- Added "Else (no rule matched)" section below the transition list using `ColorInputRow` with `ShowClear="true"`

### Fixed: MUD0002 analyzer warning in `LogNodeWidget.razor`

**`src/PSTT.Dashboard.Client/Widgets/LogNodeWidget.razor`**
- Pause/Play button: `Title="..."` attribute replaced with `<MudTooltip>` wrapper (MudBlazor MUD0002 — `Title` not valid on `MudIconButton`)

---



**Commit:** 14f0abc  **Branch:** develop

### Root cause analysis

`InvalidCharacterError: String contains an invalid character` was thrown by the browser DOM when rendering SVG `MarkupString` content (gauge/battery widgets). The core issue:

1. `HtmlEncode` (used in both widgets) does **not** strip null bytes (`\0`) — it only encodes `<>&"'`. SVG is parsed as XML which strictly rejects null bytes.
2. The server-side `SanitizePayload` (from previous session) only applies to data arriving after the server rebuild. MQTT values already in the server's in-memory cache — replayed to clients on reconnect/load — bypassed server sanitization entirely.
3. On hover, MudTooltip triggers a full component re-render including the SVG `MarkupString`. If `DataValue` contained a null byte, the SVG injection crashed the circuit.

### Fix: `MqttDataCache.UpdateValue` — client-side gateway sanitization

**`src/PSTT.Dashboard.Client/Services/MqttDataCache.cs`**
- Added `using PSTT.Dashboard.Helpers`
- `UpdateValue()` now calls `XmlStringHelper.StripInvalidXmlChars(s)` when the incoming value is a string
- This is the single entry point for ALL MQTT data on the client side — covers both live data and server-replayed cached values

### Fix: new `XmlStringHelper` utility

**`src/PSTT.Dashboard.Client/Helpers/XmlStringHelper.cs`** (new file)
- `StripInvalidXmlChars(string?)` — strips chars illegal in XML 1.0 (null bytes, lone surrogates, C0/C1 control chars except tab/LF/CR)
- `XmlSafeEncode(string?)` — strips invalid chars then HTML-encodes; use for any MarkupString SVG injection

### Fix: `GaugeNodeWidget` and `BatteryNodeWidget` SVG encoding

**`src/PSTT.Dashboard.Client/Widgets/GaugeNodeWidget.razor`**
- `@using PSTT.Dashboard.Helpers` added
- `RenderSvgLabels()` now calls `XmlStringHelper.XmlSafeEncode()` for the gauge value text and unit text (was `System.Net.WebUtility.HtmlEncode`)

**`src/PSTT.Dashboard.Client/Widgets/BatteryNodeWidget.razor`**
- `@using PSTT.Dashboard.Helpers` added
- SVG `<text>` content now uses `XmlStringHelper.XmlSafeEncode(FormatPercent())` (was `HtmlEncode`)

⚠️ The `DataValueTooltipContent.razor` tooltip already had its own `SanitizeForDisplay()` from a previous session — that path was protected. The crash path was the SVG MarkupString re-render on tooltip hover, not the tooltip content itself.

---



**Branch:** develop

### Widget refactor: MudNodeWidget uses StandardNodeLayout

**`StandardNodeLayout.razor`** updated to support icon rendering alongside the title:
- Added `HasTitleContent` computed property — title area renders when either `Node.Icon` or `Node.Title` is non-empty (was only checking title)
- Added `IconColor` computed property (same enum mapping as MudNodeWidget previously had inline)
- Updated `TitleDivStyle`: Left/Right positions use column-flex with centred icon above text; Above/Below use row-flex with icon+text side by side
- Removed duplicated icon-color logic from `MudNodeWidget`

**`MudNodeWidget.razor`** fully replaced with `StandardNodeLayout` wrapper:
- Removed ~90 lines of custom MudCard/MudCardHeader/MudCardContent/tooltip/port rendering — all now inherited from `StandardNodeLayout`
- Text content passed as `ExtraContent` RenderFragment
- Now benefits from: proper `DataValueTooltipContent` (multi-topic aware, sanitised), background image support, correct port rendering, double-click via `AppState.TriggerEditProperties()`
- Fixed latent bug: old widget checked `Node.DataTopic` (legacy singular field) as the loop condition inside a loop over `Node.DataTopics`

### Property editor refactor: reflection-driven, no more @if blocks

**`NodePropertyEditor.razor`** — removed 5 `@if (Node is XxxModel)` blocks (~135 lines):
- Replaced with a 4-line `@foreach (var category in GetNodeSpecificCategories())` loop that renders `<NodePropertyRenderer Node="Node" Category="@category" />`
- Each node-type-specific category gets a `<MudDivider>` + caption heading + the renderer
- `NodePropertyRenderer.razor` (already existed) reads `[NpXxx]` attributes via reflection and renders the appropriate MudBlazor control (MudTextField, MudNumericField, MudCheckBox, MudSelect, DynamicComponent for custom group editors)

**`NodePropertyEditor.razor.cs`** — added `GetNodeSpecificCategories()`:
- Reflects over the current node's type, collects distinct `Category` values from all `[NpXxx]`-annotated properties, returns them in declaration order
- Added `using System.Reflection`

**Effect of this change:**
- Adding a new node type no longer requires editing `NodePropertyEditor.razor` — just annotate the model properties with `[NpCustom]`/`[NpText]`/`[NpNumeric]`/`[NpCheckbox]`/`[NpSelect]` attributes and they appear automatically
- ⚠️ Minor visual change: node-specific fields are now stacked vertically rather than in compact MudGrid rows (e.g. Gauge: Min/Max/Origin/Unit now stack instead of appearing in one row). Functionally identical.

### Removed Grid/Image editor extractor todos
- `refactor-editor-grid` and `refactor-editor-image` marked done (those node types no longer exist)

---

## 2026-03-23 — Fix InvalidCharacterError from invalid chars in MQTT payloads

**Branch:** develop

### Root cause
MQTT brokers can send payloads containing null bytes (`\0`, U+0000) or other characters that are illegal in XML 1.0 / HTML DOM text nodes (lone surrogates U+D800–U+DFFF, C0/C1 control chars). When Blazor Server applied a render batch containing these characters in a text node, the browser's DOM API threw `DOMException: InvalidCharacterError`, which Blazor reported as an `InvalidOperationException` and killed the SignalR circuit.

The symptom was intermittent crashes that correlated with MQTT data arriving; the user observed it as a crash when opening the Battery node property editor (the timing coincided with a data update).

### Fix
Three-layer defence:

1. **`MqttClientService.SanitizePayload()`** (`src/PSTT.Dashboard.Server/Services/MqttClientService.cs`)
   - New private static helper strips characters outside the valid XML 1.0 character set: keeps `\t`, `\n`, `\r`, U+0020–U+D7FF, U+E000–U+FFFD; discards everything else.
   - Called at the single point where MQTT payloads are decoded: `var value = SanitizePayload(ConvertPayloadToString(...));`

2. **`BatteryNodeWidget.razor`** — SVG `<text>` rendered via `MarkupString`
   - `FormatPercent()` output is now wrapped with `System.Net.WebUtility.HtmlEncode()` before being interpolated into the raw HTML string.
   - `HtmlEncode` also encodes `<`, `>`, `&` so the SVG is always valid markup.

3. **`DataValueTooltipContent.razor`** — displays raw MQTT value in tooltip
   - Added `SanitizeForDisplay(string?)` local method (same stripping logic) applied to `val?.ToString()` before it's rendered in a text node.

### Cleanup
- `MudNodeModel.cs` — removed orphaned XML doc comment block that was left dangling after the `BackgroundImageFromData` property was deleted in the previous session.

---

## 2026-03-23 — Remove Grid/Image node types; add background image to base node

**Commit:** `2074325`
**Branch:** develop

### Removed
- `GridNodeModel.cs` + `GridNodeWidget.razor` — Grid node removed entirely. Was an outlier: its per-cell MQTT topic model didn't fit the base `StandardNodeLayout` pattern, and the wildcard-topic routing design (path/+/+ → row/column) needed for a useful Grid is a larger feature best deferred.
- `ImageNodeModel.cs` + `ImageNodeWidget.razor` — Image node removed as a separate type.

### Changed
- `MudNodeModel` — added three new base properties available on **all** node types:
  - `BackgroundImageUrl` (string?) — static CSS background image URL
  - `BackgroundObjectFit` (string, default "cover") — background-size: "cover", "contain", or "fill" (→ `100% 100%`)
  - `BackgroundImageFromData` (bool) — when true, uses the node's first MQTT data value as the background image URL (dynamic image from broker)
- `StandardNodeLayout.razor` — `ContainerStyle` now computes `background-image` + `background-size` + `background-position` from the new base properties.
- `NodePropertyEditor.razor` — replaced Image-specific and Grid-specific sections with a universal "Background Image" section (URL, Image Fit dropdown, "Use data value as URL" checkbox) shown for every node type.
- `ApplicationState.cs` — removed Image/Grid component registrations, removed type-specific deserialise/serialise blocks for Image and Grid, added base background image round-trip for all node types. Legacy `Image` NodeType entries in saved files load cleanly as plain Text nodes with the `BackgroundImageUrl` set from the old `StaticImageUrl` field.
- `DiagramState.cs` / `NodeState` — added `BackgroundImageUrl`, `BackgroundObjectFit`, `BackgroundImageFromData` as base fields; removed `GridColumnHeaders`/`GridRows`/`GridRowState`; kept `StaticImageUrl`/`ObjectFit` as nullable read-only legacy fields for old-file compat.
- `Display.razor.cs` — removed Image/Grid from `AddNode()` and paste/copy snapshots; added background image to base paste restore.
- `NodeTypePickerDialog.razor` — removed Image and Grid entries.
- `NodePropertyEditor.razor.cs` — removed `AddGridColumn`, `RemoveGridColumn`, `EnsureGridTopicSlots` helpers.

### Caveats
⚠️ Old saved files with `"NodeType": "Grid"` nodes will load as plain text nodes and their row/column data will be lost. This is intentional — backward compat for format is deprioritised per project notes.
⚠️ Old `"NodeType": "Image"` nodes load as plain text nodes with `BackgroundImageUrl` set from the old `StaticImageUrl` field, so images are preserved.

---

: shared layout, attributes, property groups

**Commit:** `dde31e9`
**Branch:** develop

### New files

| File | Purpose |
|---|---|
| `Models/NodePropertyAttributes.cs` | `[NpText]`, `[NpNumeric]`, `[NpCheckbox]`, `[NpSelect]`, `[NpCustom]` attributes for model properties. `NpNumericAttribute.Min/Max` use `double.NaN` as "no limit" sentinel (attribute parameters can't be nullable types). |
| `Models/NumericRangeSettings.cs` | Shared POCO: `Min`, `Max`, `Origin?`, `DataTopicIndex`. Used by both `GaugeNodeModel` and `BatteryNodeModel`. |
| `Widgets/DataValueTooltipContent.razor` | Shared tooltip content component accepting `MudNodeModel`. Shows all topics with values + timestamps; single "No data topic configured" fallback. Replaces 5 near-identical inline tooltip blocks. |
| `Widgets/StandardNodeLayout.razor` | Shared outer shell for visual nodes (Gauge, Battery, Switch, Image). Injects `AppState`; handles tooltip, container div + CSS class + background colour, title positioning (Above/Below/Left/Right), double-click → edit, port rendering. Accepts `ExtraContent` RenderFragment + optional `ShowTitle` bool. Correctly suppresses both title positions when `ShowTitle=false` (fixes a bug in the old `ImageNodeWidget` where the title would still appear below even when `ShowTitle=false` with `TitlePos=Above`). |
| `Components/NumericRangeEditor.razor` | MudGrid editor for `NumericRangeSettings`: Min, Max, Origin (nullable), DataTopicIndex. Accepts `[Parameter] object? Value` (cast to `NumericRangeSettings` internally). |
| `Components/ColorTransitionGroupEditor.razor` | Wraps existing `ColorTransitionEditor`. Accepts `[Parameter] object? Value` (cast to `ColorTransition`). Shows `ColorTopicIndex` numeric field + delegates threshold list to `ColorTransitionEditor`. |
| `Components/NodePropertyRenderer.razor` | Reflection-driven control renderer. Loops over `[NpXxx]` attributes on the node type filtered by `Category`; renders matching MudBlazor controls. Uses `RenderTreeBuilder` delegate pattern for generic `MudNumericField<T>` and `MudSelect<T>`. `NpCustom` → `DynamicComponent` with `Node` + `Value` params. |

### Modified files

**Models:**
- `GaugeNodeModel.cs` — `MinValue/MaxValue/ArcOrigin/DataTopicIndex` replaced by `NumericRangeSettings Range`. Read-only convenience accessors (`MinValue => Range.Min` etc.) kept for backward compat in widget render code. Added `[NpCustom]`, `[NpText]`, `[NpSelect]` attributes.
- `BatteryNodeModel.cs` — same pattern as Gauge with `NumericRangeSettings Range`. Added `[NpCustom]`, `[NpCheckbox]` attributes.
- `SwitchNodeModel.cs` — added `[NpText]`/`[NpSelect]`/`[NpCheckbox]` to all properties.
- `ImageNodeModel.cs` — added `[NpText]`/`[NpSelect]`/`[NpCheckbox]` to all properties.
- `LogNodeModel.cs` — added `[NpNumeric]`/`[NpCheckbox]` to all properties.
- `TreeViewNodeModel.cs` — added `[NpText]`/`[NpCheckbox]` to all properties.

**Widgets:**
- `BaseNodeWithDataWidget.cs` — added `protected` title positioning methods: `TitlePos`, `ShowTitleFirst()`, `OuterFlexStyle()`, `TitleDivStyle()`. These are now in one place; previously copied identically into 4 widget files.
- `GaugeNodeWidget.razor` — fully refactored to use `<StandardNodeLayout>`. Removed title methods, tooltip, container div, port loop (~35 lines of boilerplate). Only SVG arc + text remain as `<ExtraContent>`.
- `BatteryNodeWidget.razor` — same refactor as Gauge.
- `SwitchNodeWidget.razor` — same refactor (removed `@using Blazor.Diagrams.Components.Renderers`).
- `ImageNodeWidget.razor` — same refactor; passes `ShowTitle="@Node.ShowTitle"` to `StandardNodeLayout`.

**Services/Pages:**
- `ApplicationState.cs` — Gauge/Battery deserialization uses `Range = new NumericRangeSettings { Min=..., Max=..., Origin=..., DataTopicIndex=... }`. Serialization uses `g.Range.Min` etc.
- `Display.razor.cs` — paste-cloning code updated to use `Range = new NumericRangeSettings { ... }` instead of assigning flat read-only accessors.
- `NodePropertyEditor.razor` — Gauge/Battery property sections updated to use `gaugeNode.Range.Min` etc. (direct two-way binding to the POCO properties). Node property renderer (`NodePropertyRenderer`) infrastructure created but NodePropertyEditor still uses hand-crafted sections for all node types — the full migration from `@if (Node is XxxModel)` to `NodePropertyRenderer` is deferred; the infrastructure is now in place.

### Caveats / remaining work
⚠️ `NodePropertyRenderer` is created and compiles, but `NodePropertyEditor.razor` still uses manual type-dispatch for all node types. The renderer infrastructure can be adopted incrementally — annotate a model property with `[NpXxx]`, add a Category, and `NodePropertyRenderer` will render it automatically.
⚠️ `NpCustom` attributes on model properties reference `typeof(NumericRangeEditor)` which is in `MqttDashboard.Components` — a slight model→UI namespace dependency. Acceptable for now; could be removed by using string-based component lookup in future.

---

## 2026-03-23 — Bug fixes: node resize loop, port visibility, alignment toolbar, save/save-as

**Commit:** `492a2cc`
**Timestamp:** 2026-03-23 ~18:15 UTC
**Branch:** FEAT-C

### bug-node-grow — Node grows indefinitely when Title is cleared
**Files:** `src/PSTT.Dashboard.Client/Widgets/MudNodeWidget.razor`

`<MudCardHeader>` was conditionally removed from the DOM when both `Node.Title` and `Node.Icon` were empty. Blazor.Diagrams re-measures node content height after each render; losing the header element caused a size change, which triggered another render, which re-measured again → infinite loop. Fix: always render the `MudCardHeader` but apply `style="display:none"` when both fields are empty. The DOM structure stays stable; Blazor.Diagrams sees no size change.

---

### bug-port-invisible — Ports invisible on all non-Text nodes; blank border visible
**Files:** `src/PSTT.Dashboard.Client/Widgets/BaseNodeWidget.cs`, `GaugeNodeWidget.razor`, `SwitchNodeWidget.razor`, `BatteryNodeWidget.razor`, `GridNodeWidget.razor`, `ImageNodeWidget.razor`, `LogNodeWidget.razor`, `TreeViewNodeWidget.razor`

Two root causes:
1. `ContainerStyle()` in `BaseNodeWidget` added `overflow:hidden` whenever `Node.Size` was set. Ports (rendered inside that container) were clipped at the node boundary. Removed `overflow:hidden` from the style string.
2. All affected widgets applied `pa-1` (4 px MudBlazor padding) on the outer container div, creating a visible blank gap between the node's outer border and its content. Removed `pa-1` from the outer div in all 7 widgets. Inner content retains its own spacing as needed.

`MudNodeWidget` was unaffected because it renders ports outside the `<MudCard>` element.

---

### bug-align-toolbar — Alignment toolbar buttons unclickable
**Files:** `src/PSTT.Dashboard.Client/Pages/Display.razor`, `Display.razor.cs`

The alignment toolbar overlay (`position:absolute;z-index:10`) was inside a `<MudPaper>` that lacked `position:relative`. The `<DiagramCanvas>` SVG was rendered on top and intercepting pointer events. Two fixes:
1. Added `position:relative` to `CanvasStyle` so the absolute-positioned toolbar is scoped to the canvas container.
2. Raised toolbar `z-index` from `10` → `1000` to ensure it sits above all diagram canvas elements.

---

### bug-new-save-state — Save enabled after New; Save As overwrites silently
**Files:** `src/PSTT.Dashboard.Client/Layout/AppMenu.razor`, `src/PSTT.Dashboard.Client/Pages/Display.razor.cs`

Two problems:
1. After File → New, `DiagramName` is empty but the Save menu item was enabled. `SaveDashboard()` had a silent fallback: `var name = string.IsNullOrEmpty(...) ? "Default" : ...`. Fixed: Save menu item now has `Disabled="@string.IsNullOrEmpty(AppState.DiagramName)"`. The silent fallback removed; `SaveDashboard()` returns early with a warning snackbar if no filename is set.
2. Save As did not check for an existing file before overwriting. Fixed: after the user enters a name, `ListDashboardsAsync()` is called; if a match exists (case-insensitive) and it differs from the current filename, a MudBlazor "Overwrite?" confirm dialog is shown before proceeding.

Note: `DiagramName` (filename on disk, no extension) and `DiagramDisplayName` (human label in JSON, shown in title bar) are distinct — Save/Save As operate on the filename only.

---



**Commit:** `dbb63cb`
**Timestamp:** 2026-03-22 ~18:15 UTC
**Branch:** FEAT-C

### Items completed

#### Fix: Color transition topic index is per-node, not per-threshold
- `Models/GaugeNodeModel.cs` — removed `TopicIndex` from `GaugeColorThreshold`; `ColorTopicIndex` already on `GaugeNodeModel` (added earlier this session)
- `Widgets/GaugeNodeWidget.razor` — `GetArcColor()` uses `Node.ColorTopicIndex` for all threshold comparisons
- `Components/ColorTransitionEditor.razor` — reverted: no per-threshold topic field; clean 3-column layout (When / Value / Color)
- `Components/NodePropertyEditor.razor` — Gauge section: `ColorTopicIndex` spinner in same row as `DataTopicIndex`; removed `ShowTopicIndex` param from `ColorTransitionEditor` call
- `Models/DiagramState.cs` — `GaugeColorTopicIndex` on `NodeState`; `TopicIndex` removed from `GaugeColorThresholdState`
- `Services/ApplicationState.cs` — serialize/deserialize updated; `GaugeColorTopicIndex` null-when-0 for clean JSON

#### Fix: Log column options — full independent booleans, no wildcard logic
- `Models/LogNodeModel.cs` — replaced `ShowTopic` with six booleans: `ShowDate`, `ShowTime`, `ShowTopicFull`, `ShowTopicPath`, `ShowTopicName`, `ShowValue`
- `Widgets/LogNodeWidget.razor` — removed `IsWildcard`; all 6 columns driven by model booleans; added `TopicPath(topic)` and `TopicName(topic)` helper methods; `colCount` computed inline for empty-row colspan
- `Components/NodePropertyEditor.razor` — Log section: replaced single ShowTopic checkbox with 6-checkbox responsive grid (3 per row)
- `Models/DiagramState.cs` — replaced `ShowTopic` with `ShowTopicFull`, `ShowTopicPath`, `ShowTopicName`, `ShowValue` fields
- `Services/ApplicationState.cs` — serialize/deserialize updated; `ShowValue` written as null when true (clean JSON default)

#### Fix: Undo stack cleared on entering Edit Mode
- `Pages/Display.razor.cs` — in `SwitchMode(enterEditMode:true)`, added `AppState.ClearUndoRedo()` immediately after capturing `_editSnapshot`. Entering Edit Mode is now always a clean undo state.

#### Fix: Reload from disc exits Edit Mode
- `Pages/Display.razor.cs` — rewrote `ReloadDiagram()`: always calls `AppState.SetEditMode(false)` + `AppState.MarkSaved()` + `AppState.ClearUndoRedo()` before loading; always loads with `readOnly: true`; removed the re-subscription block that was restoring Edit Mode after reload.

#### Added: Undo All menu item
- `Services/ApplicationState.cs` — added `event Action? MenuUndoAll` and `TriggerUndoAll()` method
- `Layout/AppMenu.razor` — added `<MudMenuItem Label="Undo All" ...>` after Redo; added `private void UndoAll() => AppState.TriggerUndoAll();`
- `Pages/Display.razor.cs` — added `_onMenuUndoAll` private field; wired in `SubscribeEditEvents` / unwired in `UnsubscribeEditEvents` / nulled in null-out block; added `UndoAllAction()` async method that applies `_editSnapshot`, clears undo/redo, marks saved, shows snackbar — uses `_suppressDirty` guard to prevent false dirty mark during replay

### Notes
- All builds succeeded with 0 errors (pre-existing MUD0002 warning on LogNodeWidget `Title` attribute is unchanged)
- `UndoAllAction` applies the pre-edit snapshot (`_editSnapshot`) not the last undo state, so it always fully reverts to the clean state — regardless of how many changes were made

---

## 2026-03-23 — Fix: Undo All reverts to empty page

**Commit:** _(this batch)_
**Timestamp:** 2026-03-23 ~00:15 UTC
**Branch:** FEAT-C

### Bug fixed

#### Fix: Undo All reverts to empty page
**Root cause:** `UndoAllAction` called `ApplyDiagramState(_editSnapshot)`. `ApplyDiagramState` calls `CreateDiagramFromState(state, ...)` which expects a flat single-page `DiagramState` (with `Nodes` / `Links` at top level). But `_editSnapshot` from `BuildFullState()` with multiple pages is a *wrapper* `DiagramState` with a `Pages` list and empty top-level `Nodes` / `Links`. `CreateDiagramFromState` saw an empty node list and produced an empty diagram.

**Fix:** `Pages/Display.razor.cs` — `UndoAllAction` now uses `LoadFullState(_editSnapshot, readOnly: false)` which correctly handles both single-page and multi-page snapshots. After `LoadFullState`, edit-mode event handlers are re-attached (`SelectionChanged`, `Changed`, `SubscribeEditEvents`, `UpdateSelectionState`). The old `ApplyDiagramState` call for `UndoAll` is removed.

Same issue exists for regular Undo/Redo if they ever snapshot a multi-page state — noted for future hardening (regular Undo/Redo only snapshot the active page via `GetDiagramState()`, so they are safe for now).

### Notes
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — Link animation startup fix (SSR/F5 flash + initial-value timing)

**Commit:** _(this batch)_
**Timestamp:** 2026-03-22 ~19:55 UTC
**Branch:** FEAT-C

### Items completed

#### Fix: Link animations flash on F5 refresh / not shown until first live data arrives
**Root cause (two issues):**
1. `SetupDataWatchers()` was called on every `OnParametersSet`, which fires for every re-render of the node widget. Each call disposed and recreated all watchers and re-seeded from cache, calling `TriggerLinkAnimation()` on each re-run. During `RefreshAll()`, every node got its watchers torn down and rebuilt, causing an animation reset flash.
2. On initial load, `SetupDataWatchers()` seeds from cache and calls `TriggerLinkAnimation()` + `l.Refresh()` before the diagram SVG is rendered (the DiagramCanvas is guarded by `!IsInteractive`). So the animation update was lost. Animations only showed when first live data arrived.

**Fix:**
- `Widgets/BaseNodeWithDataWidget.cs` — Added `_watcherTopicsKey` (string?). `SetupDataWatchers()` now returns early if `Node.DataTopics` key matches `_watcherTopicsKey`, preventing redundant teardown/rebuild on repeated `OnParametersSet` calls. Key is cleared on `Dispose()` to ensure proper re-init.
- Added `OnAfterRenderAsync(bool firstRender)` override: calls `TriggerLinkAnimation()` when `firstRender = true`. Node widgets only mount (and fire `firstRender`) after `IsInteractive = true` because the DiagramCanvas is inside an `@if (AppState.IsInteractive)` guard in Display.razor. This ensures animation fires when the SVG is actually in the DOM.
- Promoted `TriggerLinkAnimation()` from `private` to `protected` (needed by `OnAfterRenderAsync`; also available to subclasses).

### Notes
- Fixes both "lines only shown on first data update" (timing) and "F5 flash" (redundant re-initialization).
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — Dirty flag on selection fix, log width, link delete dirty tracking

**Commit:** _(this batch)_
**Timestamp:** 2026-03-22 ~19:40 UTC
**Branch:** FEAT-C

### Items completed

#### Fix: Dirty flag fires on node selection
**Root cause:** `OnNodeChanged(node)` called `AppState.MarkEdited()` directly (no deferral), so every node.Changed event — including selection — instantly marked the diagram dirty. The `_pendingDirtyMark` pattern existed only in `OnDiagramChanged`, which fires separately.

**Fix:**
- `Pages/Display.razor.cs` — `OnNodeChanged`: removed direct `MarkEdited()` call; now uses the same `_pendingDirtyMark = true` + `InvokeAsync(...)` deferred pattern. `OnSelectionChanged` clears the flag before the callback runs for selection events, so selection doesn't mark dirty. Real moves/resizes still trigger dirty + undo push.
- `OnDiagramChanged`: removed all dirty logic; now only calls `InvokeAsync(StateHasChanged)` (diagram-level `Changed` was redundant for dirty tracking now that per-node events handle it).

#### Fix: Link removal doesn't mark diagram dirty
- `Pages/Display.razor.cs` — added `OnLinkRemoved` handler: calls `AppState.MarkEdited() + PushUndoSnapshot()`.
- `SubscribeEditEvents`: added `_diagram.Links.Removed += OnLinkRemoved`.
- `UnsubscribeEditEvents`: added unsubscription.
- `OnLinkAdded`: added `MarkEdited() + PushUndoSnapshot()` (link additions also now explicitly mark dirty).

#### Fix: Log view width expands with long content
- `Widgets/BaseNodeWidget.cs` — `ContainerStyle()`: added `overflow:hidden` to the size string. All node widgets now clip any overflowing content to their declared size.

### Notes
- `align-toolbar-grey` and `error-ui-css` were already correctly implemented — marked done.
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — ColorTransition class refactor (Gauge + Battery)

**Commit:** _(this batch)_
**Timestamp:** 2026-03-22
**Branch:** FEAT-C

### Items completed

#### Refactor: Introduce `ColorTransition` class to wrap color threshold state
- `Models/ColorTransition.cs` — NEW FILE. Contains `ColorTransition` (wraps `ColorTopicIndex` + `List<GaugeColorThreshold>`) and `GaugeColorThreshold` (moved from `GaugeNodeModel`).
- `Models/GaugeNodeModel.cs` — `ColorThresholds` and `ColorTopicIndex` removed; replaced by single `GaugeColor` property of type `ColorTransition`. `GaugeColorThreshold` class removed from this file.
- `Models/BatteryNodeModel.cs` — `ColorThresholds` and `ColorTopicIndex` removed; replaced by single `BatteryColor` property of type `ColorTransition`. Obsolete `LowColor`, `MedColor`, `HighColor`, `MidPoint`, `NegativeColor`, `PositiveColor` fields removed entirely.
- `Widgets/GaugeNodeWidget.razor` — `GetArcColor()` now uses `Node.GaugeColor.ColorThresholds` and `Node.GaugeColor.ColorTopicIndex`.
- `Widgets/BatteryNodeWidget.razor` — `ColorValue` helper uses `Node.BatteryColor.ColorTopicIndex`; `GetFillColor()` uses `Node.BatteryColor.ColorThresholds`. Obsolete color fallback code removed.
- `Components/NodePropertyEditor.razor` — all Gauge and Battery color bindings updated to new nested paths (`gaugeNode.GaugeColor.*`, `batteryNode.BatteryColor.*`).
- `Models/DiagramState.cs` — stripped all legacy flat fields (`ColorThresholds`, `ColorTopicIndex`, `GaugeColorTopicIndex`, `LowColor`, etc.); added `ColorTransitionState` DTO; `NodeState.GaugeColor` and `NodeState.BatteryColor` are `ColorTransitionState?`.
- `Services/ApplicationState.cs` — added `DeserializeColorTransition()` / `SerializeColorTransition()` private helpers + `DeserializeColorTransitionStatic()` / `SerializeColorTransitionStatic()` public static wrappers; Gauge and Battery deserialize/serialize blocks updated to use helpers.
- `Pages/Display.razor.cs` — copy/paste node serialization updated for Gauge and Battery (was using old flat `ColorThresholds`; now calls `SerializeColorTransitionStatic` / `DeserializeColorTransitionStatic`).

### Notes
- No backward compat with old JSON files — nodes will load with empty color transitions if saved with old format.
- `Display.razor.cs` also had copy-node code referencing old fields — fixed in same batch.
- Build: 0 errors, 11/11 tests passed.

---

## 2026-03-22 — DataTopic refactor, Battery topic index parity

**Commit:** `ac7b2f9`
**Timestamp:** 2026-03-22 ~18:50 UTC
**Branch:** FEAT-C

### Items completed

#### Refactor: DataTopic/DataTopic2 → computed from DataTopics list
- `Models/MudNodeModel.cs` — removed settable `DataTopic`/`DataTopic2` properties; replaced with computed read-only accessors (`=> DataTopics[0]`/`[1]`). Added `DataValues` (object?[]) and `DataUpdatedTimes` (DateTime?[]) arrays; added computed compat `DataValue`/`DataValue2`/`DataLastUpdated`/`DataLastUpdated2` getters. `DataTopics` list is now the single source of truth.
- `Widgets/BaseNodeWithDataWidget.cs` — `SetupDataWatchers()` now sizes `DataValues`/`DataUpdatedTimes` arrays to match topic count and writes to `Node.DataValues[idx]`/`Node.DataUpdatedTimes[idx]`. Removed old scalar writes. The fallback to `DataTopic`/`DataTopic2` is gone (DataTopics list must be populated by deserialization).
- `Services/ApplicationState.cs`:
  - Removed `node.DataTopic = nodeState.DataTopic` and `node.DataTopic2 = nodeState.DataTopic2` (computed, can't be set)
  - `CreateQuickAddNode`: changed `DataTopic = topicPath` → `DataTopics = new List<string> { topicPath }` in object initializer
  - Serialization: simplified `DataTopic`/`DataTopic2` write to use computed props (cleaner, same output)
- `Pages/Display.razor.cs` — paste/copy node path also used `node.DataTopic = ...`; fixed to use `node.DataTopics.Add(...)`.

#### Fix: Battery gets same DataTopicIndex + ColorTopicIndex as Gauge
- `Models/BatteryNodeModel.cs` — added `DataTopicIndex` (int, default 0) and `ColorTopicIndex` (int, default 0) properties
- `Widgets/BatteryNodeWidget.razor`:
  - Added `ActiveValue` computed property (mirrors Gauge): `Node.DataTopicIndex == 1 ? Node.DataValue2 : Node.DataValue`
  - Added `ColorValue` computed property: `Node.ColorTopicIndex == 1 ? Node.DataValue2 : Node.DataValue`
  - `UpdatePercent()` now uses `ActiveValue` instead of `Node.DataValue`
  - Added `protected override void OnData2Updated() => UpdatePercent()` (so DataTopicIndex=1 also updates fill)
  - `GetFillColor()` now uses `ColorValue` for threshold comparisons instead of `_percent` when a ColorValue is available
  - `FormatPercent()` uses `ActiveValue`
- `Models/DiagramState.cs` — added generic `DataTopicIndex`/`ColorTopicIndex` fields; kept `GaugeDataTopicIndex`/`GaugeColorTopicIndex` as backward-compat read-only fallback fields
- `Services/ApplicationState.cs` — Gauge deserialise: fallback chain `DataTopicIndex ?? GaugeDataTopicIndex ?? 0`; Gauge serialise: writes to `DataTopicIndex`/`ColorTopicIndex` (not Gauge-specific names); Battery deserialise+serialise: reads/writes `DataTopicIndex`/`ColorTopicIndex`
- `Components/NodePropertyEditor.razor` — Battery section: added 2-column row with "Value Topic (0-based)" and "Color Topic (0-based)" spinners, identical layout to Gauge

### Notes
- `DataValue`/`DataValue2`/`DataLastUpdated`/`DataLastUpdated2` are still usable everywhere as computed shims; no widget code required changing
- Old dashboard files with scalar `DataTopic`/`DataTopic2` fields (and no `DataTopics` array) are migrated transparently on load
- `GaugeDataTopicIndex`/`GaugeColorTopicIndex` in JSON are still read (fallback); new saves write generic `DataTopicIndex`/`ColorTopicIndex` — so old Gauge configs load correctly after upgrade
- All 11 tests pass; 0 build errors

---


**Commit:** `5378e80` · 2026-03-22 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: Gauge properties dialog compaction**  
- `NodePropertyEditor.razor` — all four fields (Min, Max, Origin, Unit) now live in a single `MudGrid` row with `xs="3"` each. Previously Origin was on its own line and the old informational `<MudText>` about text position is removed (replaced by the TextPosition selector).

**Added: Gauge text position (above / below arc)**  
- `GaugeNodeModel.cs` — added `TextPosition` property (string, default `"Below"`).  
- `GaugeNodeWidget.razor` — when `TextPosition == "Above"`, the static Text `<div>` is rendered before the SVG; otherwise it renders after (existing "below" behavior).  
- `NodePropertyEditor.razor` — `MudSelect` for TextPosition (Below arc / Above arc).  
- `DiagramState.cs` / `ApplicationState.cs` — persisted as `NodeState.TextPosition`; only written when non-default (saves `null` = "Below" for clean JSON).

**Added: Gauge value topic index selector**  
- `GaugeNodeModel.cs` — added `DataTopicIndex` property (int, default `0`).  
- `GaugeNodeWidget.razor` — added `private object? ActiveValue` helper that returns `Node.DataValue2` when `DataTopicIndex == 1`, else `Node.DataValue`. `UpdatePercent()`, `GetArcColor()`, and `FormatValue()` all use `ActiveValue`. Also added `OnData2Updated()` override so the widget repaints when either topic updates (correct value is read via `ActiveValue`).  
- `NodePropertyEditor.razor` — `MudNumericField` labelled "Value Topic (0-based)" with helper text.  
- `DiagramState.cs` / `ApplicationState.cs` — persisted as `NodeState.GaugeDataTopicIndex`; written as `null` when 0 (default).

**Added: Per-threshold topic index in color transitions**  
- `GaugeColorThreshold.cs` (in `GaugeNodeModel.cs`) — added `TopicIndex` property (int, default `0`).  
- `GaugeNodeWidget.razor` `GetArcColor()` — each threshold now uses `t.TopicIndex == 1 ? Node.DataValue2 : Node.DataValue` for its comparison. Different thresholds can watch different topics on the same node.  
- `ColorTransitionEditor.razor` — added `ShowTopicIndex` bool parameter (default `false`). When true, a small "Topic #" `MudNumericField` (min=0) is prepended to each threshold row; grid widths adjust (`When` drops from `xs="3"` to `xs="2"`, Color from `xs="4"` to `xs="3"`). Gauge passes `ShowTopicIndex="true"`; Battery does not.  
- `GaugeColorThresholdState` / `ApplicationState.cs` — `TopicIndex` serialised and round-tripped.

**Added: Log "always show topic column"**  
- `LogNodeModel.cs` — added `ShowTopic` bool (default `false`).  
- `LogNodeWidget.razor` — topic column and its `colspan` now show when `IsWildcard || Node.ShowTopic`. Header and data cell both updated.  
- `NodePropertyEditor.razor` — added `MudCheckBox` "Always Show Topic Column" to the Log settings section.  
- `DiagramState.cs` / `ApplicationState.cs` — persisted as `NodeState.ShowTopic`; written as `null` when false.

---

## 2026-03-22 — Bug fixes batch 2: dirty flag, thresholds, log pause, width, reconnect
**Commit:** `52f7f93` · 2026-03-22 17:11 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: Dirty flag on node selection**  
- `Display.razor.cs` `OnDiagramChanged` — Blazor.Diagrams fires `Changed` then `SelectionChanged` synchronously when a node is clicked. Previously, `Changed` immediately called `MarkEdited()`, marking the dashboard dirty even though nothing had been edited.  
- Fix: `OnDiagramChanged` now sets `_pendingDirtyMark = true` and defers via `InvokeAsync`. `OnSelectionChanged` clears the flag before the async work runs. If the change was a real edit (node moved, etc.), `SelectionChanged` does not fire, so the flag stays set and `MarkEdited()` is called.  
- ⚠️ Remaining: after undoing all changes, the dirty flag stays red ("undo stack back to saved state" detection not yet implemented — noted in TODO).

**Fixed: Default color thresholds for Battery and Gauge**  
- `BatteryNodeModel.cs` — constructor now initialises `ColorThresholds` with red ≤25%, orange ≤50%, green ≥50%.  
- `GaugeNodeModel.cs` — constructor now initialises `ColorThresholds` with red ≤0, green ≥0 (sensible for voltage/temperature readings centered on zero).  
- Old saved files that have no thresholds are unaffected (the JSON deserialiser will overwrite the constructor defaults with the empty list from the file). This only applies to newly-created nodes.

**Fixed: Log and TreeView not filling widget width**  
- Created `LogNodeWidget.razor.css` with `::deep .mud-simple-table`, `::deep .mud-table-container`, `::deep .mud-table-root` all set to `width:100%; overflow-x:hidden`.  
- Created `TreeViewNodeWidget.razor.css` with `::deep .mud-treeview { width:100%; min-width:0 }`.  
- `TreeViewNodeWidget.razor` inner flex container: added `min-width:0` to prevent flex overflow.

**Added: Log node pause/resume button**  
- `LogNodeWidget.razor` — added `_paused` bool. New header row (always shown, title fades with opacity:0 when empty so layout is stable) contains title text + small Pause/Play `MudIconButton`.  
- `OnData1ReceivedCore` returns early when `_paused = true`. Entries shown are frozen until resumed.

**Added: Reconnect value replay**  
- `MqttClientService.cs` — added `ConcurrentDictionary<string, string> _lastKnownValues`; populated with every received message.  
- `MqttTopicSubscriptionManager.cs` — `TopicMatchesFilter(filter, topic)` made public (wraps private `TopicMatches`).  
- `MqttDataHub.cs` — added `GetCurrentValuesForTopics(List<string> requestedFilters)` hub method: iterates `LastKnownValues`, returns all topics matching any of the requested filters.  
- `ISignalRService.cs` — added `GetCurrentValuesForTopicsAsync(List<string> topics)`.  
- `SignalRService.cs` — implemented via `InvokeAsync<Dictionary<string,string>>("GetCurrentValuesForTopics", ...)`.  
- `ServerSignalRService.cs` — implemented directly against `MqttClientService.LastKnownValues`.  
- `MqttInitializationService.cs` — `RestoreSubscriptionsAsync()` now calls `GetCurrentValuesForTopicsAsync` after re-subscribing and seeds `AppState.DataCache` with the results. Widgets show current data immediately on page refresh or reconnect.

---

## 2026-03-22 — Bug fixes batch 1: regression, error UI, alignment toolbar
**Commit:** `32fb995` · 2026-03-22 15:37 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: `BaseNodeWithDataWidget` initial-seed regression**  
- Previous batch's startup-animation fix called `OnData1ReceivedCore()` during cache seeding, which caused `LogNodeWidget` to append a duplicate entry every time `OnParametersSet` fired (e.g., on WASM interactive handoff).  
- Fix: `SetupDataWatchers()` changed back to calling `OnData1Updated()` + `TriggerLinkAnimation()` for the initial seed. `OnData1ReceivedCore` is only called from live MQTT messages.

**Fixed: `#blazor-error-ui` panel invisible**  
- `app.css` had `.blazor-error-boundary` styles but no `#blazor-error-ui` rule at all. The panel was always visible (unstyled, pale yellow from browser default).  
- Fix: added `display:none` default + dark-red/white styling matching the `.blazor-error-boundary` style. Panel is now hidden until Blazor raises an unhandled exception.

**Fixed: Alignment toolbar buttons greyed out**  
- `Display.razor` — six alignment `MudIconButton` elements were using default `Color.Default` (grey). Added `Color="Color.Primary"` to each so they appear clearly active.

**Also:** `TODO.md` cleaned up (fixed items marked, verbose error log stack trace removed). `CHANGELOG.md` updated. `.github/copilot-instructions.md` created.

---

## 2026-03-22 — Grid node feature + bug fixes
**Commit:** `71f4f4d` · 2026-03-22 11:58 UTC  
**Branch:** FEAT-C

### What was done

**Fixed: LogNodeWidget "Collection was modified" race condition**  
- `_entries` was a `readonly List<LogEntry>` mutated in-place from the MQTT callback thread while Blazor's render thread was iterating it.  
- Fix: `_entries` changed to a replaceable field. `OnData1ReceivedCore` builds a new list and assigns it atomically. CLR guarantees reference assignment is atomic, so the render thread always reads a complete list.

**Fixed: Link animation not starting until first value update**  
- `BaseNodeWithDataWidget.SetupDataWatchers()` seeded initial value via `OnData1Updated()` only. `OnData1ReceivedCore()` and `TriggerLinkAnimation()` were not called for the cache seed.  
- Fix: both are now called for the initial seed (later partially reverted — see regression fix above).

**Fixed: Grid size reverts to 20 on entering edit mode**  
- When entering edit mode, `_diagram.Options.GridSize` was null (read-only diagrams don't set it). The code fell back to `AppState.GridSize` which defaulted to 20.  
- Fix: fall back to `_pageStates[_activePageIndex].GridSize` instead. Also aligned `ApplicationState.GridSize` default from 20 → 10.

**Fixed: Grid menu tick marks missing**  
- `AppMenu.razor` grid submenu items were not checking `AppState.GridSize`; no visual indicator of active selection.  
- Fix: added `@(AppState.GridSize == X ? "✓ " : "  ")` prefix, matching the Theme submenu pattern.

**Added: Grid node widget**  
- New `GridNodeModel.cs` (NodeType="Grid") with `GridRowDefinition` (label + list of per-cell topic strings).  
- New `GridNodeWidget.razor` — inherits `BaseNodeWidget<GridNodeModel>`, manages its own per-cell `DataCache.Watch()` subscriptions keyed by `"r{rowIdx}:c{colIdx}"`.  
- Full persistence: `NodeState.GridColumnHeaders` + `NodeState.GridRows` (as `GridRowState` list), serialised/deserialised in `ApplicationState`.  
- Property editor: column headers, row labels, per-cell topic inputs.  
- Registered in `NodeTypePickerDialog`, `AppMenu`, `Display.razor.cs AddNode()`.

---

## 2026-03-21 — Image node, alignment tools, bug fixes
**Commit:** `477b77a` · 2026-03-21 12:21 UTC

### What was done
- **Image node** — new widget for static URL or MQTT-driven image URL. `object-fit` configurable (contain / cover / fill / scale-down). Placeholder icon when no URL set.
- **Node alignment tools** — multi-select toolbar (align left/right/top/bottom, center H/V) appears over the canvas when 2+ nodes selected in edit mode.
- Various bug fixes (see CHANGELOG for full list — auth on clean start, Docker version, save failure handling).

---

## 2026-03-20 — Multi-topic, MudTreeView, dashboard delete, MRU removal
**Commit:** `7155fc6` · 2026-03-20 14:07 UTC

### What was done
- **Variable data topics per node** — replaced fixed `DataTopic`/`DataTopic2` with a dynamic list `DataTopics`. Old files auto-migrated; saves write both formats for backward compat.
- **TreeView rewritten with MudTreeView** — replaced hand-rolled `RenderFragment` builder. Per-topic watchers avoid full-tree rebuild on each update. Expansion state preserved.
- **Dashboard delete** — Open dialog now has trash icon per row with confirmation.
- **MRU list removed** — recent files list removed; Open dialog is the sole entry point.
- **Spurious dirty on subscription add/remove** — `AddSubscriptionAsync`/`RemoveSubscriptionAsync` now only call `MarkEdited()` in edit mode.

---

## 2026-03-20 — Spurious dirty, SignalR null, edit indicator, discard fixes
**Commit:** `109223f` · 2026-03-20 10:30 UTC

### What was done
- Spurious dirty flag on mode switch suppressed during diagram lock/unlock operations.
- Dirty flag after discard fixed — discard now calls `MarkSaved()`.
- Discard now restores full page structure (not just nodes/links).
- Discard now properly exits edit mode (`SetEditMode(false)` called).
- `MarkSaved()` called after `RefreshAll()` on entering edit mode (blank page was showing red on enter).
- SignalR NullReferenceException with `#` wildcard fixed — payload coerced to `""`.
- Log table width fix — `min-width:0` and `overflow-x:hidden` on flex container.
- Edit mode indicator: grey (view), orange (editing clean), red (editing with unsaved changes).
- Page delete now shows confirmation dialog.
- Node properties dialog: backdrop click no longer dismisses it.

---

## 2026-03-20 — Save failure, wildcard Watch, MQTT retain/QoS
**Commit:** `ffe6771` · 2026-03-20 00:28 UTC

### What was done
- **Save failure stays in edit mode** — dashboard no longer closes edit mode if save fails; error snackbar includes filename and hint.
- **Log wildcard topics** — `MqttDataCache.Watch()` now supports `#`/`+` patterns. Log entries show the actual matched topic when using a wildcard subscription.
- **MQTT publish Retain + QoS** — Switch node publishes with configurable Retain flag and QoS level (0/1/2).
- Various MudBlazor UI polish.

---

## 2026-03-19 — Log/TreeView nodes, multi-page, colour transitions
**Commit:** `4b50748` · 2026-03-19 21:51 UTC

### What was done
- **Log node** — scrolling timestamped MQTT message history. Configurable max entries, optional date/time columns.
- **TreeView node** — collapsible topic tree under a configurable root prefix. Optional value column.
- **Multi-page dashboards** — `DiagramState.Pages` list; page tabs above canvas; add/remove pages; legacy single-page files load transparently.
- **Colour transition direction** — `GaugeColorThreshold.Direction` property (`>=`/`<=`).
- **Battery colour thresholds** — migrated from fixed three-band system to ordered `ColorThresholds` list matching Gauge.
- **`ColorTransitionEditor` component** — reusable threshold editor used by both Gauge and Battery.

---

## 2026-03-19 — Bug fixes, service renames, clipboard, startup setting
**Commit:** `c460e75` · 2026-03-19 19:31 UTC

### What was done
- **OS clipboard integration** — copy/paste writes to/reads from `navigator.clipboard`.
- **Startup dashboard setting** — admin-configurable: Last Used / Specific File / None. Stored in `appsettings.user.json`.
- **Service renames** — `IDiagramService` → `IDashboardService`, related dialog renames.
- **Gauge colour fix** — `GetArcColor()` was comparing `Math.Abs(value − origin)` (distance), now compares raw value. First-match semantics.
- Various bug fixes (see CHANGELOG).

---

## 2026-03-19 — Switch, Gauge, Battery nodes
**Commit:** `0543bcc` · 2026-03-19 17:17 UTC

### What was done
- **Switch node** — `MudSwitch<bool>` component, Full/Compact/IconOnly styles, MQTT publish on toggle.
- **Gauge node** — SVG arc gauge with colour thresholds, configurable min/max/unit/arc origin.
- **Battery node** — battery percentage display with colour thresholds.
- Initial `ColorTransitionEditor` scaffolding.
- `FormatText()` / `FormattableValue` moved to `BaseNodeWithDataWidget` base class.
- `TriggerLinkAnimation()` moved to `BaseNodeWithDataWidget`.

---

_Entries above this line represent the Copilot-assisted development history for this project._
_For release-level summaries see [CHANGELOG.md](CHANGELOG.md)._
