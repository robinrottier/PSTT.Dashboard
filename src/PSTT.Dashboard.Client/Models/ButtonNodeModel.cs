using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class ButtonNodeModel : TextNodeModel
{
    public ButtonNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Button";
    }

    [NpText("Button Label", Category = "Button", Order = 1, Placeholder = "Press")]
    public string ButtonLabel { get; set; } = "Press";

    [NpText("Publish Value", Category = "Button", Order = 2, Placeholder = "1")]
    public string PublishValue { get; set; } = "1";

    [NpText("Publish Topic", Category = "Button", Order = 3, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpSelect("Button Style", "Filled", "Outlined", "Text",
        Category = "Button", Order = 4,
        Labels = ["Filled", "Outlined", "Text (flat)"])]
    public string ButtonVariant { get; set; } = "Filled";

    [NpSelect("Button Color", "Default", "Primary", "Secondary", "Success", "Error", "Warning", "Info",
        Category = "Button", Order = 5)]
    public string ButtonColor { get; set; } = "Primary";

    [NpCheckbox("Read Only (no click action)", Category = "Button", Order = 6)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 7)]
    public bool Retain { get; set; } = false;

    [NpCheckbox("Publish to MQTT broker (uncheck = dashboard-local only)", Category = "Publish", Order = 8)]
    public bool PublishGlobally { get; set; } = true;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new ButtonNodeData
        {
            ButtonLabel = ButtonLabel,
            PublishValue = PublishValue,
            PublishTopic = PublishTopic,
            ButtonVariant = ButtonVariant,
            ButtonColor = ButtonColor,
            IsReadOnly = IsReadOnly,
            Retain = Retain,
            PublishGlobally = PublishGlobally,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static ButtonNodeModel FromData(ButtonNodeData data)
    {
        var node = new ButtonNodeModel(new Point(data.X, data.Y))
        {
            ButtonLabel = data.ButtonLabel ?? "Press",
            PublishValue = data.PublishValue ?? "1",
            PublishTopic = data.PublishTopic,
            ButtonVariant = data.ButtonVariant ?? "Filled",
            ButtonColor = data.ButtonColor ?? "Primary",
            IsReadOnly = data.IsReadOnly,
            Retain = data.Retain,
            PublishGlobally = data.PublishGlobally,
        };
        return ApplyBaseData(node, data);
    }
}
