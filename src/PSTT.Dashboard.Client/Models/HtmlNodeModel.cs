using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

/// <summary>
/// Renders the Text property as raw HTML markup.
/// No MQTT value substitution is performed — content is purely static from the dashboard author.
/// </summary>
public class HtmlNodeModel : TextNodeModel
{
    public HtmlNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Html";
    }

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new HtmlNodeData();
        FillBaseData(data, panX, panY);
        return data;
    }

    public static HtmlNodeModel FromData(HtmlNodeData data)
    {
        var node = new HtmlNodeModel(new Point(data.X, data.Y));
        return ApplyBaseData(node, data);
    }
}
