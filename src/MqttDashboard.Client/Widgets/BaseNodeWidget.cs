using Blazor.Diagrams.Core.Models;
using Microsoft.AspNetCore.Components;
using MqttDashboard.Models;
using MqttDashboard.Services;

namespace MqttDashboard.Widgets;

/// <summary>
/// Base class for all node widgets. Provides container styling, CSS classes,
/// port sizing, and double-click-to-edit behaviour.
/// </summary>
public abstract class BaseNodeWidget<TNode> : ComponentBase, IDisposable
    where TNode : TextNodeModel
{
    [Parameter] public TNode Node { get; set; } = null!;
    [Inject] protected ApplicationState AppState { get; set; } = null!;

    protected string ContainerStyle()
    {
        var size = Node.Size != null
            ? $"width:{Node.Size.Width}px;height:{Node.Size.Height}px;"
            : string.Empty;
        var bg = !string.IsNullOrEmpty(Node.BackgroundColor)
            ? $"background-color:{Node.BackgroundColor};"
            : string.Empty;
        return size + bg;
    }

    protected string NodeCssClass(string extra = "") =>
        $"pa-1 default-node{(string.IsNullOrEmpty(extra) ? "" : " " + extra)}" +
        (Node.Group  != null  ? " grouped"  : "") +
        (Node.Selected        ? " selected" : "");

    protected static string PortStyle(NodePortModel? port) =>
        port == null ? string.Empty
                     : $"width:{(int)port.Size.Width}px; height:{(int)port.Size.Height}px;";

    protected void OnDoubleClick()
    {
        if (AppState.IsEditMode) AppState.TriggerEditProperties();
    }

    public virtual void Dispose() { }
}
