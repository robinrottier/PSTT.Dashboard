using Blazor.Diagrams.Core.Geometry;

namespace PSTT.Dashboard.Models;

public class TableNodeModel : TextNodeModel
{
    public TableNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Table";
    }

    /// <summary>
    /// How table data is sourced.
    /// "PerTable": a topic pattern with {row}/{col} placeholders drives the whole table.
    /// "PerCell":  each cell has an explicit topic defined in <see cref="CellDefs"/> JSON.
    /// </summary>
    [NpSelect("Data Mode", "PerTable", "PerCell",
        Category = "Table", Order = 1,
        Labels = ["Per Table (wildcard pattern)", "Per Cell (explicit topics)"])]
    public string DataMode { get; set; } = "PerTable";

    /// <summary>
    /// Topic pattern using {row} and/or {col} placeholders.
    /// E.g. "sensors/{row}/{col}" — the widget subscribes to "sensors/+/+" and extracts
    /// row and column identifiers from the actual topic keys as data arrives.
    /// Used in PerTable mode.
    /// </summary>
    [NpText("Data Pattern", Category = "Table", Order = 2,
        Placeholder = "sensors/{row}/{col}",
        HelperText = "Use {row} and {col} as placeholders. The widget subscribes using MQTT wildcards (+) and extracts row/col keys from actual topic paths.")]
    public string? DataPattern { get; set; }

    /// <summary>
    /// JSON array defining columns: [{key, header, format, width, align, static}].
    /// "key" matches the {col} segment of incoming topics.
    /// "header" is the column header label (static text).
    /// "format" is a C# format string applied to the live value, e.g. "{0:F1}°C".
    /// "static" is fixed text shown when no live data is present.
    /// "width" is a CSS width, e.g. "80px".
    /// "align" is text-align: left, right, or center.
    /// If omitted in PerTable mode, columns are auto-discovered from arriving data.
    /// </summary>
    [NpText("Column Definitions (JSON)", Category = "Table", Order = 3,
        Lines = 6,
        Placeholder = "[{\"key\":\"temp\",\"header\":\"Temp\",\"format\":\"{0:F1}°C\",\"width\":\"80px\",\"align\":\"right\"},{\"key\":\"humidity\",\"header\":\"Humidity\",\"format\":\"{0:F0}%\"}]",
        HelperText = "JSON array of column definitions. Leave empty to auto-discover columns from data.")]
    public string? ColumnDefs { get; set; }

    /// <summary>
    /// JSON array of explicit cell definitions for PerCell mode:
    /// [{row, col, topic, format, static}].
    /// "row"/"col" are display keys. "topic" is the MQTT topic to subscribe to.
    /// "static" is fixed text (used when no topic, or as placeholder until data arrives).
    /// </summary>
    [NpText("Cell Definitions (JSON)", Category = "Table", Order = 4,
        Lines = 8,
        Placeholder = "[{\"row\":\"Room 1\",\"col\":\"Temp\",\"topic\":\"sensors/room1/temp\",\"format\":\"{0:F1}°C\"},{\"row\":\"Room 1\",\"col\":\"Humidity\",\"topic\":\"sensors/room1/humidity\",\"format\":\"{0:F0}%\"}]",
        HelperText = "JSON array of cell definitions for PerCell mode. Each cell can have a topic and/or static text.")]
    public string? CellDefs { get; set; }

    /// <summary>Show the header row (default true).</summary>
    [NpCheckbox("Show Header Row", Category = "Table", Order = 5)]
    public bool ShowHeader { get; set; } = true;

    /// <summary>Show the row label in the first column (default true).</summary>
    [NpCheckbox("Show Row Labels", Category = "Table", Order = 6)]
    public bool ShowRowLabels { get; set; } = true;

    // ── Serialization ──────────────────────────────────────────────────────────

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new TableNodeData
        {
            DataMode     = DataMode != "PerTable" ? DataMode : null,
            DataPattern  = DataPattern,
            ColumnDefs   = ColumnDefs,
            CellDefs     = CellDefs,
            ShowHeader   = ShowHeader   ? null : false,   // default true; only store when false
            ShowRowLabels = ShowRowLabels ? null : false,  // default true; only store when false
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static TableNodeModel FromData(TableNodeData data)
    {
        var node = new TableNodeModel(new Point(data.X, data.Y))
        {
            DataMode      = data.DataMode ?? "PerTable",
            DataPattern   = data.DataPattern,
            ColumnDefs    = data.ColumnDefs,
            CellDefs      = data.CellDefs,
            ShowHeader    = data.ShowHeader    ?? true,
            ShowRowLabels = data.ShowRowLabels ?? true,
        };
        return ApplyBaseData(node, data);
    }
}
