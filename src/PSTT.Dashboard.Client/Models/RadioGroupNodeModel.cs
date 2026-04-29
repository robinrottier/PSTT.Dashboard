using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class RadioGroupNodeModel : TextNodeModel
{
    public RadioGroupNodeModel(Point? position = null) : base(position)
    {
        NodeType = "RadioGroup";
    }

    /// <summary>
    /// Newline-separated list of radio options. Each line: "Label=Value".
    /// Lines starting with # are treated as comments and ignored.
    /// </summary>
    [NpText("Options (one per line: Label=Value)", Category = "Radio Group", Order = 1,
        Placeholder = "Manual=0\nAuto=1\nOff=2",
        Lines = 5)]
    public string? Items { get; set; }

    [NpSelect("Layout", "Horizontal", "Vertical",
        Category = "Radio Group", Order = 2,
        Labels = ["Horizontal", "Vertical"])]
    public string Orientation { get; set; } = "Horizontal";

    [NpSelect("Color", "Default", "Primary", "Secondary", "Success", "Error", "Warning", "Info",
        Category = "Radio Group", Order = 3)]
    public string RadioColor { get; set; } = "Primary";

    [NpText("Publish Topic", Category = "Radio Group", Order = 4, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpCheckbox("Read Only (no selection action)", Category = "Radio Group", Order = 5)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 6)]
    public bool Retain { get; set; } = false;

    [NpCheckbox("Publish to MQTT broker (uncheck = dashboard-local only)", Category = "Publish", Order = 7)]
    public bool PublishGlobally { get; set; } = true;

    /// <summary>Parsed list of (label, value) option pairs.</summary>
    public IEnumerable<(string Label, string Value)> ItemList =>
        string.IsNullOrWhiteSpace(Items)
            ? []
            : Items.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Where(line => !line.StartsWith('#'))
                   .Select(line =>
                   {
                       var eq = line.IndexOf('=');
                       return eq < 0
                           ? (line, line)
                           : (line[..eq].Trim(), line[(eq + 1)..].Trim());
                   });

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new RadioGroupNodeData
        {
            Items = Items,
            Orientation = Orientation,
            RadioColor = RadioColor,
            PublishTopic = PublishTopic,
            IsReadOnly = IsReadOnly,
            Retain = Retain,
            PublishGlobally = PublishGlobally,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static RadioGroupNodeModel FromData(RadioGroupNodeData data)
    {
        var node = new RadioGroupNodeModel(new Point(data.X, data.Y))
        {
            Items = data.Items,
            Orientation = data.Orientation ?? "Horizontal",
            RadioColor = data.RadioColor ?? "Primary",
            PublishTopic = data.PublishTopic,
            IsReadOnly = data.IsReadOnly,
            Retain = data.Retain,
            PublishGlobally = data.PublishGlobally,
        };
        return ApplyBaseData(node, data);
    }
}
