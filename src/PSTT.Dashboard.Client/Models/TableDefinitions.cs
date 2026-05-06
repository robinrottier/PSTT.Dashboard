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
    string? Static,
    /// <summary>
    /// Whether the user can drag to resize this column at runtime.
    /// Null/true = resizable (default). False = fixed width.
    /// </summary>
    bool? Resizable = null,
    /// <summary>Background color for cells in this column.</summary>
    string? Bg = null,
    /// <summary>Text color for cells in this column.</summary>
    string? Color = null);

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
    bool IsStatic,
    /// <summary>C# format string applied to all values in this row (column format takes priority).</summary>
    string? Format = null,
    /// <summary>CSS text-align for cells in this row (column align takes priority).</summary>
    string? Align = null,
    /// <summary>Background color for cells in this row.</summary>
    string? Bg = null,
    /// <summary>Text color for cells in this row.</summary>
    string? Color = null);

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

/// <summary>
/// Table-level appearance settings. Parsed from the <c>TableStyle</c> JSON property.
/// All fields are optional CSS values (colors, etc.).
/// </summary>
public sealed record TableStyleDef(
    /// <summary>Background color for the header row.</summary>
    string? HeaderBg,
    /// <summary>Text color for the header row.</summary>
    string? HeaderColor,
    /// <summary>Background color applied to alternating (even-index) data rows.</summary>
    string? AltRowBg,
    /// <summary>Border color for the table.</summary>
    string? BorderColor,
    /// <summary>Default text color for data cells.</summary>
    string? TextColor,
    /// <summary>CSS width of the row-label column e.g. "100px".</summary>
    string? LabelWidth = null);

/// <summary>
/// A condition entry used inside <see cref="CellStyleDef.Conditions"/>.
/// When the cell value matches, its <see cref="Bg"/> and <see cref="Color"/> are applied.
/// </summary>
public sealed record CellCondition(
    /// <summary>Comparison operator: &gt;=, &gt;, &lt;=, &lt;, ==, !=</summary>
    string? Op,
    /// <summary>Numeric threshold (used when the raw value can be parsed as a number).</summary>
    double? Value,
    /// <summary>String comparison value (used for == / != when the raw value is not numeric).</summary>
    string? Str,
    /// <summary>Background color to apply when this condition matches.</summary>
    string? Bg,
    /// <summary>Text color to apply when this condition matches.</summary>
    string? Color);

/// <summary>
/// Per-cell (or per-row-column wildcard) style override.
/// Parsed from the <c>CellStyle</c> JSON property.
/// Row and Col support "*" as a wildcard matching any key.
/// </summary>
public sealed record CellStyleDef(
    /// <summary>Row key to match, or "*" for any row.</summary>
    string? Row,
    /// <summary>Column key to match, or "*" for any column.</summary>
    string? Col,
    /// <summary>Background color override.</summary>
    string? Bg,
    /// <summary>Text color override.</summary>
    string? Color,
    /// <summary>Conditional overrides evaluated in order; first match wins.</summary>
    CellCondition[]? Conditions);

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
                var key       = Str(el, "key");
                var header    = Str(el, "header");
                var format    = Str(el, "format");
                var width     = Str(el, "width");
                var align     = Str(el, "align");
                var stat      = Str(el, "static");
                var resizable = BoolN(el, "resizable");
                var bg        = Str(el, "bg");
                var color     = Str(el, "color");
                if (!string.IsNullOrEmpty(key) || !string.IsNullOrEmpty(header))
                    result.Add(new ColumnDef(key ?? header!, header, format, width, align, stat, resizable, bg, color));
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
                var format   = Str(el, "format");
                var align    = Str(el, "align");
                var bg       = Str(el, "bg");
                var color    = Str(el, "color");
                if (!string.IsNullOrEmpty(key) || !string.IsNullOrEmpty(label))
                    result.Add(new RowDef(key, label, isStatic, format, align, bg, color));
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

    private static bool? BoolN(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v)
            ? (v.ValueKind == JsonValueKind.True ? true : v.ValueKind == JsonValueKind.False ? false : null)
            : null;

    public static TableStyleDef? ParseTableStyle(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(json, _opts);
            return new TableStyleDef(
                Str(el, "headerBg"),
                Str(el, "headerColor"),
                Str(el, "altRowBg"),
                Str(el, "borderColor"),
                Str(el, "textColor"),
                Str(el, "labelWidth"));
        }
        catch { return null; }
    }

    // ── CellStyle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a CellStyle JSON array into a list of <see cref="CellStyleDef"/> records.
    /// Returns an empty list when the string is empty or invalid.
    /// </summary>
    public static List<CellStyleDef> ParseCellStyleDefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement[]>(json, _opts);
            if (arr == null) return [];
            var result = new List<CellStyleDef>(arr.Length);
            foreach (var el in arr)
            {
                var row        = Str(el, "row");
                var col        = Str(el, "col");
                var bg         = Str(el, "bg");
                var color      = Str(el, "color");
                var conditions = ParseConditions(el);
                result.Add(new CellStyleDef(row, col, bg, color, conditions));
            }
            return result;
        }
        catch { return []; }
    }

    private static CellCondition[]? ParseConditions(JsonElement el)
    {
        if (!el.TryGetProperty("conditions", out var cArr) || cArr.ValueKind != JsonValueKind.Array)
            return null;
        var list = new List<CellCondition>();
        foreach (var c in cArr.EnumerateArray())
        {
            var op = Str(c, "op");
            double? numVal = null;
            string? strVal = null;
            if (c.TryGetProperty("value", out var vEl))
            {
                if (vEl.ValueKind == JsonValueKind.Number)
                    numVal = vEl.GetDouble();
                else if (vEl.ValueKind == JsonValueKind.String)
                    strVal = vEl.GetString();
            }
            var cbg   = Str(c, "bg");
            var ccolor = Str(c, "color");
            if (op != null)
                list.Add(new CellCondition(op, numVal, strVal, cbg, ccolor));
        }
        return list.Count > 0 ? [.. list] : null;
    }

    /// <summary>
    /// Evaluates a <see cref="CellStyleDef"/>'s conditions against a raw cell value.
    /// Returns the first matching <see cref="CellCondition"/>, or <c>null</c> if none match.
    /// </summary>
    public static CellCondition? EvaluateCondition(CellStyleDef cellStyle, string? rawValue)
    {
        if (cellStyle.Conditions == null || cellStyle.Conditions.Length == 0) return null;
        var isNum = double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var num);
        foreach (var cond in cellStyle.Conditions)
        {
            var match = cond.Op switch
            {
                ">=" => isNum && num >= (cond.Value ?? 0),
                ">"  => isNum && num >  (cond.Value ?? 0),
                "<=" => isNum && num <= (cond.Value ?? 0),
                "<"  => isNum && num <  (cond.Value ?? 0),
                "==" => isNum ? num == (cond.Value ?? 0) : rawValue == cond.Str,
                "!=" => isNum ? num != (cond.Value ?? 0) : rawValue != cond.Str,
                _    => false
            };
            if (match) return cond;
        }
        return null;
    }
}
