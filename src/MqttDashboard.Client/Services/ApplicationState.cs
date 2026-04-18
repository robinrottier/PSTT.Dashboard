using Blazor.Diagrams;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Controls.Default;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Positions.Resizing;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using MqttDashboard.Data;
using MqttDashboard.Models;
using MqttDashboard.Widgets;
using System.Collections.Concurrent;
using System.Reflection;

namespace MqttDashboard.Services;

public enum ThemeMode { Light, Dark, Auto }

public class ApplicationState
{
    private readonly int _maxMessageHistory;

    public ApplicationState(
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null,
        IDataCache? dataCache = null)
    {
        var raw = configuration?["App:MaxMessageHistory"];
        _maxMessageHistory = int.TryParse(raw, out var v) && v > 0 ? v : 500;
        DataCache = dataCache ?? new DataCache();
    }

    public string DisplayName => GetType().Assembly
        .GetCustomAttribute<System.Reflection.AssemblyProductAttribute>()?.Product
        ?? "Mqtt Dashboard";
    public int Counter { get; set; } = 0;
    public bool IsInteractive { get; private set; } = false;

    private BlazorDiagram? _diagram;

    // Multi-page support
    public List<string> PageNames { get; private set; } = ["Page 1"];
    public int ActivePageIndex { get; private set; } = 0;

    public void SetPageNames(List<string> names, int activeIndex = 0)
    {
        PageNames = new List<string>(names);
        ActivePageIndex = Math.Clamp(activeIndex, 0, Math.Max(0, PageNames.Count - 1));
        NotifyStateChangedAsync();
    }

    public void SetActivePage(int index)
    {
        ActivePageIndex = Math.Clamp(index, 0, Math.Max(0, PageNames.Count - 1));
        NotifyStateChangedAsync();
    }

    public void SetActiveDiagram(BlazorDiagram? diagram)
    {
        _diagram = diagram;
    }

    // MQTT State
    public IDataServer? DataServer { get; private set; }
    public List<MqttDataMessage> Messages { get; private set; } = new();
    public HashSet<string> SubscribedTopics { get; private set; } = new();
    public bool IsMqttConnected { get; set; } = false;
    public string MqttConnectionStatus { get; set; } = "Disconnected";

    // MQTT Data Cache
    public IDataCache DataCache { get; }

    // Theme & UI preferences
    public ThemeMode ThemeMode { get; private set; } = ThemeMode.Auto;
    public bool AutoSaveOnExitEditMode { get; private set; } = false;
    public bool ShowName { get; private set; } = true;

