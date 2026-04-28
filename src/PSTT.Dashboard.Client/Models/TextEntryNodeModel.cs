using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class TextEntryNodeModel : TextNodeModel
{
    public TextEntryNodeModel(Point? position = null) : base(position)
    {
        NodeType = "TextEntry";
    }

    [NpText("Placeholder", Category = "Text Entry", Order = 1, Placeholder = "Enter value…")]
    public string? Placeholder { get; set; }

    [NpText("Publish Topic", Category = "Text Entry", Order = 2, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpCheckbox("Read Only (display only, no input)", Category = "Text Entry", Order = 3)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 4)]
    public bool Retain { get; set; } = false;

    [NpCheckbox("Publish to MQTT broker (uncheck = dashboard-local only)", Category = "Publish", Order = 5)]
    public bool PublishGlobally { get; set; } = true;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new TextEntryNodeData
        {
            Placeholder = Placeholder,
            PublishTopic = PublishTopic,
            IsReadOnly = IsReadOnly,
            Retain = Retain,
            PublishGlobally = PublishGlobally,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static TextEntryNodeModel FromData(TextEntryNodeData data)
    {
        var node = new TextEntryNodeModel(new Point(data.X, data.Y))
        {
            Placeholder = data.Placeholder,
            PublishTopic = data.PublishTopic,
            IsReadOnly = data.IsReadOnly,
            Retain = data.Retain,
            PublishGlobally = data.PublishGlobally,
        };
        return ApplyBaseData(node, data);
    }
}
