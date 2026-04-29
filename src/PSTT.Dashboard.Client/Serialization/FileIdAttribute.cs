namespace PSTT.Dashboard.Serialization;

/// <summary>
/// Marks a <c>string</c> property whose value is a dashboard element ID (node, port, page)
/// or a reference to such an ID (link source/target). <see cref="DashboardSerializer"/>
/// replaces every value with a short sequential integer on save so that dashboard files
/// use compact, human-readable IDs instead of raw GUIDs.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class FileIdAttribute : Attribute { }
