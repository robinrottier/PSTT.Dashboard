namespace PSTT.Dashboard.Models;

/// <summary>
/// Returned by <see cref="PSTT.Dashboard.Components.ImportNodesDialog"/> when the user confirms an import.
/// </summary>
public sealed class ImportResult
{
    public List<NodeData> Nodes { get; init; } = [];
    public List<LinkData> Links { get; init; } = [];

    /// <summary>When true the caller should add the nodes on a new page; otherwise add to the current page.</summary>
    public bool AddAsNewPage { get; init; }

    /// <summary>
    /// When non-empty, the caller should add these pages to the dashboard (full-dashboard import).
    /// <see cref="Nodes"/> and <see cref="Links"/> will be empty in this case.
    /// </summary>
    public List<DashboardPageModel> AdditionalPages { get; init; } = [];
}
