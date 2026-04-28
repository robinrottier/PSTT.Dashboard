using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class SliderNodeModel : TextNodeModel
{
    public SliderNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Slider";
    }

    [NpNumeric("Min", Category = "Slider", Order = 1)]
    public double Min { get; set; } = 0;

    [NpNumeric("Max", Category = "Slider", Order = 2)]
    public double Max { get; set; } = 100;

    [NpNumeric("Step", Category = "Slider", Order = 3)]
    public double Step { get; set; } = 1;

    [NpText("Unit", Category = "Slider", Order = 4, Placeholder = "°C, %, W…")]
    public string? Unit { get; set; }

    [NpText("Publish Topic", Category = "Slider", Order = 5, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpCheckbox("Read Only (display only, no slider interaction)", Category = "Slider", Order = 6)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 7)]
    public bool Retain { get; set; } = false;

    [NpCheckbox("Publish to MQTT broker (uncheck = dashboard-local only)", Category = "Publish", Order = 8)]
    public bool PublishGlobally { get; set; } = true;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new SliderNodeData
        {
            Min = Min,
            Max = Max,
            Step = Step,
            Unit = Unit,
            PublishTopic = PublishTopic,
            IsReadOnly = IsReadOnly,
            Retain = Retain,
            PublishGlobally = PublishGlobally,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static SliderNodeModel FromData(SliderNodeData data)
    {
        var node = new SliderNodeModel(new Point(data.X, data.Y))
        {
            Min = data.Min,
            Max = data.Max,
            Step = data.Step,
            Unit = data.Unit,
            PublishTopic = data.PublishTopic,
            IsReadOnly = data.IsReadOnly,
            Retain = data.Retain,
            PublishGlobally = data.PublishGlobally,
        };
        return ApplyBaseData(node, data);
    }
}
