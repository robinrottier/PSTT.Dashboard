using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;

namespace PSTT.Dashboard.Models;

/// <summary>
/// PSTT dashboard link. Extends <see cref="FlowLinkModel"/> with a data topic that
/// drives the flow direction at runtime: positive value → Forward, negative → Reverse,
/// zero → Paused, non-numeric/missing → None.
/// </summary>
public class NodeLinkModel : FlowLinkModel
{
    public NodeLinkModel(Anchor source, Anchor target) : base(source, target) { }

    /// <summary>
    /// MQTT topic whose numeric value drives <see cref="FlowLinkModel.FlowDirection"/>.
    /// Null or empty means no data-driven animation.
    /// </summary>
    public string? DataTopic { get; set; }

    /// <summary>
    /// Persistent animation style: "None" (static link) or "Flow" (marching-ants, default).
    /// Flow only activates when <see cref="DataTopic"/> is set.
    /// </summary>
    public string Animation { get; set; } = "Flow";
}