    /// <summary>File name (stem) used for saving/loading. Set by the caller, not from file contents.</summary>
    public string DashboardName { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable display name stored inside the dashboard JSON (DashboardModel.Name).
    /// Shown in the title bar and editable in Dashboard Properties.
    /// May differ from the file name — e.g. after "Save As" the file name changes but the
    /// display name set in Properties stays the same.
    /// </summary>
    public string DashboardDisplayName { get; private set; } = string.Empty;

    /// <summary>The name to show in the UI: DiagramDisplayName when set, otherwise DiagramName.</summary>
    public string ActiveDashboardLabel =>
        !string.IsNullOrEmpty(DashboardDisplayName) ? DashboardDisplayName :
        !string.IsNullOrEmpty(DashboardName) ? DashboardName :
        "Untitled";

    public int GridSize { get; private set; } = 20;
    public bool GridSnapToCenter { get; private set; } = false;
    public string CanvasBackgroundColor { get; private set; } = string.Empty;

    // Edit mode state (set by Edit page)
    public bool IsEditMode { get; private set; } = false;
    public bool HasSelectedNode { get; private set; } = false;
    public bool HasSingleSelectedNode { get; private set; } = false;
    public bool IsMultiSelected => HasSelectedNode && !HasSingleSelectedNode;
    /// <summary>The set of port alignments present on the currently selected node. Null when no single node is selected.</summary>
    public HashSet<PortAlignment>? SelectedNodePorts { get; private set; }

    // Auth state
    public bool IsAdmin { get; private set; } = true; // default true when auth not configured
    public bool AuthEnabled { get; private set; } = false;
    public bool IsReadOnly { get; private set; } = false;

    public void SetAuthState(bool isAdmin, bool authEnabled, bool readOnly = false)
    {
        IsAdmin = isAdmin;
        AuthEnabled = authEnabled;
        IsReadOnly = readOnly;
        NotifyStateChangedAsync();
    }

    // Update state
    public string? UpdateAvailableVersion { get; private set; }
    public string? CurrentVersion { get; private set; }
    public string? DeploymentType { get; private set; }
    public string? UpdateReleaseUrl { get; private set; }
    public DateTimeOffset? UpdateLastChecked { get; private set; }

    public void SetUpdateState(string currentVersion, string? latestVersion, bool updateAvailable,
        string deploymentType, DateTimeOffset? lastChecked, string? releaseUrl)
    {
        CurrentVersion = currentVersion;
        UpdateAvailableVersion = updateAvailable ? latestVersion : null;
        DeploymentType = deploymentType;
        UpdateLastChecked = lastChecked;
        UpdateReleaseUrl = releaseUrl;
        NotifyStateChangedAsync();
    }

    // Edited flag (was IsDirty)
    public bool IsEdited { get; private set; } = false;
    public void MarkEdited() { IsEdited = true; NotifyStateChangedAsync(); }
    public void MarkSaved() { IsEdited = false; NotifyStateChangedAsync(); }

    // Clipboard
    private List<NodeData> _clipboard = new();
    public bool HasClipboard => _clipboard.Count > 0;
    public IReadOnlyList<NodeData> Clipboard => _clipboard.AsReadOnly();
    public void SetClipboard(IEnumerable<NodeData> nodes) { _clipboard = nodes.ToList(); NotifyStateChangedAsync(); }

    // Undo/Redo stacks
    private readonly Stack<DashboardPageModel> _undoStack = new();
    private readonly Stack<DashboardPageModel> _redoStack = new();
    private int _maxUndoDepth = 20;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void PushUndoSnapshot(DashboardPageModel snapshot)
    {
        _undoStack.Push(snapshot);
        while (_undoStack.Count > _maxUndoDepth)
        {
            var items = _undoStack.ToList();
            _undoStack.Clear();
            foreach (var item in items.Take(items.Count - 1).AsEnumerable().Reverse())
                _undoStack.Push(item);
        }
        _redoStack.Clear();
        NotifyStateChangedAsync();
    }

    public DashboardPageModel? PopUndo(DashboardPageModel currentState)
    {
        if (!CanUndo) return null;
        _redoStack.Push(currentState);
        var state = _undoStack.Pop();
        NotifyStateChangedAsync();
        return state;
    }

    public DashboardPageModel? PopRedo(DashboardPageModel currentState)
    {
        if (!CanRedo) return null;
        _undoStack.Push(currentState);
        var state = _redoStack.Pop();
        NotifyStateChangedAsync();
        return state;
    }

    public void ClearUndoRedo() { _undoStack.Clear(); _redoStack.Clear(); NotifyStateChangedAsync(); }

    // Edit mode toggle event — fired by MainLayout, handled by Display page
    public event Action? OnToggleEditModeRequested;
    public void RequestToggleEditMode() => OnToggleEditModeRequested?.Invoke();

    // Menu action events — the Display page subscribes to these when in edit mode
    public event Action? MenuAddNode;
    public event Action? MenuDeleteNode;
    public event Action? MenuCutSelected;
    public event Action? MenuCopySelected;
    public event Action? MenuPasteSelected;
    public event Action? MenuExportNodes;
    public event Action? MenuImportNodes;
    public event Action? MenuAddAllPorts;
    public event Action<PortAlignment>? MenuAddPort;
    public event Action<PortAlignment>? MenuDeletePort;
    public event Action? MenuEditProperties;
    public event Action? MenuSaveDiagram;
    public event Action? MenuNewDiagram;
    public event Action? MenuReloadDiagram;
    public event Action? MenuUndo;
    public event Action? MenuRedo;
    public event Action? MenuUndoAll;
    public event Action? MenuSaveAs;
    public event Action? MenuOpen;
    public event Action? MenuDiagramProperties;

    public event Action? OnStateChanged;

    // Page management events
    public event Action? MenuAddPage;
    public event Action<int>? MenuRemovePage;
    public event Action<int, string>? MenuRenamePage;
    public event Action<int>? MenuSetActivePage;

    public void TriggerAddPage() => MenuAddPage?.Invoke();
    public void TriggerRemovePage(int index) => MenuRemovePage?.Invoke(index);
    public void TriggerRenamePage(int index, string name) => MenuRenamePage?.Invoke(index, name);
    public void TriggerSetActivePage(int index) { ActivePageIndex = index; MenuSetActivePage?.Invoke(index); NotifyStateChangedAsync(); }

    public void SetInteractive() => IsInteractive = true;

    public void SetEditMode(bool editMode)
    {
        IsEditMode = editMode;
        NotifyStateChangedAsync();
    }

    public void UpdateSelectionState(bool hasSelected, bool hasSingleSelected, HashSet<PortAlignment>? selectedPorts = null)
    {
        HasSelectedNode = hasSelected;
        HasSingleSelectedNode = hasSingleSelected;
        SelectedNodePorts = selectedPorts;
        NotifyStateChangedAsync();
    }

    public void SetTheme(ThemeMode mode)
    {
        ThemeMode = mode;
        NotifyStateChangedAsync();
    }

    public void SetAutoSaveOnExitEditMode(bool value)
    {
        AutoSaveOnExitEditMode = value;
        NotifyStateChangedAsync();
    }

    public void ToggleShowDiagramName()
    {
        ShowName = !ShowName;
        NotifyStateChangedAsync();
    }

    public void SetShowDiagramName(bool show)
    {
        ShowName = show;
        NotifyStateChangedAsync();
    }

    public void SetDiagramName(string name)
    {
        DashboardName = name;
        NotifyStateChangedAsync();
    }

    public void SetDisplayName(string name)
    {
        DashboardDisplayName = name;
        NotifyStateChangedAsync();
    }

    public void SetGridSize(int size)
    {
        // In edit mode enforce minimum of 5, maximum of 100, rounded to nearest 5
        if (IsEditMode)
            size = Math.Clamp((int)Math.Round(size / 5.0) * 5, 5, 100);
        GridSize = size;
        if (_diagram != null)
        {
            _diagram.Options.GridSize = size == 0 ? null : size;
            _diagram.Options.GridSnapToCenter = GridSnapToCenter;
            _diagram.Refresh();
        }
        NotifyStateChangedAsync();
    }

    public void SetGridSnapToCenter(bool snapToCenter)
    {
        GridSnapToCenter = snapToCenter;
        if (_diagram != null)
        {
            _diagram.Options.GridSnapToCenter = snapToCenter;
            _diagram.Refresh();
        }
        NotifyStateChangedAsync();
    }

    public void SetCanvasBackground(string color)
    {
        CanvasBackgroundColor = color ?? string.Empty;
        NotifyStateChangedAsync();
    }

    // Menu trigger methods — called by AppMenu
    public void TriggerAddNode() => MenuAddNode?.Invoke();
    public void TriggerDeleteNode() => MenuDeleteNode?.Invoke();
    public void TriggerCutSelected() => MenuCutSelected?.Invoke();
    public void TriggerCopySelected() => MenuCopySelected?.Invoke();
    public void TriggerPasteSelected() => MenuPasteSelected?.Invoke();
    public void TriggerExportNodes() => MenuExportNodes?.Invoke();
    public void TriggerImportNodes() => MenuImportNodes?.Invoke();
    public void TriggerAddAllPorts() => MenuAddAllPorts?.Invoke();
    public void TriggerAddPort(PortAlignment alignment) => MenuAddPort?.Invoke(alignment);
    public void TriggerDeletePort(PortAlignment alignment) => MenuDeletePort?.Invoke(alignment);
    public void TriggerEditProperties() => MenuEditProperties?.Invoke();
    public void TriggerSaveDiagram() => MenuSaveDiagram?.Invoke();
    public void TriggerNewDiagram() => MenuNewDiagram?.Invoke();
    public void TriggerReloadDiagram() => MenuReloadDiagram?.Invoke();
    public void TriggerUndo() => MenuUndo?.Invoke();
    public void TriggerRedo() => MenuRedo?.Invoke();
    public void TriggerUndoAll() => MenuUndoAll?.Invoke();
    public void TriggerSaveAs() => MenuSaveAs?.Invoke();
    public void TriggerOpen() => MenuOpen?.Invoke();
    public void TriggerDiagramProperties() => MenuDiagramProperties?.Invoke();

    public BlazorDiagram GetOrCreateDashboard()
    {
        if (_diagram == null)
            _diagram = CreateDiagramFromPageData(null, false);
        return _diagram;
    }

    /// <summary>
    /// Apply top-level dashboard properties (name, subscriptions, etc.) from a loaded DashboardModel.
    /// Call this after loading a new dashboard before building diagrams.
    /// </summary>
    public void ApplyDashboardModel(DashboardModel model)
    {
        DashboardDisplayName = model.Name;
        ShowName = model.ShowName;
        if (model.MqttSubscriptions != null)
            SubscribedTopics = new HashSet<string>(model.MqttSubscriptions);
        NotifyStateChangedAsync();
    }

    public BlazorDiagram CreateDiagramFromPageData(DashboardPageModel? page, bool readOnly)
    {
        var options = new BlazorDiagramOptions
        {
            AllowMultiSelection = !readOnly,
            Zoom = { Enabled = false, },
            Links =
            {
                DefaultRouter = new NormalRouter(),
                DefaultPathGenerator = new SmoothPathGenerator()
            },
            AllowPanning = false,
        };
        if (!readOnly)
        {
            if (page == null)
            {
                options.GridSize = 20;
                GridSize = 20;
                GridSnapToCenter = false;
            }
            else
            {
                options.GridSize = Math.Clamp(page.GridSize == 0 ? 20 : Math.Abs(page.GridSize), 5, 100);
                GridSize = (int)options.GridSize;
                GridSnapToCenter = page.GridSnapToCenter;
            }
            options.GridSnapToCenter = GridSnapToCenter;
        }

        // Apply canvas background from page
        if (page != null)
            CanvasBackgroundColor = page.BackgroundColor ?? string.Empty;

        var diagram = new BlazorDiagram(options);
        diagram.RegisterComponent<TextNodeModel, MudNodeWidget>();
        diagram.RegisterComponent<GaugeNodeModel, GaugeNodeWidget>();
        diagram.RegisterComponent<SwitchNodeModel, SwitchNodeWidget>();
        diagram.RegisterComponent<BatteryNodeModel, BatteryNodeWidget>();
        diagram.RegisterComponent<LogNodeModel, LogNodeWidget>();
        diagram.RegisterComponent<TreeViewNodeModel, TreeViewNodeWidget>();

        if (page != null)
        {
            var nodeMap = new Dictionary<string, NodeModel>();
            foreach (var nodeData in page.Nodes)
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

                node.Locked = readOnly;
                diagram.Nodes.Add(node);
                nodeMap[nodeData.Id] = node;

                foreach (var portData in nodeData.Ports ?? [])
                {
                    var alignment = Enum.Parse<PortAlignment>(portData.Alignment);
                    AddPortToNode(node, alignment);
                }

                if (!readOnly)
                    diagram.Controls.AddFor(node).Add(new ResizeControl(new BottomRightResizerProvider()));
            }

            foreach (var linkData in page.Links)
            {
                if (nodeMap.TryGetValue(linkData.Source, out var sourceNode) &&
                    nodeMap.TryGetValue(linkData.Target, out var targetNode))
                {
                    PortModel? sourcePort = null;
                    PortModel? targetPort = null;

                    if (!string.IsNullOrEmpty(linkData.SourcePort))
                    {
                        var alignment = Enum.Parse<PortAlignment>(linkData.SourcePort);
                        sourcePort = sourceNode.Ports.FirstOrDefault(p => p.Alignment == alignment);
                    }
                    if (!string.IsNullOrEmpty(linkData.TargetPort))
                    {
                        var alignment = Enum.Parse<PortAlignment>(linkData.TargetPort);
                        targetPort = targetNode.Ports.FirstOrDefault(p => p.Alignment == alignment);
                    }

                    Anchor sourceAnchor = sourcePort != null
                        ? new SinglePortAnchor(sourcePort)
                        : new ShapeIntersectionAnchor(sourceNode);
                    Anchor targetAnchor = targetPort != null
                        ? new SinglePortAnchor(targetPort)
                        : new ShapeIntersectionAnchor(targetNode);

                    var link = diagram.Links.Add(new LinkModel(sourceAnchor, targetAnchor));
                    link.Locked = readOnly;
                    CheckForLinkAnimation(sourceNode, link);
                }
            }
        }

        _diagram = diagram;
        return _diagram;
    }

