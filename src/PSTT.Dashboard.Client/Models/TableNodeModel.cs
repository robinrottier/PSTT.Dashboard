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
    /// See <see cref="TableDefsParser.ParseColumnDefs"/> for the schema.
    /// If omitted in PerTable mode, columns are auto-discovered from arriving data.
    /// </summary>
    [NpJson("Column Definitions (JSON)", Category = "Table", Order = 3,
        Lines = 5,
        ExampleJson = """[{"key":"temp","header":"Temp","format":"{0:F1}°C","width":"80px","align":"right"},{"key":"humidity","header":"Humidity","format":"{0:F0}%","width":"80px"}]""",
        HelperText = "JSON array of column definitions. Leave empty to auto-discover columns from data.")]
    public string? ColumnDefs { get; set; }

    /// <summary>
    /// JSON array defining fixed rows: [{key, label}].
    /// "key" matches the {row} segment of the incoming topic.
    /// "label" is the display text shown in the row label column.
    /// If omitted, rows are auto-discovered from arriving data (PerTable mode).
    /// </summary>
    [NpJson("Row Definitions (JSON)", Category = "Table", Order = 4,
        Lines = 4,
        ExampleJson = """[{"key":"room1","label":"Room 1"},{"key":"room2","label":"Room 2"}]""",
        HelperText = "JSON array of row definitions. Leave empty to auto-discover rows from data. Defines display order and labels.")]
    public string? RowDefs { get; set; }

    /// <summary>
    /// JSON array of explicit cell definitions for PerCell mode:
    /// [{row, col, topic, format, static}].
    /// See <see cref="TableDefsParser.ParseCellDefs"/> for the schema.
    /// </summary>
    [NpJson("Cell Definitions (JSON)", Category = "Table", Order = 5,
        Lines = 6,
        ExampleJson = """[{"row":"Room 1","col":"Temp","topic":"sensors/room1/temp","format":"{0:F1}°C"},{"row":"Room 1","col":"Humidity","topic":"sensors/room1/humidity","format":"{0:F0}%"},{"row":"Units","col":"Temp","static":"°C"}]""",
        HelperText = "JSON array of cell definitions for PerCell mode. Each cell can have a topic and/or static text.")]
    public string? CellDefs { get; set; }

    /// <summary>Show the header row (default true).</summary>
    [NpCheckbox("Show Header Row", Category = "Table", Order = 6)]
    public bool ShowHeader { get; set; } = true;

    /// <summary>Show the row label in the first column (default true).</summary>
    [NpCheckbox("Show Row Labels", Category = "Table", Order = 7)]
    public bool ShowRowLabels { get; set; } = true;

    /// <summary>
    /// Optional JSON object to style the table appearance.
    /// Supported fields: headerBg, headerColor, altRowBg, borderColor, textColor.
    /// </summary>
    [NpJson("Table Style (JSON)", Category = "Table", Order = 8,
        Lines = 4,
        ExampleJson = """{"headerBg":"#1E293B","headerColor":"#F8FAFC","altRowBg":"rgba(0,0,0,0.07)","borderColor":"rgba(0,0,0,0.12)","textColor":""}""",
        HelperText = "Optional styling object. All fields are optional CSS color values. Leave empty to use theme defaults.")]
    public string? TableStyle { get; set; }

    // ── Serialization ──────────────────────────────────────────────────────────

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new TableNodeData
        {
            DataMode      = DataMode != "PerTable" ? DataMode : null,
            DataPattern   = DataPattern,
            ColumnDefs    = ColumnDefs,
            RowDefs       = RowDefs,
            CellDefs      = CellDefs,
            ShowHeader    = ShowHeader    ? null : false,
            ShowRowLabels = ShowRowLabels ? null : false,
            TableStyle    = TableStyle,
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
            RowDefs       = data.RowDefs,
            CellDefs      = data.CellDefs,
            ShowHeader    = data.ShowHeader    ?? true,
            ShowRowLabels = data.ShowRowLabels ?? true,
            TableStyle    = data.TableStyle,
        };
        return ApplyBaseData(node, data);
    }
}
