using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
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

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private int _panelWidth = 300;
    private bool _subViewIsCustom;
    private string? _editPageName;
    private string? _editPageBg;
    private DotNetObjectReference<EditSidePanel>? _dotNetRef;

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

    protected override void OnParametersSet()
    {
        _editPageName = CurrentPageName;
        _editPageBg = CurrentPageBgColor;
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
        SelectedNode?.Refresh();
        AppState.MarkEdited();
    }

    private void ApplyPageProps()
    {
        _ = OnPagePropsApplied.InvokeAsync((_editPageName ?? "", _editPageBg));
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