    public DashboardPageModel GetPageData()
    {
        if (_diagram == null)
            return new DashboardPageModel();

        var gridSize = (int)(_diagram.Options.GridSize ?? GridSize);

        var panX = _diagram.Pan.X;
        var panY = _diagram.Pan.Y;

        var page = new DashboardPageModel
        {
            GridSize = gridSize,
            GridSnapToCenter = _diagram.Options.GridSnapToCenter,
            BackgroundColor = string.IsNullOrEmpty(CanvasBackgroundColor) ? null : CanvasBackgroundColor,
        };

        foreach (var node in _diagram.Nodes.OfType<TextNodeModel>())
        {
            page.Nodes.Add(node.ToData(panX, panY));
        }

        foreach (var link in _diagram.Links)
        {
            var linkData = new LinkData();

            if (link.Source?.Model is PortModel sourcePort)
            {
                linkData.Source = sourcePort.Parent.Id;
                linkData.SourcePort = sourcePort.Alignment.ToString();
            }
            else if (link.Source?.Model is NodeModel sourceNode)
            {
                linkData.Source = sourceNode.Id;
            }

            if (link.Target?.Model is PortModel targetPort)
            {
                linkData.Target = targetPort.Parent.Id;
                linkData.TargetPort = targetPort.Alignment.ToString();
            }
            else if (link.Target?.Model is NodeModel targetNode)
            {
                linkData.Target = targetNode.Id;
            }

            if (!string.IsNullOrEmpty(linkData.Source) && !string.IsNullOrEmpty(linkData.Target))
                page.Links.Add(linkData);
        }

        if (panX != 0 || panY != 0)
            _diagram.SetPan(0, 0);

        return page;
    }

