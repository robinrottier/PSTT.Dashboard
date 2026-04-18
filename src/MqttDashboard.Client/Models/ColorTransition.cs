namespace MqttDashboard.Models;

public static class ColorTransitionHelper
{
    public static ColorTransitionData? Serialize(ColorTransition ct)
    {
        if (ct.ColorThresholds.Count == 0 && ct.ColorTopicIndex == 0 && ct.ElseColor == null) return null;
        return new ColorTransitionData
        {
            TopicIndex = ct.ColorTopicIndex != 0 ? ct.ColorTopicIndex : null,
            ElseColor = ct.ElseColor,
            Thresholds = ct.ColorThresholds.Count > 0
                ? ct.ColorThresholds.Select(t => new ColorThresholdData { Value = t.Value, Color = t.Color, Direction = t.Direction }).ToList()
                : null,
        };
    }

    public static ColorTransition Deserialize(ColorTransitionData? data)
    {
        if (data == null) return new ColorTransition();
        return new ColorTransition
        {
            ColorTopicIndex = data.TopicIndex ?? 0,
            ElseColor = data.ElseColor,
            ColorThresholds = data.Thresholds?
                .Select(t => new GaugeColorThreshold { Value = t.Value, Color = t.Color, Direction = t.Direction })
                .ToList() ?? new(),
        };
    }
}

/// <summary>
/// Groups the colour-transition settings that can be applied to any node that shows
/// a colour derived from a live data value: a list of threshold rules and the index
/// of the data topic whose value is tested against those rules.
/// </summary>
public class ColorTransition
{
    /// <summary>
    /// 0-based index into the node's DataTopics list whose value is used when
    /// evaluating all threshold rules. 0 = first topic (DataValue).
    /// </summary>
    public int ColorTopicIndex { get; set; } = 0;

    /// <summary>
    /// Ordered list of threshold rules. First matching rule wins.
    /// </summary>
    public List<GaugeColorThreshold> ColorThresholds { get; set; } = new();

    /// <summary>
    /// Fallback color used when no threshold rule matches. Null = use the node's built-in default.
    /// </summary>
    public string? ElseColor { get; set; }
}

/// <summary>A single colour-threshold rule: apply <see cref="Color"/> when the data value
/// satisfies the <see cref="Direction"/> comparison against <see cref="Value"/>.</summary>
public class GaugeColorThreshold
{
    public double Value { get; set; }
    public string Color { get; set; } = "var(--mud-palette-primary)";
    /// <summary>Comparison direction: ">=" or "&lt;=".</summary>
    public string Direction { get; set; } = ">=";
}
