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
    /// Persistent animation style: "None" (static, not animated), "Flow" (data-driven, default),
    /// "FlowReverse" (data-driven, reversed polarity), "Forward" or "Reverse" (fixed direction).
    /// </summary>
    public string Animation { get; set; } = "Flow";

    /// <summary>
    /// Dead-band threshold for data-driven modes. When |value| ≤ FlowThreshold the overlay is
    /// shown as a static (non-animated) line. Null or 0 means only exactly zero pauses.
    /// </summary>
    public double? FlowThreshold { get; set; }
}
