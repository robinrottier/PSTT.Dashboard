using Blazor.Diagrams.Core.Geometry;
using MqttDashboard.Components;

namespace MqttDashboard.Models;

public class BatteryNodeModel : TextNodeModel
{
    public BatteryNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Battery";
        BatteryColor = new ColorTransition
        {
            ColorThresholds =
            [
                new GaugeColorThreshold { Value = 25,  Direction = "<=", Color = "var(--mud-palette-error)" },
                new GaugeColorThreshold { Value = 50,  Direction = "<=", Color = "var(--mud-palette-warning)" },
                new GaugeColorThreshold { Value = 100, Direction = ">=", Color = "var(--mud-palette-success)" },
            ]
        };
    }

    [NpCustom("Range", typeof(NumericRangeEditor), Category = "Battery", Order = 1)]
    public NumericRangeSettings Range { get; set; } = new();

    [NpCheckbox("Show Percentage", Category = "Battery", Order = 2)]
    public bool ShowPercent { get; set; } = true;

    [NpCustom("Color Transitions", typeof(ColorTransitionGroupEditor), Category = "Battery", Order = 3)]
    public ColorTransition BatteryColor { get; set; } = new();

    // Convenience accessors.
    public double MinValue => Range.Min;
    public double MaxValue => Range.Max;
    public int DataTopicIndex => Range.DataTopicIndex;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new BatteryNodeData
        {
            ShowPercent = ShowPercent,
            Range = new NumericRangeData
            {
                Min = Range.Min,
                Max = Range.Max,
                Origin = Range.Origin,
                DataTopicIndex = Range.DataTopicIndex,
            },
            Color = ColorTransitionHelper.Serialize(BatteryColor),
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static BatteryNodeModel FromData(BatteryNodeData data)
    {
        var node = new BatteryNodeModel(new Point(data.X, data.Y))
        {
            Range = new NumericRangeSettings
            {
                Min = data.Range?.Min ?? 0,
                Max = data.Range?.Max ?? 100,
                Origin = data.Range?.Origin,
                DataTopicIndex = data.Range?.DataTopicIndex ?? 0,
            },
            ShowPercent = data.ShowPercent ?? true,
            BatteryColor = ColorTransitionHelper.Deserialize(data.Color),
        };
        return ApplyBaseData(node, data);
    }
}