    public void ResetDiagram()
    {
        _diagram = null;
    }

    // MQTT Methods
    public void SetDataServer(IDataServer server)
    {
        DataServer = server;
    }

    public void SetSubscribedTopics(IEnumerable<string> topics)
    {
        SubscribedTopics = new HashSet<string>(topics);
        NotifyStateChangedAsync();
    }

    public async Task AddSubscriptionAsync(string topic)
    {
        SubscribedTopics.Add(topic);
        if (IsEditMode) MarkEdited();
        NotifyStateChangedAsync();
        await Task.CompletedTask;
    }

    public async Task RemoveSubscriptionAsync(string topic)
    {
        SubscribedTopics.Remove(topic);
        if (IsEditMode) MarkEdited();
        NotifyStateChangedAsync();
        await Task.CompletedTask;
    }

    public void AddMessage(MqttDataMessage message)
    {
        lock (Messages)
        {
            Messages.Add(message);
            while (Messages.Count > _maxMessageHistory)
            {
                Messages.RemoveAt(0);
            }
        }

        // Update the data cache
        DataCache.UpdateValue(message.Topic, message.Payload);

        NotifyStateChangedAsync();
    }

    public List<MqttDataMessage> RecentMessages(int n)
    {
        lock (Messages)
        {
            return Messages.TakeLast(n).ToList();
        }
    }
    public void SetMqttConnectionStatus(string status, bool connected)
    {
        MqttConnectionStatus = status;
        IsMqttConnected = connected;
        NotifyStateChangedAsync();
    }

