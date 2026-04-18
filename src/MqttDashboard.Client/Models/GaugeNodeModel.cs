using Blazor.Diagrams.Core.Geometry;
using MqttDashboard.Components;

namespace MqttDashboard.Models;

public class GaugeNodeModel : TextNodeModel
{
    public GaugeNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Gauge";
        GaugeColor = new ColorTransition
        {
            ColorThresholds =
            [
                new GaugeColorThreshold { Value = 0, Direction = "<=", Color = "var(--mud-palette-error)" },
                new GaugeColorThreshold { Value = 0, Direction = ">=", Color = "var(--mud-palette-success)" },
            ]
        };
    }

    [NpCustom("Range", typeof(NumericRangeEditor), Category = "Gauge", Order = 1)]
    public NumericRangeSettings Range { get; set; } = new();

    [NpText("Unit", Category = "Gauge", Order = 2, Placeholder = "°C, W…")]
    public string? Unit { get; set; }

    [NpSelect("Text Position", "Below", "Above", Category = "Gauge", Order = 3, Labels = ["Below", "Above"])]
    public string TextPosition { get; set; } = "Below";

    [NpCustom("Color Transitions", typeof(ColorTransitionGroupEditor), Category = "Gauge", Order = 4)]
    public ColorTransition GaugeColor { get; set; } = new();

    // Convenience accessors used by the widget rendering code.
    public double MinValue => Range.Min;
    public double MaxValue => Range.Max;
    public double? Origin => Range.Origin;
    public int DataTopicIndex => Range.DataTopicIndex;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new GaugeNodeData
        {
            Unit = Unit,
            TextPosition = TextPosition != "Below" ? TextPosition : null,
            Range = new NumericRangeData
            {
                Min = Range.Min,
                Max = Range.Max,
                Origin = Range.Origin,
                DataTopicIndex = Range.DataTopicIndex,
            },
            Color = ColorTransitionHelper.Serialize(GaugeColor),
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static GaugeNodeModel FromData(GaugeNodeData data)
    {
        var node = new GaugeNodeModel(new Point(data.X, data.Y))
        {
            Range = new NumericRangeSettings
            {
                Min = data.Range?.Min ?? 0,
                Max = data.Range?.Max ?? 100,
                Origin = data.Range?.Origin,
                DataTopicIndex = data.Range?.DataTopicIndex ?? 0,
            },
            Unit = data.Unit,
            TextPosition = data.TextPosition ?? "Below",
            GaugeColor = ColorTransitionHelper.Deserialize(data.Color),
        };
        return ApplyBaseData(node, data);
    }
}
