using System.Globalization;
using System.Text.Json;

namespace PSTT.Dashboard.Models;

// ── Data records ─────────────────────────────────────────────────────────────

/// <summary>
/// Defines one column in the table widget.
/// Parsed from the <c>ColumnDefs</c> JSON property.
/// </summary>
public sealed record ColumnDef(
    /// <summary>Matches the {col} segment of the incoming MQTT topic.</summary>
    string Key,
    /// <summary>Text shown in the column header. Defaults to <see cref="Key"/>.</summary>
    string? Header,
    /// <summary>C# format string applied to the live value e.g. "{0:F1}°C".</summary>
    string? Format,
    /// <summary>CSS width of the column e.g. "80px".</summary>
    string? Width,
    /// <summary>CSS text-align: left | right | center.</summary>
    string? Align,
    /// <summary>Fixed text shown when no live data is present.</summary>
    string? Static);

/// <summary>
/// Defines one row in the table widget.
/// Parsed from the <c>RowDefs</c> JSON property.
/// </summary>
public sealed record RowDef(
    /// <summary>Matches the {row} segment of the incoming MQTT topic. Null for static-only rows.</summary>
    string? Key,
    /// <summary>Display label shown in the row label column. Defaults to <see cref="Key"/>.</summary>
    string? Label,
    /// <summary>When true this row has no data subscription — cells show static content from CellDefs.</summary>
    bool IsStatic);

/// <summary>
/// Defines one explicit cell in PerCell data mode.
/// Parsed from the <c>CellDefs</c> JSON property.
/// </summary>
public sealed record CellDef(
    /// <summary>Row key (display key, not necessarily a topic segment in PerCell mode).</summary>
    string Row,
    /// <summary>Column key.</summary>
    string Col,
    /// <summary>MQTT topic to subscribe to. Null for static-only cells.</summary>
    string? Topic,
    /// <summary>C# format string applied to the live value e.g. "{0:F1}°C".</summary>
    string? Format,
    /// <summary>Fixed text shown when no live data is present, or always when no topic is set.</summary>
    string? Static);

// ── Parser ───────────────────────────────────────────────────────────────────

/// <summary>
/// Parses the JSON definition strings stored in <see cref="TableNodeModel"/> properties
/// into strongly-typed record lists. All methods are null-safe and return empty/null on
/// invalid JSON rather than throwing.
/// </summary>
public static class TableDefsParser
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // ── ColumnDefs ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a ColumnDefs JSON string into a list of <see cref="ColumnDef"/> records.
    /// Returns <c>null</c> (fall back to auto-discovery) when the string is empty or
    /// the resulting list is empty.
    /// </summary>
    public static List<ColumnDef>? ParseColumnDefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement[]>(json, _opts);
            if (arr == null) return null;
            var result = new List<ColumnDef>(arr.Length);
            foreach (var el in arr)
            {
                var key    = Str(el, "key");
                var header = Str(el, "header");
                var format = Str(el, "format");
                var width  = Str(el, "width");
                var align  = Str(el, "align");
                var stat   = Str(el, "static");
                if (!string.IsNullOrEmpty(key) || !string.IsNullOrEmpty(header))
                    result.Add(new ColumnDef(key ?? header!, header, format, width, align, stat));
            }
            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    // ── RowDefs ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a RowDefs JSON string into a list of <see cref="RowDef"/> records.
    /// Returns <c>null</c> when the string is empty (meaning rows are auto-discovered).
    /// </summary>
    public static List<RowDef>? ParseRowDefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement[]>(json, _opts);
            if (arr == null) return null;
            var result = new List<RowDef>(arr.Length);
            foreach (var el in arr)
            {
                var key      = Str(el, "key");
                var label    = Str(el, "label");
                var isStatic = Bool(el, "static");
                if (!string.IsNullOrEmpty(key) || !string.IsNullOrEmpty(label))
                    result.Add(new RowDef(key, label, isStatic));
            }
            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    // ── CellDefs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a CellDefs JSON string into a list of <see cref="CellDef"/> records.
    /// Returns an empty list when the string is empty or invalid.
    /// </summary>
    public static List<CellDef> ParseCellDefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement[]>(json, _opts);
            if (arr == null) return [];
            var result = new List<CellDef>(arr.Length);
            foreach (var el in arr)
            {
                var row    = Str(el, "row");
                var col    = Str(el, "col");
                var topic  = Str(el, "topic");
                var format = Str(el, "format");
                var stat   = Str(el, "static");
                if (!string.IsNullOrEmpty(row) && !string.IsNullOrEmpty(col))
                    result.Add(new CellDef(row!, col!, topic, format, stat));
            }
            return result;
        }
        catch { return []; }
    }

    // ── Value formatting ──────────────────────────────────────────────────────

    /// <summary>
    /// Applies a C# composite format string to a raw MQTT value string.
    /// If the value can be parsed as a number, it is formatted as <c>double</c>.
    /// Falls back to the raw string on any error.
    /// </summary>
    public static string ApplyFormat(string? raw, string? format)
    {
        if (raw == null) return string.Empty;
        if (string.IsNullOrEmpty(format)) return raw;
        try
        {
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return string.Format(format, d);
            return string.Format(format, raw);
        }
        catch { return raw; }
    }

    // ── JSON validation / formatting ──────────────────────────────────────────

    /// <summary>
    /// Validates that <paramref name="json"/> is well-formed JSON.
    /// Returns <c>null</c> on success, or an error message on failure.
    /// </summary>
    public static string? Validate(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            return null;
        }
        catch (JsonException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Pretty-prints <paramref name="json"/>. Returns the input unchanged if it is
    /// not valid JSON (caller should validate first).
    /// </summary>
    public static string PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