    public void ClearMessages()
    {
        Messages.Clear();
        NotifyStateChangedAsync();
    }

    private void NotifyStateChangedAsync()
    {
        // Invoke asynchronously to avoid thread issues
        _ = Task.Run(() => OnStateChanged?.Invoke());
    }

    internal async Task AddTopicToDashboard(string topicPath, string nodeName)
    {
        var diagram = GetOrCreateDashboard();

        var node = new TextNodeModel(
            new Blazor.Diagrams.Core.Geometry.Point(100 + diagram.Nodes.Count * 20, 100))
        {
            Title = nodeName,
            DataTopics = new List<string> { topicPath },
        };
        // try be clever with formatting...
        string? format = null;
        switch (nodeName.ToLower())
        {
            case "soc":     format = "{0:0}%"; break;
            case "power":
            case "solar":
            case "pv":
            case "load":
            case "grid":    format = "{0:0}W"; break;
        }
        if (format != null)
        {
            node.Text = format;
        }

        diagram.Nodes.Add(node);
        diagram.Controls.AddFor(node).Add(new ResizeControl(new BottomRightResizerProvider()));
    }

    public void CheckForLinkAnimation(NodeModel sourceNode, LinkModel link)
    {
        if (sourceNode is TextNodeModel textSource
         && !string.IsNullOrWhiteSpace(textSource.LinkAnimation)
         && textSource.LinkAnimation != "None")
        {
            link.DashPattern = "5,5";
            link.AddAnimation(new AnimateModel()
            {
                AttributeName = "stroke-dashoffset",
                From = "0",
                To = "0",
                Duration = "1s"
            });
        }
    }

    internal void AddPortToNode(NodeModel node, PortAlignment alignment)
    {
        if (node != null)
        {
            node.AddPort(new NodePortModel(node, alignment));
        }
    }
}
