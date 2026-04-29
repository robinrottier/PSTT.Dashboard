using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

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

    [NpNumeric("Time Column Width (px, 0=auto)", Category = "Column Widths", Order = 8, Min = 0, Max = 400)]
    public int TimeWidth { get; set; } = 0;

    [NpNumeric("Topic Column Width (px, 0=auto)", Category = "Column Widths", Order = 9, Min = 0, Max = 400)]
    public int TopicWidth { get; set; } = 0;

    [NpNumeric("Value Column Width (px, 0=auto)", Category = "Column Widths", Order = 10, Min = 0, Max = 400)]
    public int ValueWidth { get; set; } = 0;

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
                TimeWidth = TimeWidth > 0 ? TimeWidth : null,
                TopicWidth = TopicWidth > 0 ? TopicWidth : null,
                ValueWidth = ValueWidth > 0 ? ValueWidth : null,
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
            TimeWidth = data.Columns?.TimeWidth ?? 0,
            TopicWidth = data.Columns?.TopicWidth ?? 0,
            ValueWidth = data.Columns?.ValueWidth ?? 0,
        };
        return ApplyBaseData(node, data);
    }
}
