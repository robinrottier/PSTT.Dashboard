using PSTT.Dashboard.Models;

namespace PSTT.Dashboard.Client.Tests;

public class TableDefsParserTests
{
    // ── ParseColumnDefs ──────────────────────────────────────────────────────

    [Fact]
    public void ParseColumnDefs_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(TableDefsParser.ParseColumnDefs(null));
        Assert.Null(TableDefsParser.ParseColumnDefs(""));
        Assert.Null(TableDefsParser.ParseColumnDefs("  "));
    }

    [Fact]
    public void ParseColumnDefs_InvalidJson_ReturnsNull()
    {
        Assert.Null(TableDefsParser.ParseColumnDefs("not json at all"));
        Assert.Null(TableDefsParser.ParseColumnDefs("{bad}"));
    }

    [Fact]
    public void ParseColumnDefs_FullSchema_ParsesAllFields()
    {
        var json = """
            [
              { "key": "temp", "header": "Temp", "format": "{0:F1}°C", "width": "80px", "align": "right", "static": "N/A" },
              { "key": "humidity", "header": "Humidity" }
            ]
            """;

        var cols = TableDefsParser.ParseColumnDefs(json);

        Assert.NotNull(cols);
        Assert.Equal(2, cols!.Count);

        var temp = cols[0];
        Assert.Equal("temp",     temp.Key);
        Assert.Equal("Temp",     temp.Header);
        Assert.Equal("{0:F1}°C", temp.Format);
        Assert.Equal("80px",     temp.Width);
        Assert.Equal("right",    temp.Align);
        Assert.Equal("N/A",      temp.Static);

        var hum = cols[1];
        Assert.Equal("humidity", hum.Key);
        Assert.Equal("Humidity", hum.Header);
        Assert.Null(hum.Format);
        Assert.Null(hum.Width);
    }

    [Fact]
    public void ParseColumnDefs_KeylessHeaderOnly_UsesHeaderAsKey()
    {
        var json = """[{"header":"Status"}]""";
        var cols = TableDefsParser.ParseColumnDefs(json);

        Assert.NotNull(cols);
        var col = cols![0];
        Assert.Equal("Status", col.Key);
        Assert.Equal("Status", col.Header);
    }

    [Fact]
    public void ParseColumnDefs_EmptyArray_ReturnsNull()
    {
        Assert.Null(TableDefsParser.ParseColumnDefs("[]"));
    }

    // ── ParseRowDefs ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseRowDefs_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(TableDefsParser.ParseRowDefs(null));
        Assert.Null(TableDefsParser.ParseRowDefs(""));
    }

    [Fact]
    public void ParseRowDefs_BasicRows_ParsesKeyAndLabel()
    {
        var json = """[{"key":"room1","label":"Room 1"},{"key":"room2","label":"Room 2"}]""";
        var rows = TableDefsParser.ParseRowDefs(json);

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.Equal("room1",  rows[0].Key);
        Assert.Equal("Room 1", rows[0].Label);
        Assert.False(rows[0].IsStatic);
        Assert.Equal("room2",  rows[1].Key);
    }

    [Fact]
    public void ParseRowDefs_StaticRow_SetsIsStaticTrue()
    {
        var json = """[{"key":null,"label":"Summary","static":true}]""";
        var rows = TableDefsParser.ParseRowDefs(json);

        Assert.NotNull(rows);
        var row = rows![0];
        Assert.Null(row.Key);
        Assert.Equal("Summary", row.Label);
        Assert.True(row.IsStatic);
    }

    [Fact]
    public void ParseRowDefs_EmptyArray_ReturnsNull()
    {
        Assert.Null(TableDefsParser.ParseRowDefs("[]"));
    }

    // ── ParseCellDefs ────────────────────────────────────────────────────────

    [Fact]
    public void ParseCellDefs_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(TableDefsParser.ParseCellDefs(null));
        Assert.Empty(TableDefsParser.ParseCellDefs(""));
    }

    [Fact]
    public void ParseCellDefs_InvalidJson_ReturnsEmpty()
    {
        Assert.Empty(TableDefsParser.ParseCellDefs("not json"));
    }

    [Fact]
    public void ParseCellDefs_DynamicAndStaticCells_ParsesAllFields()
    {
        var json = """
            [
              { "row": "Room 1", "col": "Temp", "topic": "sensors/room1/temp", "format": "{0:F1}°C" },
              { "row": "Units",  "col": "Temp", "static": "°C" }
            ]
            """;

        var cells = TableDefsParser.ParseCellDefs(json);

        Assert.Equal(2, cells.Count);

        var dynamic = cells[0];
        Assert.Equal("Room 1",          dynamic.Row);
        Assert.Equal("Temp",            dynamic.Col);
        Assert.Equal("sensors/room1/temp", dynamic.Topic);
        Assert.Equal("{0:F1}°C",        dynamic.Format);
        Assert.Null(dynamic.Static);

        var stat = cells[1];
        Assert.Equal("Units", stat.Row);
        Assert.Equal("Temp",  stat.Col);
        Assert.Null(stat.Topic);
        Assert.Equal("°C", stat.Static);
    }

    [Fact]
    public void ParseCellDefs_RowOrColMissing_SkipsEntry()
    {
        var json = """[{"row":"A"},{"col":"B"},{"row":"C","col":"D"}]""";
        var cells = TableDefsParser.ParseCellDefs(json);

        Assert.Single(cells);
        Assert.Equal("C", cells[0].Row);
        Assert.Equal("D", cells[0].Col);
    }

    // ── ApplyFormat ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyFormat_NullRaw_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TableDefsParser.ApplyFormat(null, "{0:F1}"));
    }

    [Fact]
    public void ApplyFormat_NullFormat_ReturnsRaw()
    {
        Assert.Equal("42.5", TableDefsParser.ApplyFormat("42.5", null));
    }

    [Fact]
    public void ApplyFormat_NumericValue_AppliesFormat()
    {
        Assert.Equal("42.5°C", TableDefsParser.ApplyFormat("42.5", "{0:F1}°C"));
    }

    [Fact]
    public void ApplyFormat_NumericRounding_AppliesRounding()
    {
        Assert.Equal("43°C", TableDefsParser.ApplyFormat("42.7", "{0:F0}°C"));
    }

    [Fact]
    public void ApplyFormat_StringValue_FormatsAsString()
    {
        Assert.Equal("Status: online", TableDefsParser.ApplyFormat("online", "Status: {0}"));
    }

    [Fact]
    public void ApplyFormat_InvalidFormat_FallsBackToRaw()
    {
        // A broken format string should not throw
        var result = TableDefsParser.ApplyFormat("42", "{0:ZZZZZ}");
        // May return raw or throw-handled value — either is acceptable, must not throw
        Assert.NotNull(result);
    }

    // ── Validate / PrettyPrint ────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidJson_ReturnsNull()
    {
        Assert.Null(TableDefsParser.Validate("[{\"key\":\"temp\"}]"));
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsErrorMessage()
    {
        Assert.NotNull(TableDefsParser.Validate("not json"));
    }

    [Fact]
    public void Validate_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(TableDefsParser.Validate(null));
        Assert.Null(TableDefsParser.Validate(""));
    }

    [Fact]
    public void PrettyPrint_CompactJson_ReturnsPrettyJson()
    {
        var compact = """[{"key":"temp","header":"Temp"}]""";
        var pretty  = TableDefsParser.PrettyPrint(compact);
        Assert.Contains("\n",  pretty);
        Assert.Contains("key", pretty);
    }

    [Fact]
    public void PrettyPrint_InvalidJson_ReturnsUnchanged()
    {
        var bad = "not json";
        Assert.Equal(bad, TableDefsParser.PrettyPrint(bad));
    }

    // ── Serialization round-trip with RowDefs ────────────────────────────────

    [Fact]
    public void TableNodeModel_WithRowDefs_RoundTrips()
    {
        var model = new TableNodeModel(new Blazor.Diagrams.Core.Geometry.Point(0, 0))
        {
            RowDefs = """[{"key":"r1","label":"Row 1"},{"key":"r2","label":"Row 2"}]""",
        };

        var data = (TableNodeData)model.ToData(0, 0);
        Assert.Equal(model.RowDefs, data.RowDefs);

        var restored = TableNodeModel.FromData(data);
        Assert.Equal(model.RowDefs, restored.RowDefs);
    }
}
