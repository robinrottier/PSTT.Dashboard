using PSTT.Dashboard.Helpers;
using PSTT.Dashboard.Models;
using System.Text.Json;

namespace PSTT.Dashboard.Client.Tests;

public class TableTopicParserTests
{
    // ── PatternToWildcard ────────────────────────────────────────────────────

    [Fact]
    public void PatternToWildcard_RowAndCol_ReturnsDoubleWildcard()
    {
        Assert.Equal("sensors/+/+", TableTopicParser.PatternToWildcard("sensors/{row}/{col}"));
    }

    [Fact]
    public void PatternToWildcard_RowOnly_ReturnsSingleWildcard()
    {
        Assert.Equal("home/+/status", TableTopicParser.PatternToWildcard("home/{row}/status"));
    }

    [Fact]
    public void PatternToWildcard_NoPlaceholders_ReturnsLiteralTopic()
    {
        Assert.Equal("home/sensor/temp", TableTopicParser.PatternToWildcard("home/sensor/temp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PatternToWildcard_NullOrEmpty_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, TableTopicParser.PatternToWildcard(input));
    }

    // ── TryExtractSegments ───────────────────────────────────────────────────

    [Fact]
    public void TryExtractSegments_RowAndCol_ExtractsBoth()
    {
        var result = TableTopicParser.TryExtractSegments(
            "sensors/{row}/{col}", "sensors/room1/temp", out var row, out var col);

        Assert.True(result);
        Assert.Equal("room1", row);
        Assert.Equal("temp", col);
    }

    [Fact]
    public void TryExtractSegments_RowOnly_ExtractsRow()
    {
        var result = TableTopicParser.TryExtractSegments(
            "home/{row}/status", "home/device5/status", out var row, out var col);

        Assert.True(result);
        Assert.Equal("device5", row);
        Assert.Null(col);
    }

    [Fact]
    public void TryExtractSegments_ColAlias_ExtractsCol()
    {
        var result = TableTopicParser.TryExtractSegments(
            "data/{column}", "data/humidity", out var row, out var col);

        Assert.True(result);
        Assert.Null(row);
        Assert.Equal("humidity", col);
    }

    [Fact]
    public void TryExtractSegments_SegmentCountMismatch_ReturnsFalse()
    {
        var result = TableTopicParser.TryExtractSegments(
            "sensors/{row}/{col}", "sensors/room1", out var row, out var col);

        Assert.False(result);
        Assert.Null(row);
        Assert.Null(col);
    }

    [Fact]
    public void TryExtractSegments_LiteralMismatch_ReturnsFalse()
    {
        var result = TableTopicParser.TryExtractSegments(
            "sensors/{row}/{col}", "wrong/room1/temp", out var row, out var col);

        Assert.False(result);
    }

    [Fact]
    public void TryExtractSegments_NullPattern_ReturnsFalse()
    {
        var result = TableTopicParser.TryExtractSegments(
            null, "sensors/room1/temp", out var row, out var col);

        Assert.False(result);
        Assert.Null(row);
        Assert.Null(col);
    }

    [Fact]
    public void TryExtractSegments_DeepPath_ExtractsCorrectly()
    {
        var result = TableTopicParser.TryExtractSegments(
            "building/floor1/{row}/sensors/{col}",
            "building/floor1/lab3/sensors/co2",
            out var row, out var col);

        Assert.True(result);
        Assert.Equal("lab3", row);
        Assert.Equal("co2", col);
    }

    [Fact]
    public void TryExtractSegments_UnknownPlaceholder_IsIgnored()
    {
        // {type} is an unknown placeholder — should not break extraction
        var result = TableTopicParser.TryExtractSegments(
            "sensors/{row}/{type}", "sensors/room2/temperature", out var row, out var col);

        Assert.True(result);
        Assert.Equal("room2", row);
        Assert.Null(col); // {type} not mapped to col
    }

    // ── TableNodeModel serialization round-trip ──────────────────────────────

    [Fact]
    public void TableNodeModel_DefaultValues_RoundTrip()
    {
        var model = new TableNodeModel(new Blazor.Diagrams.Core.Geometry.Point(100, 200))
        {
            DataPattern = "sensors/{row}/{col}",
            ColumnDefs  = "[{\"key\":\"temp\",\"header\":\"Temp\"}]",
            ShowHeader  = true,
            ShowRowLabels = true,
        };

        var data = (PSTT.Dashboard.Models.TableNodeData)model.ToData(0, 0);

        // ShowHeader/ShowRowLabels default to true — should be stored as null (omitted)
        Assert.Null(data.ShowHeader);
        Assert.Null(data.ShowRowLabels);
        Assert.Equal("sensors/{row}/{col}", data.DataPattern);
        Assert.Equal("[{\"key\":\"temp\",\"header\":\"Temp\"}]", data.ColumnDefs);
        Assert.Null(data.DataMode); // "PerTable" is default — omitted
    }

    [Fact]
    public void TableNodeModel_NonDefaultValues_RoundTrip()
    {
        var model = new TableNodeModel(new Blazor.Diagrams.Core.Geometry.Point(0, 0))
        {
            DataMode    = "PerCell",
            ShowHeader  = false,
            ShowRowLabels = false,
        };

        var data = (PSTT.Dashboard.Models.TableNodeData)model.ToData(0, 0);

        Assert.Equal("PerCell", data.DataMode);
        Assert.Equal(false, data.ShowHeader);
        Assert.Equal(false, data.ShowRowLabels);
    }

    [Fact]
    public void TableNodeModel_FromData_RestoresDefaults()
    {
        var data = new TableNodeData
        {
            Id = "abc", X = 10, Y = 20, Width = 280, Height = 180,
            DataPattern = "home/{row}/{col}",
            // ShowHeader/ShowRowLabels omitted → should default to true
        };

        var model = TableNodeModel.FromData(data);

        Assert.True(model.ShowHeader);
        Assert.True(model.ShowRowLabels);
        Assert.Equal("PerTable", model.DataMode);
        Assert.Equal("home/{row}/{col}", model.DataPattern);
    }
}
