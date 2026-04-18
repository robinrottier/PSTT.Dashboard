using System.Text.Json.Serialization;

namespace MqttDashboard.Models;

// ── Root document ─────────────────────────────────────────────────────────────

public class DashboardModel
{
    public string Name { get; set; } = string.Empty;
    public bool ShowName { get; set; } = false;
    public HashSet<string>? MqttSubscriptions { get; set; }
    public List<DashboardPageModel> Pages { get; set; } = new();
    [JsonPropertyOrder(99)] public DashboardFileInfo? FileInfo { get; set; }
}

public class DashboardPageModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Page 1";
    public int GridSize { get; set; } = 20;
    public bool GridSnapToCenter { get; set; } = false;
    public string? BackgroundColor { get; set; }
    public List<NodeData> Nodes { get; set; } = new();
    public List<LinkData> Links { get; set; } = new();
}

public class DashboardFileInfo
{
    public string WrittenAt { get; set; } = string.Empty;
    public string? Filename { get; set; }
}

// ── Node data hierarchy (polymorphic) ─────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "nodeType")]
[JsonDerivedType(typeof(TextNodeData),     "Text")]
[JsonDerivedType(typeof(GaugeNodeData),    "Gauge")]
[JsonDerivedType(typeof(SwitchNodeData),   "Switch")]
[JsonDerivedType(typeof(BatteryNodeData),  "Battery")]
[JsonDerivedType(typeof(LogNodeData),      "Log")]
[JsonDerivedType(typeof(TreeViewNodeData), "TreeView")]
public abstract class NodeData
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }

    // Position and size rounded to 2 decimal places on write
    [JsonIgnore] private double _x;
    [JsonIgnore] private double _y;
    [JsonIgnore] private double _width;
    [JsonIgnore] private double _height;

    public double X { get => Math.Round(_x, 2); set => _x = value; }
    public double Y { get => Math.Round(_y, 2); set => _y = value; }
    public double Width { get => Math.Round(_width, 2); set => _width = value; }
    public double Height { get => Math.Round(_height, 2); set => _height = value; }

    public string? Icon { get; set; }
    public string? IconName { get; set; }
    public string? IconColor { get; set; }
    public string? Text { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? BackgroundObjectFit { get; set; }
    public string? TitlePosition { get; set; }
    public string? LinkAnimation { get; set; }
    public int? FontSize { get; set; }
    public List<string>? DataTopics { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public List<NodePortData>? Ports { get; set; }
}

public class TextNodeData : NodeData { }

public class GaugeNodeData : NodeData
{
    public string? Unit { get; set; }
    public string? TextPosition { get; set; }
    public NumericRangeData? Range { get; set; }
    public ColorTransitionData? Color { get; set; }
}

public class SwitchNodeData : NodeData
{
    public SwitchSettingsData? Switch { get; set; }
}

public class BatteryNodeData : NodeData
{
    public bool? ShowPercent { get; set; }
    public NumericRangeData? Range { get; set; }
    public ColorTransitionData? Color { get; set; }
}

public class LogNodeData : NodeData
{
    public int? MaxEntries { get; set; }
    public LogColumnsData? Columns { get; set; }
}

public class TreeViewNodeData : NodeData
{
    public string? RootTopic { get; set; }
    public bool? ShowValues { get; set; }
}

// ── Shared nested value types ─────────────────────────────────────────────────

public class NumericRangeData
{
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double? Origin { get; set; }
    public int DataTopicIndex { get; set; } = 0;
}

public class ColorTransitionData
{
    public int? TopicIndex { get; set; }
    public List<ColorThresholdData>? Thresholds { get; set; }
    public string? ElseColor { get; set; }
}

public class ColorThresholdData
{
    public double Value { get; set; }
    public string Direction { get; set; } = ">=";
    public string Color { get; set; } = "var(--mud-palette-primary)";
}

public class SwitchSettingsData
{
    public string? PublishTopic { get; set; }
    public string OnValue { get; set; } = "1";
    public string OffValue { get; set; } = "0";
    public string Style { get; set; } = "Full";
    public string OnText { get; set; } = "ON";
    public string OffText { get; set; } = "OFF";
    public bool ReadOnly { get; set; } = false;
    public bool Retain { get; set; } = false;
    public int Qos { get; set; } = 0;
}

public class LogColumnsData
{
    public bool? ShowDate { get; set; }
    public bool? ShowTime { get; set; }
    public bool? TopicFull { get; set; }
    public bool? TopicPath { get; set; }
    public bool? TopicName { get; set; }
    public bool? Value { get; set; }
}

public class NodePortData
{
    public string Id { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
}

public class LinkData
{
    public string Source { get; set; } = string.Empty;
    public string? SourcePort { get; set; }
    public string Target { get; set; } = string.Empty;
    public string? TargetPort { get; set; }
}
