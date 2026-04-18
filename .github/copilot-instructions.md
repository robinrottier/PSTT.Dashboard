# Copilot Instructions for MqttDashboard

## Architecture Overview

```
Browser (InteractiveAuto — WASM preferred, server fallback)
  └─ Blazor Client (MqttDashboard.Client RCL)
       └─ SignalR WebSocket
            └─ ASP.NET Core Server (MqttDashboard.Server)
                 └─ MQTT (MQTTnet 5) → Broker
```

**Projects:**
- **`MqttDashboard.Client`** — Razor class library: all pages, widgets, models, client services. Shared between both hosts.
- **`MqttDashboard.Server`** — Server-side services: MQTT client, SignalR hub, REST API controllers, dashboard file storage.
- **`MqttDashboard.WebApp`** — Primary Blazor host (`InteractiveAuto` render mode, WASM + server-side).
- **`MqttDashboard.WebApp.Client`** — WASM-side entry point for the WebApp host.
- **`MqttDashboard.WebAppServerOnly`** — Alternate Blazor Server-only host (no WASM download; suited to Raspberry Pi).

**Data flow (MQTT → widget):**
1. `MqttClientService` receives a message from the broker.
2. `MqttDataHub` (SignalR) broadcasts it to connected clients.
3. Client `SignalRService` updates `MqttDataCache` (concurrent dict + watcher callbacks).
4. `BaseNodeWithDataWidget.SetupDataWatchers()` subscribed via `DataCache.Watch()` — fires `OnData1ReceivedCore()` + `TriggerLinkAnimation()` + `StateHasChanged()`.

**Diagram engine:** `rrSoft.Blazor.Diagrams` (fork of Blazor.Diagrams). Nodes are `MudNodeModel` subclasses; ports are `MudPortModel`; links are `LinkModel`. The central orchestrator is `ApplicationState.cs` (~800 lines).

### Future plans

MqttDashBoard.Client project is blazor heavy. It might make sense to add a ".Core" project for pure C# models and services that can be shared with a potential non-blazor UI (e.g. Electron.NET or Avalonia) in the future and also to aid unit testing.

Likewise ".Server" has a lot of blazor and perhaps non-blazor services could be moved to other projects.
For example all things to do with Mqtt could be in a "MqttDashboard.Mqtt" project with no blazor dependencies and then both the Blazor server and potential non-blazor hosts could reference that.
Or other functional group of code logic into other smaller discrete projects that again can be untit tsted in isolation and shared with potential other UI frameworks in the future.

Perhaps "..WebServerOnly" could be combined into a single hosting project, maybe with different port for side-x-side hosting of various blazor models.

IN longer term they may be a MAUI desktop application that combines all aspect of this app (server/client rolled into one), using MAUI Blazor web view. This would run standalone, so the structure should always support that in the future.

### Test projects

Currently unit tests are in `MqttDashboard.Client.Tests` and `MqttDashboard.Server.Tests`. Both use xUnit and Moq. Test the client-side code by mocking the `SignalRService` and `MqttDataCache` to simulate incoming MQTT messages and verify widget state updates.

Some whole system tests with a headless browser (Playwright) would be ideal to cover the full flow from MQTT message to rendered widget, but this is not implemented yet.

OR some other way to remove the browser aspect but still obtain a full end-to-end test of the code.

Code coverage is currently pretty light, especially on the client side due to no full system tests to make that possible. Adding more unit tests for the core services and models would be a good idea.

---

## Build & Test Commands

```bash
# One-time setup
dotnet workload install wasm-tools

# Build
dotnet build MqttDashboard.slnx

# Run all tests
dotnet test MqttDashboard.slnx

# Run a single test project
dotnet test tests/MqttDashboard.Client.Tests/MqttDashboard.Client.Tests.csproj
dotnet test tests/MqttDashboard.Server.Tests/MqttDashboard.Server.Tests.csproj

# Run with Docker (builds from source)
docker compose up --build
```

