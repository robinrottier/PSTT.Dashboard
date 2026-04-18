namespace MqttDashboard.Models;

/// <summary>
/// Returned by <see cref="MqttDashboard.Components.ImportNodesDialog"/> when the user confirms an import.
/// </summary>
public sealed class ImportResult
{
    public List<NodeData> Nodes { get; init; } = [];
    public List<LinkData> Links { get; init; } = [];

    /// <summary>When true the caller should add the nodes on a new page; otherwise add to the current page.</summary>
    public bool AddAsNewPage { get; init; }
}
