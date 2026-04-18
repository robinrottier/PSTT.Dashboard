using MudBlazor;

namespace MqttDashboard.Models;

/// <summary>
/// Represents a node in the MQTT topic tree
/// </summary>
public class MqttTopicNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public object? Value { get; set; }
    public bool HasValue => Value != null;
    public HashSet<MqttTopicNode> Children { get; set; } = new();
    public bool HasChildren => Children.Any();
    public string Icon => HasChildren ? Icons.Material.Filled.Folder : Icons.Material.Filled.Label;
    public MudBlazor.Color IconColor => HasChildren ? MudBlazor.Color.Warning : MudBlazor.Color.Info;

    /// <summary>
    /// Build a tree structure from flat topic list for MudTreeView
    /// </summary>
    public static HashSet<MqttTopicNode> BuildTree(Dictionary<string, object> topicValues)
    {
        var rootSet = new HashSet<MqttTopicNode>();

        foreach (var kvp in topicValues.OrderBy(x => x.Key))
        {
            var parts = kvp.Key.Split('/');
            var currentLevel = rootSet;
            var currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                currentPath += (i > 0 ? "/" : "") + part;
                var isLastPart = i == parts.Length - 1;

                var existingNode = currentLevel.FirstOrDefault(n => n.Name == part);

                if (existingNode == null)
                {
                    var newNode = new MqttTopicNode
                    {
                        Name = part,
                        FullPath = currentPath,
                        Value = isLastPart ? kvp.Value : null
                    };
                    currentLevel.Add(newNode);
                    currentLevel = newNode.Children;
                }
                else
                {
                    if (isLastPart)
                    {
                        existingNode.Value = kvp.Value;
                    }
                    currentLevel = existingNode.Children;
                }
            }
        }

        return rootSet;
    }
}
