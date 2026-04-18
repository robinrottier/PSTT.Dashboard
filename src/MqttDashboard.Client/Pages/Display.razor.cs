using Blazor.Diagrams;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using MqttDashboard.Models;
using MqttDashboard.Services;
using MqttDashboard.Widgets;
using MqttDashboard.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;

namespace MqttDashboard.Pages;

public partial class Display : IDisposable
{
    [Inject] private ApplicationState AppState { get; set; } = default!;
    [Inject] private IDashboardService DashboardService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    // Multi-page diagram state
    private List<BlazorDiagram?> _diagrams = [null];
    private List<DashboardPageModel> _pageStates = [new DashboardPageModel()];
    private int _activePageIndex = 0;
    private BlazorDiagram? _diagram => _diagrams.Count > _activePageIndex ? _diagrams[_activePageIndex] : null;

    // Pre-edit snapshot for discard revert
    private DashboardModel? _editSnapshot;

    // Suppress dirty tracking during mode switches and diagram loading
    private bool _suppressDirty = false;
    // Deferred dirty flag: set by OnDiagramChanged, cleared by OnSelectionChanged.
    // Ensures selection-only changes (which fire both Changed and SelectionChanged) don't mark the diagram dirty.
    private bool _pendingDirtyMark = false;

    // Inline tab rename state
    private int _renamingPageIndex = -1;
    private string _renameValue = string.Empty;

    private int _nodeCounter = 1;
    private int _pasteGeneration = 0;

    // Stored handler references for clean unsubscription
    private Action? _onMenuSaveDiagram;
    private Action? _onMenuReloadDiagram;
    private Action? _onMenuEditProperties;
    private Action? _onMenuSaveAs;
    private Action? _onMenuOpen;
    private Action? _onMenuUndo;
    private Action? _onMenuRedo;
    private Action? _onMenuUndoAll;
    private Action? _onMenuDiagramProperties;
    private Action? _onMenuPaste;
    private Action? _onMenuExportNodes;
    private Action? _onMenuImportNodes;
    private Action? _onMenuAddPage;
    private Action<int>? _onMenuSetActivePage;
    private DateTimeOffset _lastUndoPushByMove = DateTimeOffset.MinValue;
    private readonly List<(NodeModel Node, Action<Blazor.Diagrams.Core.Models.Base.Model> Handler)> _nodeChangedSubscriptions = new();
    private IDisposable? _locationChangingRegistration;

    private const string LastDashboardKey = "mqttdashboard_lastDiagram";
    private ElementReference _canvasRef;

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // Only handle keyboard shortcuts in edit mode
        if (!AppState.IsEditMode) return;

