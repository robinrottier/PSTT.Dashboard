using System.Reflection;
using System.Text.Json;
using PSTT.Dashboard.Models;

namespace PSTT.Dashboard.Helpers;

/// <summary>
/// JSON-based snapshot of a node's NpXxx-decorated properties.
/// Used for grid-view Cancel and will serve as the foundation for
/// the future JSON property editing feature.
/// </summary>
public static class NodeModelSnapshot
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = false,
        IncludeFields = false,
    };

    /// <summary>
    /// Serialises all NpXxx-decorated properties of the node to a JSON string.
    /// Handles all attribute types including NpCustom (arbitrary reference types).
    /// </summary>
    public static string Capture(TextNodeModel node)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in GetSnapshotProps(node.GetType()))
        {
            try
            {
                var value = prop.GetValue(node);
                dict[prop.Name] = JsonSerializer.SerializeToElement(value, prop.PropertyType, _opts);
            }
            catch { /* skip unserializable properties */ }
        }
        return JsonSerializer.Serialize(dict, _opts);
    }

    /// <summary>
    /// Restores NpXxx-decorated properties from a previously captured JSON snapshot.
    /// Properties that cannot be deserialized are silently skipped.
    /// </summary>
    public static void Restore(TextNodeModel node, string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot)) return;
        Dictionary<string, JsonElement>? dict;
        try { dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(snapshot, _opts); }
        catch { return; }
        if (dict == null) return;

        foreach (var prop in GetSnapshotProps(node.GetType()).Where(p => p.CanWrite))
        {
            if (!dict.TryGetValue(prop.Name, out var element)) continue;
            try
            {
                var value = element.Deserialize(prop.PropertyType, _opts);
                prop.SetValue(node, value);
            }
            catch { /* skip properties that fail to deserialize */ }
        }
    }

    private static IEnumerable<PropertyInfo> GetSnapshotProps(Type type) =>
        type.GetProperties()
            .Where(p => p.GetCustomAttribute<NodePropertyAttribute>() != null && p.CanRead);
}
