using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class IFrameNodeModel : TextNodeModel
{
    public IFrameNodeModel(Point? position = null) : base(position)
    {
        NodeType = "IFrame";
    }

    [NpText("URL", Category = "IFrame", Order = 1, Placeholder = "https://example.com")]
    public string? SourceUrl { get; set; }

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new IFrameNodeData
        {
            SourceUrl = SourceUrl,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static IFrameNodeModel FromData(IFrameNodeData data)
    {
        var node = new IFrameNodeModel(new Point(data.X, data.Y))
        {
            SourceUrl = data.SourceUrl,
        };
        return ApplyBaseData(node, data);
    }
}