> **Note:** `dotnet build` may fail with file-lock errors while Visual Studio has the solution open. Build the `MqttDashboard.Client` project directly as a workaround, or build from within VS.

---

## Widget Development Guide

All widgets sholud try and use class hierarcy and shared components where possible to avoid code duplication.

Where similar functioanlity exists between 2 widgets that sholud be extracted to a common base class.

Re-used functionality in property setting and diaglogs should be extracted to shared components where possible.

Color setting is a common example of this, where the same color picker component can be used for multiple properties across different widgets.

Ultimately the list of widget types should be in a map or other structures to support easier adding of new types without
needing to edit multiple files to add the various switych/cases for the new type

And support for widgets in external files as some sort of addon or plugin model.

### Adding a New Node/Widget Type

Follow this checklist — every existing widget (Gauge, Log, Switch, Battery, TreeView, Image, Grid) uses the same pattern.

#### 1. Create the model — `src/MqttDashboard.Client/Models/`

```csharp
public class MyNodeModel : MudNodeModel
{
    public MyNodeModel(Point? position = null) : base(position)
    {
        NodeType = "My";          // unique discriminator string
    }
    public string MyProp { get; set; } = "default";
}
```

#### 2. Add persistence fields — `Models/DiagramState.cs` (`NodeState` class)

```csharp
public string? MyProp { get; set; }
```

#### 3. Create the widget — `src/MqttDashboard.Client/Widgets/MyNodeWidget.razor`

```razor
@inherits BaseNodeWithDataWidget<MyNodeModel>

<div @ondblclick="OnDoubleClick">
    @Node.DataValue
    @foreach (var port in Node.Ports)
    {
        <PortRenderer @key="port" Port="port" Style="@PortStyle(port as MudPortModel)" />
    }
</div>

@code {
    protected override void OnData1Updated() => StateHasChanged();
}
```

Use `BaseNodeWidget<T>` instead of `BaseNodeWithDataWidget<T>` only if the node never subscribes to MQTT topics.

#### 4. Register in `ApplicationState.cs`

In **`CreateDiagramFromState()`** — add a `case` in the `NodeType` switch and call `diagram.RegisterComponent<MyNodeModel, MyNodeWidget>()`.

In **`GetDiagramState()`** — add an `else if (node is MyNodeModel m)` block to populate `NodeState.MyProp`.

#### 5. Wire up the UI

- **`NodePropertyEditor.razor`** — add an `@if (Node is MyNodeModel)` section for type-specific properties.
- **`NodePropertyEditor.razor.cs`** — add any helper methods.
- **`NodeTypePickerDialog.razor`** — add an entry to the type picker.
- **`AppMenu.razor`** — add an "Add → My Node" menu item calling `AddNode("My")`.
- **`Display.razor.cs` `AddNode()`** — add a `case "My"` that calls `AppState.AddNodeToActivePage(new MyNodeModel(...))`.

---

## Key Conventions

### Widget base classes
- `BaseNodeWithDataWidget<T>` — for nodes that watch MQTT topics. Manages `DataCache.Watch()` subscriptions, link animation, `DataValue`/`DataValue2`.
- `BaseNodeWidget<T>` — raw base, no MQTT wiring. Use when the widget manages its own subscriptions (e.g. `GridNodeWidget` uses per-cell watchers).
- Never mutate `_entries` or other render-thread collections in place from MQTT callbacks — build a new collection and assign the reference atomically.

### State persistence
- `DiagramState` / `PageState` / `NodeState` (in `Models/DiagramState.cs`) are the serialisable models written to `data/diagram.json`.
- `ApplicationState.GetDiagramState()` serialises; `CreateDiagramFromState()` deserialises.
- `NodeState.NodeType` is the discriminator for round-tripping polymorphic models.
- Legacy single-page files have null `Pages`; the multi-page code handles this transparently.

### Link animations
- Controlled by `MudNodeModel.LinkAnimation` ("None" | "Forward" | "Reverse").
- `TriggerLinkAnimation()` in `BaseNodeWithDataWidget` sets `link.Animations[0].To` based on the sign of `DataValue`. Call it whenever data arrives or when seeding from cache.
- `ApplicationState.CheckForLinkAnimation()` adds the `AnimateModel` to a link at creation time.

