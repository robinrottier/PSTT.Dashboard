using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class SwitchNodeModel : TextNodeModel
{
    public SwitchNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Switch";
    }

    [NpText("Publish Topic", Category = "Switch", Order = 1, Placeholder = "Defaults to Data Topic if empty")]
    public string? PublishTopic { get; set; }

    [NpText("ON Value", Category = "Switch", Order = 2, Placeholder = "1")]
    public string OnValue { get; set; } = "1";

    [NpText("OFF Value", Category = "Switch", Order = 3, Placeholder = "0")]
    public string OffValue { get; set; } = "0";

    [NpSelect("Switch Style", "Full", "Compact", "IconOnly",
        Category = "Switch", Order = 4,
        Labels = ["Full (chip + icon)", "Compact (text + icon)", "Icon Only"])]
    public string SwitchStyle { get; set; } = "Full";

    [NpText("ON Text", Category = "Switch", Order = 5, Placeholder = "ON")]
    public string OnText { get; set; } = "ON";

    [NpText("OFF Text", Category = "Switch", Order = 6, Placeholder = "OFF")]
    public string OffText { get; set; } = "OFF";

    [NpCheckbox("Read Only (display only, no toggle)", Category = "Switch", Order = 7)]
    public bool IsReadOnly { get; set; } = false;

    [NpCheckbox("Retain message (broker stores last value)", Category = "Publish", Order = 8)]
    public bool Retain { get; set; } = false;

    [NpSelect("QoS Level", "0", "1", "2",
        Category = "Publish", Order = 9,
        Labels = ["0 — At Most Once (fire and forget)", "1 — At Least Once", "2 — Exactly Once"])]
    public int QosLevel { get; set; } = 0;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new SwitchNodeData
        {
            Switch = new SwitchSettingsData
            {
                PublishTopic = PublishTopic,
                OnValue = OnValue,
                OffValue = OffValue,
                Style = SwitchStyle,
                OnText = OnText,
                OffText = OffText,
                ReadOnly = IsReadOnly,
                Retain = Retain,
                Qos = QosLevel,
            },
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static SwitchNodeModel FromData(SwitchNodeData data)
    {
        var node = new SwitchNodeModel(new Point(data.X, data.Y))
        {
            PublishTopic = data.Switch?.PublishTopic,
            OnValue = data.Switch?.OnValue ?? "1",
            OffValue = data.Switch?.OffValue ?? "0",
            SwitchStyle = data.Switch?.Style ?? "Full",
            OnText = data.Switch?.OnText ?? "ON",
            OffText = data.Switch?.OffText ?? "OFF",
            IsReadOnly = data.Switch?.ReadOnly ?? false,
            Retain = data.Switch?.Retain ?? false,
            QosLevel = data.Switch?.Qos ?? 0,
        };
        return ApplyBaseData(node, data);
    }
}
