using MudBlazor;
using MudBlazor.Utilities;
using Microsoft.AspNetCore.Components;

namespace MqttDashboard.Components;

public enum ColorPickerMode
{
    Theme,
    Named,
    Custom
}

public partial class ColorPickerDialog
{
    [CascadingParameter] private IMudDialogInstance? MudDialog { get; set; }
    [Parameter] public ColorPickerMode Mode { get; set; }
    [Parameter] public string? CurrentColor { get; set; }

    private string? SelectedColor;
    private MudColor CustomColor = new MudColor("#FFFFFF");
    private string CustomColorString = "";

    private static readonly Dictionary<string, string> ThemeColors = new()
    {
        { "Primary", "var(--mud-palette-primary)" },
        { "Primary Lighten", "var(--mud-palette-primary-lighten)" },
        { "Primary Darken", "var(--mud-palette-primary-darken)" },
        { "Secondary", "var(--mud-palette-secondary)" },
        { "Secondary Lighten", "var(--mud-palette-secondary-lighten)" },
        { "Secondary Darken", "var(--mud-palette-secondary-darken)" },
        { "Tertiary", "var(--mud-palette-tertiary)" },
        { "Tertiary Lighten", "var(--mud-palette-tertiary-lighten)" },
        { "Tertiary Darken", "var(--mud-palette-tertiary-darken)" },
        { "Info", "var(--mud-palette-info)" },
        { "Info Lighten", "var(--mud-palette-info-lighten)" },
        { "Info Darken", "var(--mud-palette-info-darken)" },
        { "Success", "var(--mud-palette-success)" },
        { "Success Lighten", "var(--mud-palette-success-lighten)" },
        { "Success Darken", "var(--mud-palette-success-darken)" },
        { "Warning", "var(--mud-palette-warning)" },
        { "Warning Lighten", "var(--mud-palette-warning-lighten)" },
        { "Warning Darken", "var(--mud-palette-warning-darken)" },
        { "Error", "var(--mud-palette-error)" },
        { "Error Lighten", "var(--mud-palette-error-lighten)" },
        { "Error Darken", "var(--mud-palette-error-darken)" },
        { "Dark", "var(--mud-palette-dark)" },
        { "Dark Lighten", "var(--mud-palette-dark-lighten)" },
        { "Dark Darken", "var(--mud-palette-dark-darken)" },
        { "Surface", "var(--mud-palette-surface)" },
        { "Background", "var(--mud-palette-background)" },
        { "Background Grey", "var(--mud-palette-background-grey)" },
        { "Drawer Background", "var(--mud-palette-drawer-background)" },
        { "Appbar Background", "var(--mud-palette-appbar-background)" },
    };

    private static readonly List<string> NamedColors = new()
    {
        // Basic
        "white", "black", "gray", "silver", "lightgray", "darkgray",
        // Reds
        "red", "darkred", "crimson", "indianred", "lightcoral", "salmon", "lightsalmon", "firebrick",
        // Oranges
        "orange", "darkorange", "coral", "tomato", "orangered",
        // Browns
        "chocolate", "sienna", "brown", "maroon", "saddlebrown",
        // Yellows
        "yellow", "gold", "lightyellow", "lemonchiffon", "khaki", "palegoldenrod",
        // Greens
        "green", "darkgreen", "lime", "limegreen", "lightgreen", "palegreen", "springgreen",
        "seagreen", "forestgreen", "olive", "olivedrab", "yellowgreen", "lawngreen",
        // Blues
        "blue", "darkblue", "navy", "midnightblue", "royalblue", "steelblue", "lightblue",
        "skyblue", "lightskyblue", "deepskyblue", "dodgerblue", "cornflowerblue", "cadetblue",
        "powderblue", "slateblue", "mediumblue",
        // Cyans
        "aqua", "cyan", "lightcyan", "turquoise", "darkturquoise", "mediumturquoise", "paleturquoise",
        // Purples
        "purple", "indigo", "darkviolet", "violet", "plum", "orchid", "mediumorchid", "mediumpurple",
        "blueviolet", "darkmagenta", "darkslateblue",
        // Pinks
        "magenta", "fuchsia", "pink", "lightpink", "hotpink", "deeppink", "palevioletred", "mediumvioletred",
        // Neutrals
        "beige", "tan", "wheat", "peachpuff", "bisque", "blanchedalmond", "cornsilk",
        // Pastels
        "lavender", "thistle", "honeydew", "azure", "aliceblue", "ghostwhite", 
        "mintcream", "seashell", "oldlace", "linen", "ivory", "snow"
    };

    protected override void OnInitialized()
    {
        SelectedColor = CurrentColor;
        
        if (Mode == ColorPickerMode.Custom)
        {
            if (!string.IsNullOrEmpty(CurrentColor))
            {
                CustomColorString = CurrentColor;
                try
                {
                    CustomColor = new MudColor(CurrentColor);
                }
                catch
                {
                    CustomColor = new MudColor("#FFFFFF");
                }
            }
        }
    }

    protected override void OnParametersSet()
    {
        if (Mode == ColorPickerMode.Custom && CustomColor != null)
        {
            CustomColorString = CustomColor.Value;
        }
    }

    private void SelectColor(string color)
    {
        SelectedColor = color;
        StateHasChanged();
    }

    private void Select()
    {
        string? resultColor = null;
        
        if (Mode == ColorPickerMode.Custom)
        {
            // Prefer custom string if provided, otherwise use picker value
            resultColor = !string.IsNullOrWhiteSpace(CustomColorString) 
                ? CustomColorString 
                : CustomColor?.Value;
        }
        else
        {
            resultColor = SelectedColor;
        }
        
        MudDialog?.Close(DialogResult.Ok(resultColor));
    }

    private void Cancel()
    {
        MudDialog?.Cancel();
    }
}
