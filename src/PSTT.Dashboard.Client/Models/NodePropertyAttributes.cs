namespace PSTT.Dashboard.Models;

/// <summary>
/// Base for all node-property attributes. Controls how a model property
/// is rendered inside <c>NodePropertyRenderer</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public abstract class NodePropertyAttribute(string displayName) : Attribute
{
    public string DisplayName { get; } = displayName;

    /// <summary>Groups displayed together under a section heading in the editor dialog.</summary>
    public string? Category { get; set; }

    /// <summary>Sort order within the category.</summary>
    public int Order { get; set; }

    public string? HelperText { get; set; }
}

/// <summary>Single-line or multi-line text field.</summary>
public sealed class NpTextAttribute(string displayName) : NodePropertyAttribute(displayName)
{
    public string? Placeholder { get; set; }
    public int Lines { get; set; } = 1;
}

/// <summary>Numeric input (double, double?, int, int?).</summary>
public sealed class NpNumericAttribute(string displayName) : NodePropertyAttribute(displayName)
{
    /// <summary>Use <see cref="double.NaN"/> (the default) to indicate "no limit".</summary>
    public double Min { get; set; } = double.NaN;
    /// <summary>Use <see cref="double.NaN"/> (the default) to indicate "no limit".</summary>
    public double Max { get; set; } = double.NaN;
    public string? Placeholder { get; set; }
}

/// <summary>Checkbox / boolean toggle.</summary>
public sealed class NpCheckboxAttribute(string displayName) : NodePropertyAttribute(displayName) { }

/// <summary>
/// Drop-down select. <paramref name="options"/> are the stored values;
/// set <see cref="Labels"/> to override the display text for each option.
/// </summary>
public sealed class NpSelectAttribute(string displayName, params string[] options) : NodePropertyAttribute(displayName)
{
    public string[] Options { get; } = options;

    /// <summary>Human-readable labels matching <see cref="Options"/> by index.</summary>
    public string[]? Labels { get; set; }
}

/// <summary>
/// JSON textarea with live validation, pretty-print button, and collapsible example.
/// Rendered as <c>JsonEditorField</c> inside <c>NodePropertyRenderer</c>.
/// </summary>
public sealed class NpJsonAttribute(string displayName) : NodePropertyAttribute(displayName)
{
    /// <summary>Number of visible lines in the textarea (default 6).</summary>
    public int Lines { get; set; } = 6;

    /// <summary>
    /// Optional example JSON shown in a collapsible "Show example" panel.
    /// If omitted, no example panel is rendered.
    /// </summary>
    public string? ExampleJson { get; set; }
}
/// The component must declare:
/// <code>
///   [Parameter] public TextNodeModel Node { get; set; }   // owning node
///   [Parameter] public object? Value { get; set; }        // the group object
/// </code>
/// </summary>
public sealed class NpCustomAttribute(string displayName, Type componentType) : NodePropertyAttribute(displayName)
{
    public Type ComponentType { get; } = componentType;
}
