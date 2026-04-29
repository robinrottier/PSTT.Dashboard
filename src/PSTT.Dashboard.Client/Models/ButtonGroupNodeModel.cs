using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class ButtonGroupNodeModel : TextNodeModel
{
    public ButtonGroupNodeModel(Point? position = null) : base(position)
    {
        NodeType = "ButtonGroup";
    }

    /// <summary>
    /// Newline-separated list of buttons. Each line: "Label=Value".
    /// Label is the button text; Value is what gets published when clicked.
    /// Lines starting with # are treated as comments and ignored.
    /// </summary>
    [NpText("Buttons (one per line: Label=Value)", Category = "Button Group", Order = 1,
        Placeholder = "Manual=0\nAuto=1\nOff=2",
        Lines = 5)]
    public string? Items { get; set; }

    [NpSelect("Layout", "Horizontal", "Vertical",
        Category = "Button Group", Order = 2,
        Labels = ["Horizontal", "Vertical"])]
    public string Orientation { get; set; } = "Horizontal";

    [NpSelect("Button Style", "Filled", "Outlined", "Text",
        Category = "Button Group", Order = 3,
        Labels = ["Filled", "Outlined", "Text (flat)"])]
    public string ButtonVariant { get; set; } = "Outlined";

    [NpSelect("Button Color", "Default", "Primary", "Secondary", "Success", "Error", "Warning", "Info",
        Category = "Button Group", Order = 4)]
    public string ButtonColor { get; set; } = "Primary";

    [NpSelect("Active Button Color", "Default", "Primary", "Secondary", "Success", "Error", "Warning", "Info",
        Category = "Button Group", Order = 5)]
    public string ActiveButtonColor { get; set; } = "Success";

    [NpText("Publish Topic", Category = "Button Group", Order = 6, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpCheckbox("Read Only (no click action)", Category = "Button Group", Order = 7)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 8)]
    public bool Retain { get; set; } = false;

    [NpCheckbox("Publish to MQTT broker (uncheck = dashboard-local only)", Category = "Publish", Order = 9)]
    public bool PublishGlobally { get; set; } = true;

    /// <summary>Parsed list of (label, value) button pairs.</summary>
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
        var data = new ButtonGroupNodeData
        {
            Items = Items,
            Orientation = Orientation,
            ButtonVariant = ButtonVariant,
            ButtonColor = ButtonColor,
            ActiveButtonColor = ActiveButtonColor,
            PublishTopic = PublishTopic,
            IsReadOnly = IsReadOnly,
            Retain = Retain,
            PublishGlobally = PublishGlobally,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static ButtonGroupNodeModel FromData(ButtonGroupNodeData data)
    {
        var node = new ButtonGroupNodeModel(new Point(data.X, data.Y))
        {
            Items = data.Items,
            Orientation = data.Orientation ?? "Horizontal",
            ButtonVariant = data.ButtonVariant ?? "Outlined",
            ButtonColor = data.ButtonColor ?? "Primary",
            ActiveButtonColor = data.ActiveButtonColor ?? "Success",
            PublishTopic = data.PublishTopic,
            IsReadOnly = data.IsReadOnly,
            Retain = data.Retain,
            PublishGlobally = data.PublishGlobally,
        };
        return ApplyBaseData(node, data);
    }
}