### Undo/redo
- Call `AppState.PushUndoSnapshot()` before any destructive edit. Max depth 20.
- Located in `ApplicationState.cs`.

### JavaScript interop
- Only `localStorage` is used (via `LocalStorageService`). Everything else is pure Blazor/SignalR.

### CSS
- MudBlazor theme handles most styling.
- Global overrides: `src/MqttDashboard.WebApp/MqttDashboard.WebAppServerOnly/wwwroot/app.css`.
- Scoped widget styles: `Widgets/*.razor.css` files (CSS isolation).
- Client-specific styles: `src/MqttDashboard.Client/wwwroot/app.css` (menu/submenu rules).

### Configuration
- Environment variables use `__` as separator (e.g. `MqttSettings__Broker`).
- User-editable runtime settings persist to `data/appsettings.user.json` (not the main `appsettings.json`).

### Render mode awareness
- `AppState.IsInteractive` gates diagram rendering. The canvas (`DiagramCanvas`) is only rendered once `IsInteractive` is true to avoid SSR/WASM handoff flicker.
- `@rendermode InteractiveAuto` is the default for pages.


## Development workflow

### General

Do not worry about Old saved files and backward compatiblity for now, as the format is still evolving. If you need to make breaking changes to the file format, it's fine

### Files

| File | Purpose |
|------|---------|
| `TODO.md` | Backlog — bugs, enhancements, ideas. User edits this between sessions. |
| `DEVCHANGELOG.md` | **Detailed** per-Copilot-session log. One section per batch, newest first. Used for review. |
| `CHANGELOG.md` | Public-facing [Keep a Changelog](https://keepachangelog.com) format. Updated each session under `[Unreleased]`. User/contributor readable — less detail than DEVCHANGELOG. |

### At the start of a session

1. Check `git status`. If the repo is dirty:
   - If only `TODO.md` changed → auto-commit it with message `"chore: update TODO"` and continue.
   - Otherwise → ask the user to stash or commit (suggest `"chore: WIP"`) before starting.
2. Read `TODO.md` to understand the current backlog.
3. Read `DEVCHANGELOG.md` (top section) to understand what was done last session.
4. Any TODOs marked (by other) as DONE should be verified if possible, removed and added to CHANGELOGs

### During a session

- Work through TODO items in batches. Prefer surgical, tested changes.
- If a TODO item is too large or complex, break it into smaller items and add them to `TODO.md` with a reference to the original. Or note it as "needs discussion" with a comment.
- Run existing tests (`dotnet test MqttDashboard.slnx`) after significant changes.

### At the end of a session

1. **Remove completed items from `TODO.md`.**
2. **Update `DEVCHANGELOG.md`** — prepend a new section at the top:
   - Heading: `## YYYY-MM-DD — <short description>`
   - Sub-heading: commit hash + UTC timestamp + branch
   - One sub-section per item: what file(s) changed, why, how it works, any caveats or known remaining issues (use ⚠️).
   - This section is the primary review artifact — write enough detail that each item can be independently verified or pushed back to TODO.
3. **Update `CHANGELOG.md`** under `[Unreleased]` in standard [Keep a Changelog](https://keepachangelog.com) format (`### Added`, `### Fixed`, `### Changed`). This is the user/contributor-facing log — summarise what changed and why it matters, without internal implementation detail. Less verbose than DEVCHANGELOG.
4. **Commit** all changes on the current branch (feature branch or `develop`). Commit message format: `<type>: <short summary>` (e.g. `fix:`, `feat:`, `chore:`, `docs:`). Always include the Co-authored-by trailer.

### Releases (GitHub-initiated)

When a release is published on GitHub, add a section to `CHANGELOG.md` with the release name, date, and a link to the GitHub release notes. The patch-release workflow (`.github/workflows/patch-release.yml`) handles tag bumping automatically; major/minor releases are tagged manually.
