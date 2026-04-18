using MqttDashboard.Models;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using System.Reflection;

namespace MqttDashboard.Components;

public partial class NodePropertyEditor
{
    [CascadingParameter] private IMudDialogInstance? MudDialog { get; set; }
    [Parameter] public TextNodeModel Node { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private double Width { get; set; }
    private double Height { get; set; }
    private int? FontSize { get; set; }
    private int _newMetadataCounter = 1;

    protected override void OnInitialized()
    {
        Width    = Node.Size?.Width  ?? 120;
        Height   = Node.Size?.Height ?? 90;
        FontSize = Node.FontSize;
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

    private void Save()
    {
        // Update size
        Node.Size = new Blazor.Diagrams.Core.Geometry.Size(Width, Height);

        // Update font size (null = default)
        Node.FontSize = FontSize > 0 ? FontSize : null;

        // Refresh the node to update the display
        Node.Refresh();

        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        MudDialog?.Cancel();
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
}
