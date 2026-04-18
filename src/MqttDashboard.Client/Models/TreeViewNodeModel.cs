using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class TreeViewNodeModel : TextNodeModel
{
    public TreeViewNodeModel(Point? position = null) : base(position) { NodeType = "TreeView"; }

    // Root topic is now stored in DataTopics[0] (inherited from TextNodeModel).
    // This was previously a separate RootTopic property; old files are migrated in FromData.

    [NpCheckbox("Show Values", Category = "Tree View", Order = 1)]
    public bool ShowValues { get; set; } = true;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new TreeViewNodeData
        {
            ShowValues = ShowValues ? null : false,   // default true; only store when false
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static TreeViewNodeModel FromData(TreeViewNodeData data)
    {
        var node = new TreeViewNodeModel(new Point(data.X, data.Y))
        {
            ShowValues = data.ShowValues ?? true,
        };
        ApplyBaseData(node, data);
        // Backward compat: migrate legacy RootTopic → DataTopics[0] for files saved before this change
        if (!string.IsNullOrEmpty(data.RootTopic) && node.DataTopics.Count == 0)
            node.DataTopics.Add(data.RootTopic);
        return node;
    }
}
