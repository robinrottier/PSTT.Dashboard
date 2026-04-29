using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

/// <summary>
/// Renders the Text property as Markdown converted to HTML.
/// Uses Markdig for conversion. Content is static — no MQTT value substitution.
/// </summary>
public class MarkdownNodeModel : TextNodeModel
{
    public MarkdownNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Markdown";
    }

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new MarkdownNodeData();
        FillBaseData(data, panX, panY);
        return data;
    }

    public static MarkdownNodeModel FromData(MarkdownNodeData data)
    {
        var node = new MarkdownNodeModel(new Point(data.X, data.Y));
        return ApplyBaseData(node, data);
    }
}
