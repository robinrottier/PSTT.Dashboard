namespace PSTT.Dashboard.Components;

public class TopicNode
{
    public string Label { get; set; } = string.Empty;
    public string FullTopic { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsLeaf => Children.Count == 0;
    public List<TopicNode> Children { get; set; } = new();
    internal Dictionary<string, TopicNode> ChildMap { get; } = new();
}
