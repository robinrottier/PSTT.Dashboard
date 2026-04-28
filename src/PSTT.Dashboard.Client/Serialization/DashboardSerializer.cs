using System.Collections;
using System.Reflection;
using System.Text.Json;
using PSTT.Dashboard.Models;

namespace PSTT.Dashboard.Serialization;

/// <summary>
/// Maps GUIDs/opaque IDs to sequential 1-based integers for file serialization.
/// The first time a given ID is encountered it is assigned the next integer;
/// subsequent occurrences of the same ID return the same integer (preserving
/// cross-references between nodes, ports and links).
/// </summary>
public sealed class DashboardIdMapper
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);
    private int _next;

    /// <summary>Returns the mapped sequential ID for <paramref name="id"/>, creating a new mapping if needed.</summary>
    public string Map(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (!_map.TryGetValue(id, out var mapped))
            _map[id] = mapped = (++_next).ToString();
        return mapped;
    }
}

/// <summary>
/// Serializes and deserializes <see cref="DashboardModel"/> to/from JSON, replacing
/// runtime GUIDs with compact sequential integers on write via <see cref="DashboardIdMapper"/>.
/// <para>
/// Properties marked with <see cref="FileIdAttribute"/> are detected by reflection.
/// The model is deep-cloned before remapping so the in-memory model is never mutated.
/// </para>
/// </summary>
public static class DashboardSerializer
{
    /// <summary>
    /// Serializes <paramref name="model"/> to JSON with all <see cref="FileIdAttribute"/>-marked
    /// properties replaced by sequential 1-based integers.
    /// </summary>
    public static string Serialize(DashboardModel model, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(CloneAndRemap(model, options), options);

    /// <summary>
    /// Serializes <paramref name="model"/> wrapped in <c>{"psttdashboard":"dashboard","data":{...}}</c>
    /// with sequential IDs applied to all <see cref="FileIdAttribute"/>-marked properties.
    /// </summary>
    public static string SerializeDashboard(DashboardModel model, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(new { psttdashboard = "dashboard", data = CloneAndRemap(model, options) }, options);

    /// <summary>
    /// Serializes <paramref name="page"/> wrapped in <c>{"psttdashboard":"page","data":{...}}</c>
    /// with sequential IDs applied to all <see cref="FileIdAttribute"/>-marked properties.
    /// </summary>
    public static string SerializePage(DashboardPageModel page, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(page, options);
        var clone = JsonSerializer.Deserialize<DashboardPageModel>(json, options)
                    ?? throw new InvalidOperationException("DashboardPageModel clone deserialization returned null.");
        RemapIds(clone, new DashboardIdMapper());
        return JsonSerializer.Serialize(new { psttdashboard = "page", data = clone }, options);
    }

    /// <summary>
    /// Serializes <paramref name="nodes"/> wrapped in <c>{"psttdashboard":"nodes","data":[...]}</c>
    /// with sequential IDs applied to all <see cref="FileIdAttribute"/>-marked properties.
    /// </summary>
    public static string SerializeNodes(List<NodeData> nodes, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(nodes, options);
        var clone = JsonSerializer.Deserialize<List<NodeData>>(json, options) ?? [];
        var mapper = new DashboardIdMapper();
        foreach (var node in clone)
            RemapIds(node, mapper);
        return JsonSerializer.Serialize(new { psttdashboard = "nodes", data = clone }, options);
    }

    /// <summary>
    /// Deserializes <paramref name="json"/> to a <see cref="DashboardModel"/>.
    /// No ID remapping is applied on load; the IDs in the file (sequential integers or GUIDs)
    /// are used as-is and remain valid opaque strings throughout the session.
    /// </summary>
    public static DashboardModel? Deserialize(string json, JsonSerializerOptions? options = null)
        => JsonSerializer.Deserialize<DashboardModel>(json, options);

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Deep-clones <paramref name="model"/> via JSON round-trip and remaps all IDs.</summary>
    private static DashboardModel CloneAndRemap(DashboardModel model, JsonSerializerOptions? options)
    {
        var json = JsonSerializer.Serialize(model, options);
        var clone = JsonSerializer.Deserialize<DashboardModel>(json, options)
                    ?? throw new InvalidOperationException("DashboardModel clone deserialization returned null.");
        RemapIds(clone, new DashboardIdMapper());
        return clone;
    }

    private static void RemapIds(object? obj, DashboardIdMapper mapper)
    {
        if (obj is null) return;

        var type = obj.GetType();

        // Sort by MetadataToken: stable C# declaration order within each type.
        // This guarantees that within DashboardPageModel, Nodes (definitions)
        // are visited before Links (references), so cross-references resolve correctly.
        foreach (var prop in type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.MetadataToken))
        {
            if (!prop.CanRead) continue;

            if (prop.IsDefined(typeof(FileIdAttribute), inherit: true))
            {
                // Remap this ID in place on the clone
                if (prop.CanWrite && prop.GetValue(obj) is string id && !string.IsNullOrEmpty(id))
                    prop.SetValue(obj, mapper.Map(id));
                continue;
            }

            var val = prop.GetValue(obj);
            if (val is null or string) continue;  // strings without [FileId] are not IDs

            if (val is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    if (item is not null and not string && item.GetType().IsClass)
                        RemapIds(item, mapper);
            }
            else if (val.GetType().IsClass)
            {
                RemapIds(val, mapper);
            }
        }
    }
}
