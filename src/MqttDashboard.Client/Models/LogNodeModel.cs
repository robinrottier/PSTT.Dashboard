using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class LogNodeModel : TextNodeModel
{
    public LogNodeModel(Point? position = null) : base(position) { NodeType = "Log"; }

    [NpNumeric("Max Entries", Category = "Log", Order = 1, Min = 1, Max = 500)]
    public int MaxEntries { get; set; } = 20;

    [NpCheckbox("Date", Category = "Columns", Order = 2)]
    public bool ShowDate { get; set; } = false;

    [NpCheckbox("Time", Category = "Columns", Order = 3)]
    public bool ShowTime { get; set; } = true;

    [NpCheckbox("Full topic", Category = "Columns", Order = 4)]
    public bool ShowTopicFull { get; set; } = false;

    [NpCheckbox("Topic path", Category = "Columns", Order = 5)]
    public bool ShowTopicPath { get; set; } = false;

    [NpCheckbox("Topic name", Category = "Columns", Order = 6)]
    public bool ShowTopicName { get; set; } = false;

    [NpCheckbox("Value", Category = "Columns", Order = 7)]
    public bool ShowValue { get; set; } = true;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new LogNodeData
        {
            MaxEntries = MaxEntries,
            Columns = new LogColumnsData
            {
                ShowDate = ShowDate ? true : null,
                ShowTime = ShowTime ? null : false,   // default is true; only store when false
                TopicFull = ShowTopicFull ? true : null,
                TopicPath = ShowTopicPath ? true : null,
                TopicName = ShowTopicName ? true : null,
                Value = ShowValue ? null : false,     // default is true; only store when false
            },
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static LogNodeModel FromData(LogNodeData data)
    {
        var node = new LogNodeModel(new Point(data.X, data.Y))
        {
            MaxEntries = data.MaxEntries ?? 20,
            ShowDate = data.Columns?.ShowDate ?? false,
            ShowTime = data.Columns?.ShowTime ?? true,
            ShowTopicFull = data.Columns?.TopicFull ?? false,
            ShowTopicPath = data.Columns?.TopicPath ?? false,
            ShowTopicName = data.Columns?.TopicName ?? false,
            ShowValue = data.Columns?.Value ?? true,
        };
        return ApplyBaseData(node, data);
    }
}