        // Common clipboard / edit shortcuts
        var ctrl = e.CtrlKey || e.MetaKey;
        if (ctrl)
        {
            switch (e.Key)
            {
                case "c":
                case "C":
                    CopySelectedNodes();
                    break;
                case "x":
                case "X":
                    CutSelectedNodes();
                    break;
                case "v":
                case "V":
                    await PasteNodesAsync();
                    break;
                case "z":
                case "Z":
                    if (e.ShiftKey) await RedoAction(); else await UndoAction();
                    break;
                case "y":
                case "Y":
                    await RedoAction();
                    break;
                case "s":
                case "S":
                    // Ctrl+S: save. If there's no filename yet, open Save As dialog.
                    if (string.IsNullOrEmpty(AppState.DashboardName))
                        await SaveAsDiagram();
                    else
                        await SaveDashboard();
                    break;
            }
        }
        else
        {
            // Non-ctrl shortcuts
            switch (e.Key)
            {
                case "Delete":
                case "Backspace":
                    DeleteSelectedNode();
                    break;
                case "Escape":
                    // Cancel inline rename if active
                    if (_renamingPageIndex != -1) { _renamingPageIndex = -1; StateHasChanged(); }
                    break;
                case "F2":
                    // Start rename for single selected page/tab
                    if (AppState.IsEditMode && AppState.PageNames.Count > 0)
                    {
                        StartRename(_activePageIndex, AppState.PageNames[_activePageIndex]);
                    }
                    break;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();
            AppState.OnToggleEditModeRequested += OnToggleEditModeRequested;
            AppState.OnStateChanged += OnAppStateChanged;

            _locationChangingRegistration = Nav.RegisterLocationChangingHandler(OnLocationChanging);

            // Subscribe Open / OpenRecent for all modes (not just edit mode)
            _onMenuOpen = () => InvokeAsync(OpenDiagram);
            AppState.MenuOpen += _onMenuOpen;

            // Subscribe page navigation for all modes
            _onMenuSetActivePage = idx => { _ = InvokeAsync(() => SwitchToPageAsync(idx)); };
            AppState.MenuSetActivePage += _onMenuSetActivePage;

            var savedState = await DashboardService.LoadDashboardAsync();
            // Preserve runtime subscriptions — the in-memory set may already include
            // topics added this session (e.g. from the Data page). These must not be
            // clobbered when LoadFullState/CreateDiagramFromState sets them from the file.
            var runtimeTopics = AppState.SubscribedTopics.ToHashSet();

            if (savedState != null && savedState.Pages.Count > 0)
            {
                LoadFullState(savedState, readOnly: true);
                if (string.IsNullOrEmpty(AppState.DashboardName))
                    AppState.SetDiagramName("Default");
                if (runtimeTopics.Count > 0)
                    AppState.SetSubscribedTopics(runtimeTopics);
                AppState.MarkSaved();
                StateHasChanged();
                await Task.Delay(100);
                RefreshAll();
                StateHasChanged();
            }
            else
            {
                LoadFullState(null, readOnly: true);
                if (string.IsNullOrEmpty(AppState.DashboardName))
                    AppState.SetDiagramName("Default");
                AppState.MarkSaved();
                StateHasChanged();
            }

            // Try to auto-open the last named diagram if none was loaded by name
            if (string.IsNullOrEmpty(AppState.DashboardName))
            {
                var lastName = await GetLastDiagramName();
                if (!string.IsNullOrEmpty(lastName))
                {
                    var lastState = await DashboardService.LoadDashboardByNameAsync(lastName);
                    if (lastState != null)
                    {
                        LoadFullState(lastState, readOnly: true);
                        AppState.SetDiagramName(lastName);
                        if (runtimeTopics.Count > 0)
                            AppState.SetSubscribedTopics(runtimeTopics);
                        AppState.MarkSaved();
                        StateHasChanged();
                    }
                }
            }
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    // ── Full state loading (replaces all pages) ───────────────────────────────

    private void LoadFullState(DashboardModel? state, bool readOnly)
    {
        // Unsubscribe selection/change from all existing diagrams
        foreach (var d in _diagrams.OfType<BlazorDiagram>())
        {
            d.SelectionChanged -= OnSelectionChanged;
            d.Changed -= OnDiagramChanged;
        }
        AppState.ResetDiagram();

        _suppressDirty = true;
        try
        {
            if (state != null)
                AppState.ApplyDashboardModel(state);

            if (state?.Pages != null && state.Pages.Count > 0)
            {
                var pageNames = state.Pages.Select(p => p.Name).ToList();
                _pageStates = new List<DashboardPageModel>(state.Pages);
                _diagrams = new List<BlazorDiagram?>(Enumerable.Repeat<BlazorDiagram?>(null, _pageStates.Count));
                _activePageIndex = 0;
                _diagrams[0] = AppState.CreateDiagramFromPageData(_pageStates[0], readOnly);
                AppState.SetPageNames(pageNames, 0);
            }
            else
            {
                _pageStates = [new DashboardPageModel { GridSize = AppState.GridSize > 0 ? AppState.GridSize : 10 }];
                _diagrams = [null];
                _activePageIndex = 0;
                _diagrams[0] = AppState.GetOrCreateDashboard();
                AppState.SetPageNames(["Page 1"], 0);
            }
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private DashboardModel BuildFullState()
    {
        // Capture current page state
        var currentPage = AppState.GetPageData();
        _pageStates[_activePageIndex] = currentPage;

        var fileInfo = new DashboardFileInfo
        {
            WrittenAt = DateTimeOffset.UtcNow.ToString("o"),
            Filename  = !string.IsNullOrEmpty(AppState.DashboardName) ? AppState.DashboardName : null,
        };

        return new DashboardModel
        {
            Name = AppState.DashboardDisplayName,
            ShowName = AppState.ShowName,
            MqttSubscriptions = new HashSet<string>(AppState.SubscribedTopics),
            Pages = _pageStates.Select((ps, i) => new DashboardPageModel
            {
                Id = ps.Id,
                Name = i < AppState.PageNames.Count ? AppState.PageNames[i] : $"Page {i + 1}",
                GridSize = ps.GridSize,
                GridSnapToCenter = ps.GridSnapToCenter,
                BackgroundColor = ps.BackgroundColor,
                Nodes = ps.Nodes,
                Links = ps.Links,
            }).ToList(),
            FileInfo = fileInfo,
        };
    }

    private void RefreshAll()
    {
        if (_diagram == null) return;
        foreach (var node in _diagram.Nodes) node.Refresh();
        foreach (var link in _diagram.Links) link.Refresh();
        _diagram.Refresh();
    }

    private void OnAppStateChanged() => InvokeAsync(StateHasChanged);

    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (AppState.IsEditMode && AppState.IsEdited)
        {
            var confirmed = await DialogService.ShowMessageBoxAsync(
                "Unsaved Changes",
                "You have unsaved changes that will be lost. Leave without saving?",
                yesText: "Leave",
                cancelText: "Stay");
            if (confirmed != true)
                context.PreventNavigation();
        }
    }

    // ── Mode switching ────────────────────────────────────────────────────────

    private void OnToggleEditModeRequested()
    {
        InvokeAsync(async () => await SwitchMode(!AppState.IsEditMode));
    }

    private async Task SwitchMode(bool enterEditMode)
    {
        if (_diagram == null) return;

        if (!enterEditMode && AppState.IsEdited)
        {
            if (AppState.AutoSaveOnExitEditMode)
            {
                var saved = await SaveDashboard();
                if (!saved) return; // Stay in edit mode if save failed (e.g. no filename yet)
            }
            else
            {
                var confirm = await DialogService.ShowMessageBoxAsync(
                    "Unsaved Changes",
                    "You have unsaved changes. Save before leaving edit mode?",
                    yesText: "Save",
                    noText: "Discard",
                    cancelText: "Cancel");
                if (confirm == null) return; // Cancel — stay in edit mode
                if (confirm == true)
                {
                    var saved = await SaveDashboard();
                    if (!saved) return; // Stay in edit mode if save failed
                }
                else
                {
                    // Discard — revert to pre-edit snapshot
                    if (_editSnapshot != null)
                    {
                        UnsubscribeEditEvents();
                        _suppressDirty = true;
                        try { LoadFullState(_editSnapshot, readOnly: true); }
                        finally { _suppressDirty = false; }
                        AppState.SetEditMode(false);
                        AppState.MarkSaved();
                        AppState.UpdateSelectionState(false, false);
                        StateHasChanged();
                        return;
                    }
                    AppState.MarkSaved();
                }
            }
        }

        if (enterEditMode)
        {
            // Snapshot current state before any edit-mode changes
            _editSnapshot = BuildFullState();
            // Start each edit session with a clean undo stack
            AppState.ClearUndoRedo();
        }

        if (AppState.IsEditMode)
            UnsubscribeEditEvents();

        if (AppState.IsEditMode && !enterEditMode)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }

        _suppressDirty = true;
        try
        {
            foreach (var node in _diagram.Nodes)
            {
                node.Locked = !enterEditMode;
                if (enterEditMode)
                    _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
                else
                    _diagram.Controls.RemoveFor(node);
            }

            foreach (var link in _diagram.Links)
                link.Locked = !enterEditMode;

            if (enterEditMode)
            {
                if (_diagram.Options.GridSize == null)
                {
                    // Diagram was created read-only (GridSize not set in options).
                    // Restore from the saved page state.
                    var savedGs = _pageStates[_activePageIndex].GridSize;
                    _diagram.Options.GridSize = Math.Clamp(savedGs == 0 ? 20 : Math.Abs(savedGs), 5, 100);
                    _diagram.Options.GridSnapToCenter = _pageStates[_activePageIndex].GridSnapToCenter;
                }
                AppState.SetGridSize(_diagram.Options.GridSize.HasValue ? (int)_diagram.Options.GridSize.Value : 0);
                _diagram.Options.AllowMultiSelection = true;
                _diagram.SelectionChanged += OnSelectionChanged;
                _diagram.Changed += OnDiagramChanged;
                SubscribeEditEvents();
                UpdateSelectionState();
            }
            else
            {
                _diagram.Options.GridSize = null; // no grid in view mode
                _diagram.Options.AllowMultiSelection = false;
                _diagram.UnselectAll();
            }
        }
        finally
        {
            _suppressDirty = false;
        }

        AppState.SetEditMode(enterEditMode);
        // Clear any dirty flag spuriously raised during mode-switch setup
        if (enterEditMode) AppState.MarkSaved();
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
        // Suppress any spurious Changed events fired during RefreshAll
        if (enterEditMode) AppState.MarkSaved();
        // Focus the canvas so keyboard shortcuts work immediately when entering edit mode
        if (enterEditMode)
        {
            try { await Task.Delay(50); await _canvasRef.FocusAsync(); } catch { /* ignore */ }
        }
    }

    private void SubscribeEditEvents()
    {
        _diagram!.Links.Added   += OnLinkAdded;
        _diagram!.Links.Removed += OnLinkRemoved;
        AppState.MenuAddNode       += AddNode;
        AppState.MenuDeleteNode    += DeleteSelectedNode;
        AppState.MenuCutSelected   += CutSelectedNodes;
        AppState.MenuCopySelected  += CopySelectedNodes;
        _onMenuPaste = () => InvokeAsync(PasteNodesAsync);
        AppState.MenuPasteSelected += _onMenuPaste;
        _onMenuExportNodes = () => InvokeAsync(ExportNodesAsync);
        AppState.MenuExportNodes += _onMenuExportNodes;
        _onMenuImportNodes = () => InvokeAsync(ImportNodesAsync);
        AppState.MenuImportNodes += _onMenuImportNodes;
        AppState.MenuAddPort       += AddPortToSelectedNode;
        AppState.MenuAddAllPorts   += AddAllPortsToSelectedNode;
        AppState.MenuDeletePort    += DeletePortFromSelectedNode;
        AppState.MenuNewDiagram    += NewDiagram;

        _onMenuSaveDiagram    = () => InvokeAsync(async () => { await SaveDashboard(); });
        _onMenuReloadDiagram  = () => InvokeAsync(ReloadDiagram);
        _onMenuEditProperties = () => InvokeAsync(EditNodeProperties);
        _onMenuSaveAs         = () => InvokeAsync(SaveAsDiagram);
        _onMenuUndo           = () => InvokeAsync(UndoAction);
        _onMenuRedo           = () => InvokeAsync(RedoAction);
        _onMenuUndoAll        = () => InvokeAsync(UndoAllAction);

        AppState.MenuSaveDiagram    += _onMenuSaveDiagram;
        AppState.MenuReloadDiagram  += _onMenuReloadDiagram;
        AppState.MenuEditProperties += _onMenuEditProperties;
        AppState.MenuSaveAs         += _onMenuSaveAs;
        AppState.MenuUndo           += _onMenuUndo;
        AppState.MenuRedo           += _onMenuRedo;
        AppState.MenuUndoAll        += _onMenuUndoAll;

        _onMenuDiagramProperties = () => InvokeAsync(ShowDiagramPropertiesAsync);
        AppState.MenuDiagramProperties += _onMenuDiagramProperties;

        _onMenuAddPage = () => InvokeAsync(AddPageAsync);
        AppState.MenuAddPage += _onMenuAddPage;

        // Subscribe to existing nodes' Changed events to detect moves
        foreach (var node in _diagram!.Nodes.OfType<NodeModel>())
            SubscribeToNodeChanges(node);
        // Subscribe to future nodes
        _diagram.Nodes.Added += OnNodeAddedInEditMode;
    }

    private void UnsubscribeEditEvents()
    {
        _diagram?.Links.Added   -= OnLinkAdded;
        _diagram?.Links.Removed -= OnLinkRemoved;
        AppState.MenuAddNode       -= AddNode;
        AppState.MenuDeleteNode    -= DeleteSelectedNode;
        AppState.MenuCutSelected   -= CutSelectedNodes;
        AppState.MenuCopySelected  -= CopySelectedNodes;
        if (_onMenuPaste != null) AppState.MenuPasteSelected -= _onMenuPaste;
        if (_onMenuExportNodes != null) AppState.MenuExportNodes -= _onMenuExportNodes;
        if (_onMenuImportNodes != null) AppState.MenuImportNodes -= _onMenuImportNodes;
        AppState.MenuAddPort       -= AddPortToSelectedNode;
        AppState.MenuAddAllPorts   -= AddAllPortsToSelectedNode;
        AppState.MenuDeletePort    -= DeletePortFromSelectedNode;
        AppState.MenuNewDiagram    -= NewDiagram;

        if (_onMenuSaveDiagram    != null) AppState.MenuSaveDiagram    -= _onMenuSaveDiagram;
        if (_onMenuReloadDiagram  != null) AppState.MenuReloadDiagram  -= _onMenuReloadDiagram;
        if (_onMenuEditProperties != null) AppState.MenuEditProperties -= _onMenuEditProperties;
        if (_onMenuSaveAs         != null) AppState.MenuSaveAs         -= _onMenuSaveAs;
        if (_onMenuUndo           != null) AppState.MenuUndo           -= _onMenuUndo;
        if (_onMenuRedo           != null) AppState.MenuRedo           -= _onMenuRedo;
        if (_onMenuUndoAll        != null) AppState.MenuUndoAll        -= _onMenuUndoAll;

        if (_onMenuDiagramProperties != null) AppState.MenuDiagramProperties -= _onMenuDiagramProperties;
        if (_onMenuAddPage           != null) AppState.MenuAddPage           -= _onMenuAddPage;

        _diagram?.Nodes.Added -= OnNodeAddedInEditMode;
        foreach (var (node, handler) in _nodeChangedSubscriptions)
            node.Changed -= handler;
        _nodeChangedSubscriptions.Clear();

        _onMenuSaveDiagram = _onMenuReloadDiagram = _onMenuEditProperties = null;
        _onMenuSaveAs = _onMenuUndo = _onMenuRedo = _onMenuUndoAll = _onMenuDiagramProperties = _onMenuAddPage = null;
        _onMenuExportNodes = _onMenuImportNodes = null;
    }

    // ── Diagram event handlers ────────────────────────────────────────────────

    private void OnSelectionChanged(object model)
    {
        // Clear both immediately and deferred: if SelectionChanged fires before node.Changed
        // (Blazor.Diagrams may fire selection first), the deferred clear ensures the flag
        // set by the subsequent node.Changed callback is still nullified.
        _pendingDirtyMark = false;
        _ = InvokeAsync(() => _pendingDirtyMark = false);
        UpdateSelectionState();
        InvokeAsync(StateHasChanged);
    }

    private void OnDiagramChanged()
    {
        // Keep the UI in sync for link add/remove and other diagram-level changes.
        // Dirty tracking is handled per-node (OnNodeChanged) and per-link (OnLinkAdded/Removed).
        InvokeAsync(StateHasChanged);
    }

    private void OnLinkAdded(Blazor.Diagrams.Core.Models.Base.BaseLinkModel link)
    {
        if (link is not LinkModel lm) return;
        if ((link.Source?.Model is PortModel port ? port.Parent : link.Source?.Model) is NodeModel sourceNode)
            AppState.CheckForLinkAnimation(sourceNode, lm);
        if (!_suppressDirty) { AppState.MarkEdited(); PushUndoSnapshot(); }
    }

    private void OnLinkRemoved(Blazor.Diagrams.Core.Models.Base.BaseLinkModel link)
    {
        if (!_suppressDirty) { AppState.MarkEdited(); PushUndoSnapshot(); }
    }

    private void UpdateSelectionState()
    {
        var selected = _diagram?.GetSelectedModels().OfType<NodeModel>().ToList() ?? [];
        HashSet<PortAlignment>? ports = null;
        if (selected.Count == 1)
            ports = selected[0].Ports.OfType<NodePortModel>().Select(p => p.Alignment).ToHashSet();
        AppState.UpdateSelectionState(selected.Count > 0, selected.Count == 1, ports);
    }

    // ── Node operations ───────────────────────────────────────────────────────

    private async void AddNode()
    {
        if (_diagram == null) return;

        var dialog = await DialogService.ShowAsync<NodeTypePickerDialog>("Add Node",
            new DialogParameters(),
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true });
        var result = await dialog.Result;
        if (result == null || result.Canceled || result.Data is not string nodeType) return;

        PushUndoSnapshot();
        var rng = new Random();
        _diagram.UnselectAll();

        TextNodeModel node = nodeType switch
        {
            "Gauge"    => new GaugeNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))    { Title = $"Gauge {_nodeCounter++}" },
            "Switch"   => new SwitchNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))   { Title = $"Switch {_nodeCounter++}" },
            "Battery"  => new BatteryNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))  { Title = $"Battery {_nodeCounter++}" },
            "Log"      => new LogNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))      { Title = $"Log {_nodeCounter++}" },
            "TreeView" => new TreeViewNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400))) { Title = $"Tree {_nodeCounter++}" },
            _          => new TextNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))     { Title = $"Node {_nodeCounter++}" },
        };

        _diagram.Nodes.Add(node);
        _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
        _diagram.SelectModel(node, false);
        UpdateSelectionState();
        StateHasChanged();
    }

    private void DeleteSelectedNode()
    {
        if (_diagram == null) return;
        PushUndoSnapshot();
        foreach (var n in _diagram.GetSelectedModels().OfType<NodeModel>().ToList())
            _diagram.Nodes.Remove(n);
        UpdateSelectionState();
        StateHasChanged();
    }

    private void NewDiagram()
    {
        InvokeAsync(async () =>
        {
            if (AppState.IsEdited)
            {
                bool confirmed = await ConfirmDiscardChanges("New dashboard");
                if (!confirmed) return;
            }
            PushUndoSnapshot();

            // Unsubscribe from current diagram
            if (_diagram != null)
            {
                _diagram.SelectionChanged -= OnSelectionChanged;
                _diagram.Changed -= OnDiagramChanged;
            }
            UnsubscribeEditEvents();

            AppState.ResetDiagram();
            AppState.SetDiagramName(string.Empty);
            AppState.SetDisplayName(string.Empty);
            AppState.MarkSaved();
            AppState.ClearUndoRedo();

            _pageStates = [new DashboardPageModel { GridSize = AppState.GridSize > 0 ? AppState.GridSize : 10 }];
            _diagrams = [null];
            _activePageIndex = 0;
            AppState.SetPageNames(["Page 1"], 0);

            _diagrams[0] = AppState.GetOrCreateDashboard();
            _diagram!.SelectionChanged += OnSelectionChanged;
            _diagram!.Changed += OnDiagramChanged;
            SubscribeEditEvents();

            _nodeCounter = 1;
            UpdateSelectionState();
            Snackbar.Add("New dashboard created", Severity.Info);
            StateHasChanged();
        });
    }

    private async Task ReloadDiagram()
    {
        if (AppState.IsEdited)
        {
            bool confirmed = await ConfirmDiscardChanges("Reload dashboard");
            if (!confirmed) return;
        }
        if (AppState.IsEditMode)
        {
            _diagram?.SelectionChanged -= OnSelectionChanged;
            _diagram?.Changed -= OnDiagramChanged;
            UnsubscribeEditEvents();
        }

        AppState.SetEditMode(false);
        AppState.MarkSaved();
        AppState.ClearUndoRedo();
        var savedState = await DashboardService.LoadDashboardAsync();
        if (savedState != null && savedState.Pages.Count > 0)
        {
            var prevTopics = AppState.SubscribedTopics.ToHashSet();
            LoadFullState(savedState, readOnly: true);
            await SyncSubscriptionsAsync(prevTopics, AppState.SubscribedTopics);
            var nodeCount = savedState.Pages.Sum(p => p.Nodes.Count);
            Snackbar.Add($"Dashboard reloaded ({nodeCount} nodes)", Severity.Info);
        }
        else
        {
            LoadFullState(null, readOnly: true);
            Snackbar.Add("No saved dashboard found", Severity.Warning);
        }
        StateHasChanged();
    }

    // ── Page management ───────────────────────────────────────────────────────

    private async Task SwitchToPageAsync(int index)
    {
        if (index == _activePageIndex) return;
        if (index < 0 || index >= _pageStates.Count) return;

        // Save current page state
        if (_diagram != null)
            _pageStates[_activePageIndex] = AppState.GetPageData();

        // Unsubscribe diagram-specific events from current page
        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
            _diagram.Links.Added -= OnLinkAdded;
            _diagram.Nodes.Added -= OnNodeAddedInEditMode;
            foreach (var (node, handler) in _nodeChangedSubscriptions)
                node.Changed -= handler;
            _nodeChangedSubscriptions.Clear();
        }

        // Switch page
        _activePageIndex = index;
        AppState.SetActivePage(index);

        // Create diagram for the new page (always fresh to handle mode changes)
        _suppressDirty = true;
        try { _diagrams[_activePageIndex] = AppState.CreateDiagramFromPageData(_pageStates[_activePageIndex], !AppState.IsEditMode); }
        finally { _suppressDirty = false; }

        // Re-subscribe diagram events for the new page
        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            _diagram.Links.Added += OnLinkAdded;
            _diagram.Nodes.Added += OnNodeAddedInEditMode;
            foreach (var node in _diagram.Nodes.OfType<NodeModel>())
                SubscribeToNodeChanges(node);
            UpdateSelectionState();
        }

        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    private async Task AddPageAsync()
    {
        var newPageName = $"Page {_pageStates.Count + 1}";
        var newPageState = new DashboardPageModel { GridSize = AppState.GridSize > 0 ? AppState.GridSize : 10 };
        _pageStates.Add(newPageState);
        _diagrams.Add(null);
        var newNames = new List<string>(AppState.PageNames) { newPageName };
        AppState.SetPageNames(newNames, _activePageIndex);
        await SwitchToPageAsync(_pageStates.Count - 1);
        AppState.MarkEdited();
    }

    private async Task RemovePageAsync(int index)
    {
        if (_pageStates.Count <= 1) return;

        var pageName = index < AppState.PageNames.Count ? AppState.PageNames[index] : $"Page {index + 1}";
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Delete Page",
            $"Delete '{pageName}'? All widgets on this page will be lost.",
            yesText: "Delete",
            cancelText: "Cancel");
        if (confirm != true) return;

        // Save current page before removing
        if (_diagram != null && index == _activePageIndex)
            _pageStates[_activePageIndex] = AppState.GetPageData();

        // Unsubscribe from the diagram being removed (if in edit mode)
        if (AppState.IsEditMode)
        {
            var removingDiagram = _diagrams.Count > index ? _diagrams[index] : null;
            if (removingDiagram != null)
            {
                removingDiagram.SelectionChanged -= OnSelectionChanged;
                removingDiagram.Changed -= OnDiagramChanged;
                removingDiagram.Links.Added -= OnLinkAdded;
                removingDiagram.Nodes.Added -= OnNodeAddedInEditMode;
            }
            if (index == _activePageIndex)
            {
                foreach (var (node, handler) in _nodeChangedSubscriptions)
                    node.Changed -= handler;
                _nodeChangedSubscriptions.Clear();
            }
        }

        _pageStates.RemoveAt(index);
        _diagrams.RemoveAt(index);
        var newNames = new List<string>(AppState.PageNames);
        newNames.RemoveAt(index);
        var newActive = Math.Clamp(_activePageIndex >= index ? _activePageIndex - 1 : _activePageIndex, 0, _pageStates.Count - 1);
        _activePageIndex = newActive;
        AppState.SetPageNames(newNames, newActive);

        // Create diagram for the now-active page if needed
        if (_diagrams[_activePageIndex] == null)
            _diagrams[_activePageIndex] = AppState.CreateDiagramFromPageData(_pageStates[_activePageIndex], !AppState.IsEditMode);
        else
            AppState.SetActiveDiagram(_diagrams[_activePageIndex]);

        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            _diagram.Links.Added += OnLinkAdded;
            _diagram.Nodes.Added += OnNodeAddedInEditMode;
            foreach (var node in _diagram.Nodes.OfType<NodeModel>())
                SubscribeToNodeChanges(node);
            UpdateSelectionState();
        }

        AppState.MarkEdited();
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    private void StartRename(int index, string currentName)
    {
        _renamingPageIndex = index;
        _renameValue = currentName;
        StateHasChanged();
    }

    private async Task CommitRename(int index)
    {
        _renamingPageIndex = -1;
        await RenamePageAsync(index, _renameValue);
    }

    private Task RenamePageAsync(int index, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return Task.CompletedTask;
        if (index < 0 || index >= _pageStates.Count) return Task.CompletedTask;
        var newNames = new List<string>(AppState.PageNames);
        newNames[index] = newName.Trim();
        AppState.SetPageNames(newNames, _activePageIndex);
        AppState.MarkEdited();
        StateHasChanged();
        return Task.CompletedTask;
    }
    // Clipboard tag written to the OS clipboard so we can recognise our own data on paste.
    private const string ClipboardTag = """{"mqttdashboard":"nodes",""";

    private static List<NodeData> BuildSnapshots(IEnumerable<TextNodeModel> selected)
        => selected.Select(n => n.ToData()).ToList();

    private void CopySelectedNodes() => CopySelectedNodes(true);

    private void CopySelectedNodes(bool showSnackbar)
    {
        if (_diagram == null) return;
        var selected = _diagram.GetSelectedModels().OfType<TextNodeModel>().ToList();
        if (selected.Count == 0) return;
        _pasteGeneration = 0;
        var snapshots = BuildSnapshots(selected);
        AppState.SetClipboard(snapshots);

        // Also write to the OS clipboard so paste works across browser windows.
        var json = System.Text.Json.JsonSerializer.Serialize(new { mqttdashboard = "nodes", data = snapshots });
        _ = JSRuntime.InvokeAsync<bool>("mqttClipboard.writeText", json).AsTask()
              .ContinueWith(_ => { });

        if (showSnackbar)
            Snackbar.Add($"Copied {snapshots.Count} node(s)", Severity.Info);
    }

    private void CutSelectedNodes()
    {
        if (_diagram == null) return;
        var selected = _diagram.GetSelectedModels().OfType<TextNodeModel>().ToList();
        if (selected.Count == 0) return;
        var count = selected.Count;

        _pasteGeneration = 0;
        // Copy to clipboard but suppress the "Copied" snackbar — we'll show "Cut" instead.
        CopySelectedNodes(false);

        PushUndoSnapshot();
        foreach (var n in _diagram!.GetSelectedModels().OfType<NodeModel>().ToList())
            _diagram.Nodes.Remove(n);
        UpdateSelectionState();
        Snackbar.Add($"Cut {count} node(s)", Severity.Info);
        StateHasChanged();
    }

    private async Task PasteNodesAsync()
    {
        if (_diagram == null) return;

        List<NodeData>? toPaste = null;
        try
        {
            var text = await JSRuntime.InvokeAsync<string?>("mqttClipboard.readText");
            if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("{\"mqttdashboard\":\"nodes\"", StringComparison.Ordinal))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    toPaste = System.Text.Json.JsonSerializer.Deserialize<List<NodeData>>(dataEl.GetRawText());
                    if (toPaste != null)
                        AppState.SetClipboard(toPaste);
                }
            }
        }
        catch { /* fall back to in-memory clipboard */ }

        if (toPaste == null)
        {
            if (!AppState.HasClipboard) return;
            toPaste = AppState.Clipboard.ToList();
        }

        if (toPaste.Count == 0) return;

        PushUndoSnapshot();
        _pasteGeneration++;
        _diagram.UnselectAll();
        double offset = 30 * _pasteGeneration;

        foreach (var nodeData in toPaste)
        {
            TextNodeModel node = nodeData switch
            {
                GaugeNodeData d    => GaugeNodeModel.FromData(d),
                SwitchNodeData d   => SwitchNodeModel.FromData(d),
                BatteryNodeData d  => BatteryNodeModel.FromData(d),
                LogNodeData d      => LogNodeModel.FromData(d),
                TreeViewNodeData d => TreeViewNodeModel.FromData(d),
                _                  => TextNodeModel.FromData(nodeData),
            };
            // Offset paste position
            node.SetPosition(nodeData.X + offset, nodeData.Y + offset);

            foreach (var ps in nodeData.Ports ?? [])
            {
                if (Enum.TryParse<Blazor.Diagrams.Core.Models.PortAlignment>(ps.Alignment, out var alignment))
                    AppState.AddPortToNode(node, alignment);
            }
            _diagram.Nodes.Add(node);
            _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
            _diagram.SelectModel(node, false);
        }
        UpdateSelectionState();
        Snackbar.Add($"Pasted {toPaste.Count} node(s)", Severity.Info);
        StateHasChanged();
    }

    // ── Export / Import ───────────────────────────────────────────────────────

    private async Task ExportNodesAsync()
    {
        if (_diagram == null) return;
        var selected = _diagram.GetSelectedModels().OfType<TextNodeModel>().ToList();
        var selectedNodes = BuildSnapshots(selected);
        var currentPage = AppState.GetPageData();

        var parameters = new DialogParameters<ExportNodesDialog>
        {
            { d => d.SelectedNodes, selectedNodes },
            { d => d.PageData, currentPage },
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true };
        await DialogService.ShowAsync<ExportNodesDialog>("Export", parameters, options);
    }

    private async Task ImportNodesAsync()
    {
        if (_diagram == null) return;
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<ImportNodesDialog>("Import", new DialogParameters(), options);
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: ImportResult importResult }) return;

        PushUndoSnapshot();

        if (importResult.AddAsNewPage)
        {
            var newPageData = new DashboardPageModel
            {
                Name = $"Page {_pageStates.Count + 1}",
                GridSize = Math.Max(5, AppState.GridSize),
                GridSnapToCenter = AppState.GridSnapToCenter,
                Nodes = importResult.Nodes,
                Links = importResult.Links,
            };
            _pageStates.Add(newPageData);
            _diagrams.Add(null);
            var newNames = new List<string>(AppState.PageNames) { newPageData.Name };
            AppState.SetPageNames(newNames, _activePageIndex);
            await SwitchToPageAsync(_pageStates.Count - 1);
        }
        else
        {
            _diagram.UnselectAll();
            foreach (var nodeData in importResult.Nodes)
            {
                TextNodeModel node = nodeData switch
                {
                    GaugeNodeData d    => GaugeNodeModel.FromData(d),
                    SwitchNodeData d   => SwitchNodeModel.FromData(d),
                    BatteryNodeData d  => BatteryNodeModel.FromData(d),
                    LogNodeData d      => LogNodeModel.FromData(d),
                    TreeViewNodeData d => TreeViewNodeModel.FromData(d),
                    _                  => TextNodeModel.FromData(nodeData),
                };
                foreach (var ps in nodeData.Ports ?? [])
                {
                    if (Enum.TryParse<Blazor.Diagrams.Core.Models.PortAlignment>(ps.Alignment, out var alignment))
                        AppState.AddPortToNode(node, alignment);
                }
                _diagram.Nodes.Add(node);
                _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
                _diagram.SelectModel(node, false);
            }
            UpdateSelectionState();
        }

        AppState.MarkEdited();
        Snackbar.Add($"Imported {importResult.Nodes.Count} node(s)", Severity.Success);
        StateHasChanged();
    }


    private void PushUndoSnapshot()
    {
        if (_diagram == null) return;
        AppState.PushUndoSnapshot(AppState.GetPageData());
    }

    private async Task UndoAction()
    {
        if (_diagram == null || !AppState.CanUndo) return;
        var current = AppState.GetPageData();
        var previous = AppState.PopUndo(current);
        if (previous == null) return;
        await ApplyDiagramState(previous);
        Snackbar.Add("Undo", Severity.Info);
    }

    private async Task UndoAllAction()
    {
        if (_diagram == null || !AppState.CanUndo || _editSnapshot == null) return;

        // Unsubscribe from all current diagrams before replacing them
        UnsubscribeEditEvents();
        foreach (var d in _diagrams.OfType<BlazorDiagram>())
        {
            d.SelectionChanged -= OnSelectionChanged;
            d.Changed -= OnDiagramChanged;
        }

        _suppressDirty = true;
        try
        {
            // LoadFullState handles both single-page and multi-page snapshots correctly.
            // ApplyDiagramState only restores a single page and cannot revert page-count changes.
            LoadFullState(_editSnapshot, readOnly: false);
        }
        finally { _suppressDirty = false; }

        // Re-attach edit-mode event handlers for the (now active) page
        _diagram!.SelectionChanged += OnSelectionChanged;
        _diagram!.Changed += OnDiagramChanged;
        SubscribeEditEvents();
        UpdateSelectionState();

        AppState.ClearUndoRedo();
        AppState.MarkSaved();
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
        Snackbar.Add("Reverted to saved state", Severity.Info);
    }

    private async Task RedoAction()
    {
        if (_diagram == null || !AppState.CanRedo) return;
        var current = AppState.GetPageData();
        var next = AppState.PopRedo(current);
        if (next == null) return;
        await ApplyDiagramState(next);
        Snackbar.Add("Redo", Severity.Info);
    }

    private async Task ApplyDiagramState(DashboardPageModel state)
    {
        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
        var previousTopics = AppState.SubscribedTopics.ToHashSet();
        AppState.ResetDiagram();
        var newDiagram = AppState.CreateDiagramFromPageData(state, readOnly: !AppState.IsEditMode);
        _diagrams[_activePageIndex] = newDiagram;
        _pageStates[_activePageIndex] = state;
        await SyncSubscriptionsAsync(previousTopics, AppState.SubscribedTopics);
        if (AppState.IsEditMode)
        {
            _diagram!.SelectionChanged += OnSelectionChanged;
            _diagram!.Changed += OnDiagramChanged;
            UpdateSelectionState();
        }
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    private async Task SyncSubscriptionsAsync(HashSet<string> previous, IReadOnlyCollection<string> current)
    {
        if (AppState.DataServer == null) return;
        var currentSet = new HashSet<string>(current);
        foreach (var topic in previous.Where(t => !currentSet.Contains(t)))
            await AppState.DataServer.UnsubscribeAsync(topic);
        foreach (var topic in currentSet.Where(t => !previous.Contains(t)))
            await AppState.DataServer.SubscribeAsync(topic);
    }

    // ── Save As / Open ────────────────────────────────────────────────────────

    private async Task SaveAsDiagram()
    {
        var parameters = new DialogParameters<SimpleInputDialog>
        {
            { d => d.Title, "Save Dashboard As" },
            { d => d.Label, "Dashboard name" },
            { d => d.Value, AppState.DashboardName }
        };
        // Use a slightly larger, centered dialog for Open (was ExtraSmall/full-width which appeared narrow/left-aligned in some browsers)
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = false, CloseButton = true };
        var dialog = await DialogService.ShowAsync<SimpleInputDialog>("Save As", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: string name } && !string.IsNullOrWhiteSpace(name))
        {
            // Check for existing file and confirm overwrite
            var existing = await DashboardService.ListDashboardsAsync();
            if (existing.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            {
                var overwrite = await DialogService.ShowMessageBoxAsync(
                    "Overwrite?",
                    $"A dashboard named '{name}' already exists. Overwrite it?",
                    yesText: "Overwrite", cancelText: "Cancel");
                if (overwrite != true) return;
            }
            var state = BuildFullState();
            var success = await DashboardService.SaveDashboardByNameAsync(name, state);
            if (success)
            {
                AppState.SetDiagramName(name);
                AppState.MarkSaved();
                await SaveLastDiagramName(name);
                Snackbar.Add($"Saved as '{name}'", Severity.Success);
            }
            else
            {
                Snackbar.Add("Failed to save dashboard", Severity.Error);
            }
        }
    }

    private async Task OpenDiagram()
    {
        if (AppState.IsEdited)
        {
            bool confirmed = await ConfirmDiscardChanges("Open dashboard");
            if (!confirmed) return;
        }
        var names = await DashboardService.ListDashboardsAsync();
        if (names.Count == 0)
        {
            Snackbar.Add("No saved dashboards found", Severity.Warning);
            return;
        }
        var parameters = new DialogParameters<DashboardPickerDialog>
        {
            { d => d.DashboardNames, names }
        };
        // Use a slightly larger, centered dialog for Open so it appears centered across browsers
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = false, CloseButton = true };
        var dialog = await DialogService.ShowAsync<DashboardPickerDialog>("Open Dashboard", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: string name } && !string.IsNullOrWhiteSpace(name))
        {
            var state = await DashboardService.LoadDashboardByNameAsync(name);
            if (state != null)
            {
                AppState.ClearUndoRedo();
                var prevTopics = AppState.SubscribedTopics.ToHashSet();
                if (AppState.IsEditMode)
                {
                    _diagram?.SelectionChanged -= OnSelectionChanged;
                    _diagram?.Changed -= OnDiagramChanged;
                    UnsubscribeEditEvents();
                }
                LoadFullState(state, readOnly: !AppState.IsEditMode);
                await SyncSubscriptionsAsync(prevTopics, AppState.SubscribedTopics);
                if (AppState.IsEditMode && _diagram != null)
                {
                    _diagram.SelectionChanged += OnSelectionChanged;
                    _diagram.Changed += OnDiagramChanged;
                    SubscribeEditEvents();
                    UpdateSelectionState();
                }
                AppState.SetDiagramName(name);
                AppState.MarkSaved();
                await SaveLastDiagramName(name);
                var nodeCount = state.Pages.Sum(p => p.Nodes.Count);
                Snackbar.Add($"Opened '{name}' ({nodeCount} nodes)", Severity.Info);
                StateHasChanged();
                await Task.Delay(100);
                RefreshAll();
                StateHasChanged();
            }
            else
            {
                Snackbar.Add($"Failed to load '{name}'", Severity.Error);
            }
        }
    }

    private async Task<bool> SaveDashboard()
    {
        if (string.IsNullOrEmpty(AppState.DashboardName))
        {
            Snackbar.Add("No filename — use Save As to save this dashboard", Severity.Warning);
            return false;
        }
        try
        {
            var state = BuildFullState();
            var name = AppState.DashboardName;
            var success = await DashboardService.SaveDashboardByNameAsync(name, state);
            if (success)
            {
                AppState.MarkSaved();
                await SaveLastDiagramName(name);
                var nodeCount = state.Pages.Sum(p => p.Nodes.Count);
                var linkCount = state.Pages.Sum(p => p.Links.Count);
                Snackbar.Add($"Saved '{name}' ({nodeCount} nodes, {linkCount} links)", Severity.Success);
                return true;
            }
            else
            {
                Snackbar.Add($"Failed to save '{name}' — check server logs for details", Severity.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving: {ex.Message}", Severity.Error);
            return false;
        }
    }

    // ── Port operations ───────────────────────────────────────────────────────

    private void AddPortToSelectedNode(PortAlignment alignment)
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<NodeModel>().FirstOrDefault();
        if (node != null && !node.Ports.Any(p => p.Alignment == alignment))
        {
            AppState.AddPortToNode(node, alignment);
            node.Refresh();
            StateHasChanged();
        }
    }

    private void AddAllPortsToSelectedNode()
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<NodeModel>().FirstOrDefault();
        if (node == null) return;
        foreach (var alignment in new[] { PortAlignment.Top, PortAlignment.Right, PortAlignment.Bottom, PortAlignment.Left })
        {
            if (!node.Ports.Any(p => p.Alignment == alignment))
                AppState.AddPortToNode(node, alignment);
        }
        node.Refresh();
        StateHasChanged();
    }

    private void DeletePortFromSelectedNode(PortAlignment alignment)
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<NodeModel>().FirstOrDefault();
        var port = node?.Ports.FirstOrDefault(p => p.Alignment == alignment);
        if (port != null)
        {
            node!.RemovePort(port);
            node.Refresh();
            StateHasChanged();
        }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    private async Task EditNodeProperties()
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<TextNodeModel>().FirstOrDefault();
        if (node == null) { Snackbar.Add("No node selected", Severity.Warning); return; }
        var parameters = new DialogParameters { { "Node", node } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, BackdropClick = false };
        var dialog = await DialogService.ShowAsync<NodePropertyEditor>($"Edit {node.NodeType} Node Properties", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            StateHasChanged();
            Snackbar.Add("Node properties updated", Severity.Success);
        }
    }

    private string CanvasStyle =>
        string.IsNullOrEmpty(AppState.CanvasBackgroundColor)
            ? "position:relative;width: 100%; height: calc(100vh - 100px); overflow: hidden;"
            : $"position:relative;width: 100%; height: calc(100vh - 100px); overflow: hidden; background-color: {AppState.CanvasBackgroundColor};";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SubscribeToNodeChanges(NodeModel node)
    {
        if (_nodeChangedSubscriptions.Any(x => x.Node == node)) return;
        Action<Blazor.Diagrams.Core.Models.Base.Model> handler = _ => OnNodeChanged(node);
        node.Changed += handler;
        _nodeChangedSubscriptions.Add((node, handler));
    }

    private void OnNodeAddedInEditMode(Blazor.Diagrams.Core.Models.Base.Model model)
    {
        if (model is NodeModel node)
            SubscribeToNodeChanges(node);
    }

    private void OnNodeChanged(NodeModel node)
    {
        if (_suppressDirty) return;
        // Defer the dirty/undo mark: if the change was a selection event, diagram.SelectionChanged
        // fires synchronously right after node.Changed, and OnSelectionChanged clears _pendingDirtyMark
        // before the queued callback runs — so selection-only changes never mark dirty.
        _pendingDirtyMark = true;
        _ = InvokeAsync(() =>
        {
            if (!_pendingDirtyMark) return;
            _pendingDirtyMark = false;
            AppState.MarkEdited();
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastUndoPushByMove).TotalSeconds >= 1.5)
            {
                _lastUndoPushByMove = now;
                PushUndoSnapshot();
            }
            StateHasChanged();
        });
    }

    private async Task ShowDiagramPropertiesAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        await DialogService.ShowAsync<DashboardPropertiesDialog>("Dashboard Properties", options);
    }

    // Called from the "no topics" overlay on Display.razor
    private Task OpenDashboardProperties() => ShowDiagramPropertiesAsync();

    private async Task SaveLastDiagramName(string name)
    {
        try { await JSRuntime.InvokeVoidAsync("localStorage.setItem", LastDashboardKey, name); }
        catch { /* ignore */ }
    }

    private async Task<string?> GetLastDiagramName()
    {
        try { return await JSRuntime.InvokeAsync<string?>("localStorage.getItem", LastDashboardKey); }
        catch { return null; }
    }

    private async Task<bool> ConfirmDiscardChanges(string action)
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "Unsaved Changes",
            $"You have unsaved changes. Proceed with {action} and discard changes?",
            yesText: "Discard", cancelText: "Cancel");
        return result == true;
    }

    // ── Node alignment ────────────────────────────────────────────────────────

    private void AlignNodes(string alignment)
    {
        if (_diagram == null) return;
        var nodes = _diagram.GetSelectedModels().OfType<NodeModel>().ToList();
        if (nodes.Count < 2) { Snackbar.Add("Select 2+ nodes to align", Severity.Info); return; }
        PushUndoSnapshot();
        switch (alignment)
        {
            case "left":    var left    = nodes.Min(n => n.Position.X);                                       foreach (var n in nodes) n.SetPosition(left, n.Position.Y); break;
            case "right":   var right   = nodes.Max(n => n.Position.X + (n.Size?.Width ?? 100));              foreach (var n in nodes) n.SetPosition(right - (n.Size?.Width ?? 100), n.Position.Y); break;
            case "top":     var top     = nodes.Min(n => n.Position.Y);                                       foreach (var n in nodes) n.SetPosition(n.Position.X, top); break;
            case "bottom":  var bottom  = nodes.Max(n => n.Position.Y + (n.Size?.Height ?? 50));              foreach (var n in nodes) n.SetPosition(n.Position.X, bottom - (n.Size?.Height ?? 50)); break;
            case "centerH": var cx = nodes.Average(n => n.Position.X + (n.Size?.Width ?? 100) / 2.0);        foreach (var n in nodes) n.SetPosition(cx - (n.Size?.Width ?? 100) / 2.0, n.Position.Y); break;
            case "centerV": var cy = nodes.Average(n => n.Position.Y + (n.Size?.Height ?? 50) / 2.0);        foreach (var n in nodes) n.SetPosition(n.Position.X, cy - (n.Size?.Height ?? 50) / 2.0); break;
        }
        foreach (var n in nodes) n.Refresh();
        _diagram.Refresh();
        StateHasChanged();
    }

    private void SameWidth()
    {
        if (_diagram == null) return;
        var nodes = _diagram.GetSelectedModels().OfType<NodeModel>().ToList();
        if (nodes.Count < 2) { Snackbar.Add("Select 2+ nodes to resize", Severity.Info); return; }
        PushUndoSnapshot();
        var maxWidth = nodes.Max(n => n.Size?.Width ?? 100);
        foreach (var n in nodes)
            n.Size = new Blazor.Diagrams.Core.Geometry.Size(maxWidth, n.Size?.Height ?? 50);
        foreach (var n in nodes) n.Refresh();
        _diagram.Refresh();
        StateHasChanged();
    }

    private void SameHeight()
    {
        if (_diagram == null) return;
        var nodes = _diagram.GetSelectedModels().OfType<NodeModel>().ToList();
        if (nodes.Count < 2) { Snackbar.Add("Select 2+ nodes to resize", Severity.Info); return; }
        PushUndoSnapshot();
        var maxHeight = nodes.Max(n => n.Size?.Height ?? 50);
        foreach (var n in nodes)
            n.Size = new Blazor.Diagrams.Core.Geometry.Size(n.Size?.Width ?? 100, maxHeight);
        foreach (var n in nodes) n.Refresh();
        _diagram.Refresh();
        StateHasChanged();
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _locationChangingRegistration?.Dispose();
        AppState.OnToggleEditModeRequested -= OnToggleEditModeRequested;
        AppState.OnStateChanged -= OnAppStateChanged;

        // These are subscribed regardless of edit mode
        if (_onMenuOpen       != null) AppState.MenuOpen       -= _onMenuOpen;
        if (_onMenuSetActivePage != null) AppState.MenuSetActivePage -= _onMenuSetActivePage;

        if (AppState.IsEditMode)
        {
            Snackbar.Clear();
            AppState.SetEditMode(false);
            AppState.UpdateSelectionState(false, false);
            UnsubscribeEditEvents();
        }

        // Unsubscribe from ALL diagrams
        foreach (var d in _diagrams.OfType<BlazorDiagram>())
        {
            d.SelectionChanged -= OnSelectionChanged;
            d.Changed -= OnDiagramChanged;
        }

        GC.SuppressFinalize(this);
    }

    private record StartupSettingsDto(string Mode, string? Dashboard);
}
