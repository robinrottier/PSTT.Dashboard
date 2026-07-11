using Blazor.Diagrams.Core.Models;
using Microsoft.AspNetCore.Components;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Widgets;

/// <summary>
/// Base class for all node widgets. Provides container styling, CSS classes,
/// port sizing, and double-click-to-edit behaviour.
/// </summary>
public abstract class BaseNodeWidget<TNode> : ComponentBase, IDisposable
    where TNode : TextNodeModel
{
    [Parameter] public TNode Node { get; set; } = null!;
    [Inject] protected ApplicationState AppState { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (Node != null)
        {
            if (Node.Size == null)
            {
                Node.Size = new Blazor.Diagrams.Core.Geometry.Size(120, 90);
            }

            var w = Math.Max(Node.Size.Width, Node.MinimumDimensions.Width);
            var h = Math.Max(Node.Size.Height, Node.MinimumDimensions.Height);
            if (w != Node.Size.Width || h != Node.Size.Height)
            {
                Node.Size = new Blazor.Diagrams.Core.Geometry.Size(w, h);
            }
        }
    }

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
        (Node.Selected        ? " selected" : "") +
        (Node.IsPrimarySelection ? " primary-node" : "");

    protected static string PortStyle(NodePortModel? port) => string.Empty;

    protected static string PortClass(NodePortModel? port) =>
        port?.PortStyle switch
        {
            "Invisible" => "port-invisible",
            "Fine"      => "port-fine",
            _           => string.Empty
        };

    protected void OnDoubleClick()
    {
        if (AppState.IsEditMode) AppState.TriggerEditProperties();
    }

    public virtual void Dispose() { }
}
