using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PSTT.Dashboard.Helpers;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Components;

public partial class EditSidePanel : IAsyncDisposable
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public SidePanelTab ActiveTab { get; set; } = SidePanelTab.NodeProps;
    [Parameter] public EventCallback<SidePanelTab> ActiveTabChanged { get; set; }
    [Parameter] public TextNodeModel? SelectedNode { get; set; }
    [Parameter] public NodeLinkModel? SelectedLink { get; set; }
    [Parameter] public EventCallback OnNodeSaved { get; set; }
    [Parameter] public EventCallback OnNodeClose { get; set; }
    [Parameter] public EventCallback<string> OnAddNodeTypeSelected { get; set; }
    [Parameter] public ApplicationState AppState { get; set; } = default!;
    [Parameter] public bool HasSelectedNode { get; set; }
    [Parameter] public IReadOnlyCollection<string>? SelectedNodeTopics { get; set; }
    [Parameter] public EventCallback<string> OnTopicAssigned { get; set; }
    [Parameter] public EventCallback<string> OnCreateNodeWithTopic { get; set; }
    [Parameter] public string? CurrentPageName { get; set; }
    [Parameter] public string? CurrentPageBgColor { get; set; }
    [Parameter] public EventCallback<(string Name, string? BgColor)> OnPagePropsApplied { get; set; }
    [Parameter] public bool CanMovePageLeft { get; set; }
    [Parameter] public bool CanMovePageRight { get; set; }
    [Parameter] public EventCallback OnMovePageLeft { get; set; }
    [Parameter] public EventCallback OnMovePageRight { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private int _panelWidth = 300;
    private bool _subViewIsCustom = true;
    private DotNetObjectReference<EditSidePanel>? _dotNetRef;

    // Grid view Apply/Cancel state (Nodes)
    private NodePropertyEditor? _nodeEditor;
    private TextNodeModel? _lastGridNode;
    private string _propertySnapshot = "";
    private bool _gridHasPendingChanges;

    // Page model editing state
    private DashboardPageModel? _editPageModel;
    private string _pageSnapshot = "";
    private bool _pageHasPendingChanges;

    // Dashboard model editing state
    private DashboardModel? _editDashboardModel;
    private string _dashboardSnapshot = "";
    private bool _dashboardHasPendingChanges;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var saved = await JSRuntime.InvokeAsync<int?>("SidePanel.loadWidth");
            if (saved.HasValue && saved.Value >= 200 && saved.Value <= 800)
                _panelWidth = saved.Value;
        }
        catch { /* ignore JS errors during SSR */ }
    }

    private void InitPageModel()
    {
        _editPageModel = new DashboardPageModel
        {
            Name = CurrentPageName ?? "",
            BackgroundColor = CurrentPageBgColor
        };
        _pageSnapshot = System.Text.Json.JsonSerializer.Serialize(_editPageModel);
        _pageHasPendingChanges = false;
    }

    private void InitDashboardModel()
    {
        _editDashboardModel = new DashboardModel
        {
            Name = AppState.DashboardDisplayName,
            ShowName = AppState.ShowName,
            BackgroundColor = AppState.CanvasBackgroundColor,
            GridSize = Math.Max(5, AppState.GridSize),
            GridSnapToCenter = AppState.GridSnapToCenter,
            MqttSubscriptions = new HashSet<string>(AppState.SubscribedTopics)
        };
        _dashboardSnapshot = System.Text.Json.JsonSerializer.Serialize(_editDashboardModel);
        _dashboardHasPendingChanges = false;
    }

    protected override void OnParametersSet()
    {
        // Re-initialize page model if active page name/color params changed from outside
        if (_editPageModel == null || _editPageModel.Name != CurrentPageName || _editPageModel.BackgroundColor != CurrentPageBgColor)
        {
            InitPageModel();
        }

        // Re-initialize dashboard model if null
        if (_editDashboardModel == null)
        {
            InitDashboardModel();
        }

        // Capture a snapshot when the selected node changes (before any edits)
        if (!object.ReferenceEquals(SelectedNode, _lastGridNode))
        {
            _lastGridNode = SelectedNode;
            _gridHasPendingChanges = false;
            _propertySnapshot = SelectedNode != null ? NodeModelSnapshot.Capture(SelectedNode) : "";
        }
    }

    private async Task SetTab(SidePanelTab tab)
    {
        ActiveTab = tab;
        await ActiveTabChanged.InvokeAsync(tab);
    }

    [JSInvokable]
    public async Task SetWidth(int width)
    {
        _panelWidth = Math.Max(200, Math.Min(800, width));
        try { await JSRuntime.InvokeVoidAsync("SidePanel.saveWidth", _panelWidth); } catch { }
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnResizeMouseDown(MouseEventArgs e)
    {
        try
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("SidePanel.startResize", _dotNetRef, (int)e.ClientX, _panelWidth);
        }
        catch { }
    }

    private void OnGridPropertyChanged()
    {
        _gridHasPendingChanges = true;
        SelectedNode?.Refresh();
        AppState.MarkEdited();
    }

    private void GridApply()
    {
        AppState.PushUndoSnapshot(AppState.GetPageData());
        _propertySnapshot = SelectedNode != null ? NodeModelSnapshot.Capture(SelectedNode) : "";
        _gridHasPendingChanges = false;
    }

    private void GridCancel()
    {
        if (SelectedNode == null) return;
        NodeModelSnapshot.Restore(SelectedNode, _propertySnapshot);
        SelectedNode.Refresh();
        _gridHasPendingChanges = false;
        AppState.MarkEdited();
    }

    // Page Properties editing
    private void OnPageGridPropertyChanged()
    {
        _pageHasPendingChanges = true;
    }

    private void ApplyPageProps()
    {
        if (_editPageModel != null)
        {
            _ = OnPagePropsApplied.InvokeAsync((_editPageModel.Name, _editPageModel.BackgroundColor));
            _pageSnapshot = System.Text.Json.JsonSerializer.Serialize(_editPageModel);
            _pageHasPendingChanges = false;
        }
    }

    private void PageFormCancel()
    {
        PageGridCancel();
    }

    private void PageGridApply()
    {
        ApplyPageProps();
    }

    private void PageGridCancel()
    {
        if (!string.IsNullOrEmpty(_pageSnapshot))
        {
            _editPageModel = System.Text.Json.JsonSerializer.Deserialize<DashboardPageModel>(_pageSnapshot);
            _pageHasPendingChanges = false;
        }
    }

    // Dashboard Properties editing
    private void OnDashboardGridPropertyChanged()
    {
        _dashboardHasPendingChanges = true;
    }

    private void ApplyDashboardProps()
    {
        if (_editDashboardModel != null)
        {
            AppState.PushUndoSnapshot(AppState.GetPageData());
            AppState.SetDisplayName(_editDashboardModel.Name);
            AppState.SetShowDiagramName(_editDashboardModel.ShowName);
            if (_editDashboardModel.GridSize >= 5 && _editDashboardModel.GridSize <= 100)
            {
                AppState.SetGridSize(_editDashboardModel.GridSize);
            }
            AppState.SetGridSnapToCenter(_editDashboardModel.GridSnapToCenter);
            if (_editDashboardModel.MqttSubscriptions != null)
            {
                AppState.SetSubscribedTopics(_editDashboardModel.MqttSubscriptions.ToList());
            }
            AppState.MarkEdited();

            _dashboardSnapshot = System.Text.Json.JsonSerializer.Serialize(_editDashboardModel);
            _dashboardHasPendingChanges = false;
        }
    }

    private void DashboardFormCancel()
    {
        DashboardGridCancel();
    }

    private void DashboardGridApply()
    {
        ApplyDashboardProps();
    }

    private void DashboardGridCancel()
    {
        if (!string.IsNullOrEmpty(_dashboardSnapshot))
        {
            _editDashboardModel = System.Text.Json.JsonSerializer.Deserialize<DashboardModel>(_dashboardSnapshot);
            _dashboardHasPendingChanges = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
