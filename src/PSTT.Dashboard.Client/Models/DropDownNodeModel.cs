using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class DropDownNodeModel : TextNodeModel
{
    public DropDownNodeModel(Point? position = null) : base(position)
    {
        NodeType = "DropDown";
    }

    /// <summary>Comma-separated list of options to show in the dropdown.</summary>
    [NpText("Options (comma-separated)", Category = "Drop Down", Order = 1, Placeholder = "On,Off,Auto")]
    public string? Options { get; set; }

    [NpText("Publish Topic", Category = "Drop Down", Order = 2, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpCheckbox("Read Only (display only, no selection)", Category = "Drop Down", Order = 3)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 4)]
    public bool Retain { get; set; } = false;

    [NpCheckbox("Publish to MQTT broker (uncheck = dashboard-local only)", Category = "Publish", Order = 5)]
    public bool PublishGlobally { get; set; } = true;

    public IEnumerable<string> OptionList =>
        string.IsNullOrWhiteSpace(Options)
            ? []
            : Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new DropDownNodeData
        {
            Options = Options,
            PublishTopic = PublishTopic,
            IsReadOnly = IsReadOnly,
            Retain = Retain,
            PublishGlobally = PublishGlobally,
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static DropDownNodeModel FromData(DropDownNodeData data)
    {
        var node = new DropDownNodeModel(new Point(data.X, data.Y))
        {
            Options = data.Options,
            PublishTopic = data.PublishTopic,
            IsReadOnly = data.IsReadOnly,
            Retain = data.Retain,
            PublishGlobally = data.PublishGlobally,
        };
        return ApplyBaseData(node, data);
    }
}
