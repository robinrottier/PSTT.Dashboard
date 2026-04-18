namespace MqttDashboard.Models;

/// <summary>
/// Shared settings for numeric range visualization: min, max, an optional midpoint/origin,
/// and which data topic drives the display. Used by Gauge, Battery, and any future visual
/// node that maps a live value onto a range.
/// </summary>
public class NumericRangeSettings
{
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;

    /// <summary>
    /// Origin/midpoint of the visual scale (e.g. gauge zero-point).
    /// When null the display starts from <see cref="Min"/>.
    /// </summary>
    public double? Origin { get; set; }

    /// <summary>0-based index of the data topic whose value drives the display.</summary>
    public int DataTopicIndex { get; set; } = 0;
}
