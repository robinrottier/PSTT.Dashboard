using PSTT.Dashboard.Models;
using PSTT.Dashboard.Helpers;
using PSTT.Dashboard.Services;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PSTT.Dashboard.Components;

public partial class NodePropertyEditor
{
    [Parameter] public TextNodeModel Node { get; set; } = default!;
    [Parameter] public EventCallback OnSaved { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ApplicationState AppState { get; set; } = default!;

    private double Width { get; set; }
    private double Height { get; set; }
    private int? FontSize { get; set; }
    private int _newMetadataCounter = 1;

    // Snapshot for cancel — captured when panel opens
    private double _savedWidth;
    private double _savedHeight;
    private int? _savedFontSize;

    protected override void OnParametersSet()
    {
        // Re-initialise whenever the node changes (panel switched to a different node)
        Width    = Node.Size?.Width  ?? 120;
        Height   = Node.Size?.Height ?? 90;
        FontSize = Node.FontSize;

        _savedWidth    = Width;
        _savedHeight   = Height;
        _savedFontSize = FontSize;
    }

    private async Task OpenIconPicker()
    {
        var parameters = new DialogParameters
        {
            { "CurrentIcon", Node.Icon }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<IconPickerDialog>("Select Icon", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data != null)
        {
            // The result is an anonymous object with IconPath and IconName
            var resultData = result.Data as dynamic;
            if (resultData != null)
            {
                Node.Icon = resultData.IconPath;
                Node.IconName = resultData.IconName;
                StateHasChanged();
            }
        }
    }

    private void ClearIcon()
    {
        Node.Icon = null;
        Node.IconName = null;
        StateHasChanged();
    }

    private string GetIconName()
    {
        if (string.IsNullOrEmpty(Node.Icon))
            return "No icon selected";

        return Node.IconName ?? "Custom Icon";
    }

    private void UpdateMetadata(string key, string value)
    {
        Node.Metadata[key] = value;
    }

    private void RemoveMetadata(string key)
    {
        Node.Metadata.Remove(key);
    }

    private void AddMetadata()
    {
        var newKey = $"Property{_newMetadataCounter++}";
        while (Node.Metadata.ContainsKey(newKey))
        {
            newKey = $"Property{_newMetadataCounter++}";
        }
        Node.Metadata[newKey] = "";
    }

    private async Task Save()
    {
        Node.Size    = new Blazor.Diagrams.Core.Geometry.Size(Width, Height);
        Node.FontSize = FontSize > 0 ? FontSize : null;
        Node.Refresh();

        _savedWidth    = Width;
        _savedHeight   = Height;
        _savedFontSize = FontSize;

        await OnSaved.InvokeAsync();
    }

    private async Task Cancel()
    {
        // Revert local state
        Width    = _savedWidth;
        Height   = _savedHeight;
        FontSize = _savedFontSize;

        await OnClose.InvokeAsync();
    }

    /// <summary>
    /// Returns the distinct [NpXxx]-annotated categories on this node type, in declaration order.
    /// These are the node-type-specific categories rendered by NodePropertyRenderer.
    /// </summary>
    private IEnumerable<string> GetNodeSpecificCategories() =>
        Node.GetType().GetProperties()
            .Select(p => p.GetCustomAttribute<NodePropertyAttribute>()?.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()!;

    // ── Table widget: discover defs from live data ────────────────────────────

    private static readonly JsonSerializerOptions _prettyJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Queries the current data cache for all topics matching the node's DataPattern,
    /// extracts unique column keys, and fills ColumnDefs with a minimal JSON array.
    /// </summary>
    private void FillColumnDefsFromData(TableNodeModel node)
    {
        var snapshot = GetPatternSnapshot(node.DataPattern);
        if (snapshot == null) return;

        var cols = new List<string>();
        foreach (var key in snapshot.Keys)
        {
            if (TableTopicParser.TryExtractSegments(node.DataPattern, key, out _, out var col)
                && col != null && !cols.Contains(col))
                cols.Add(col);
        }
        if (cols.Count == 0) return;

        var defs = cols.Select(c => new { key = c, header = c }).ToList();
        node.ColumnDefs = JsonSerializer.Serialize(defs, _prettyJson);
        StateHasChanged();
    }

    /// <summary>
    /// Queries the current data cache for all topics matching the node's DataPattern,
    /// extracts unique row keys, and fills RowDefs with a minimal JSON array.
    /// </summary>
    private void FillRowDefsFromData(TableNodeModel node)
    {
        var snapshot = GetPatternSnapshot(node.DataPattern);
        if (snapshot == null) return;

        var rows = new List<string>();
        foreach (var key in snapshot.Keys)
        {
            if (TableTopicParser.TryExtractSegments(node.DataPattern, key, out var row, out _)
                && row != null && !rows.Contains(row))
                rows.Add(row);
        }
        if (rows.Count == 0) return;

        var defs = rows.Select(r => new { key = r, label = r }).ToList();
        node.RowDefs = JsonSerializer.Serialize(defs, _prettyJson);
        StateHasChanged();
    }

    private IReadOnlyDictionary<string, string>? GetPatternSnapshot(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return null;
        var wildcard = TableTopicParser.PatternToWildcard(pattern);
        if (string.IsNullOrEmpty(wildcard)) return null;
        return AppState.BridgedDataCache.GetSnapshot(wildcard);
    }
}
