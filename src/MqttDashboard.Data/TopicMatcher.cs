namespace MqttDashboard.Data;

/// <summary>
/// MQTT topic-filter matching following the MQTT 3.1/5.0 specification.
/// <c>+</c> matches exactly one topic level; <c>#</c> matches any remaining levels.
/// </summary>
public static class TopicMatcher
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="topic"/> is matched by <paramref name="filter"/>.
    /// </summary>
    public static bool Matches(string filter, string topic)
    {
        if (filter == topic)
            return true;

        var filterParts = filter.Split('/');
        var topicParts  = topic.Split('/');

        if (filterParts.Length > 0 && filterParts[^1] == "#")
        {
            for (int i = 0; i < filterParts.Length - 1; i++)
            {
                if (i >= topicParts.Length) return false;
                if (filterParts[i] != "+" && filterParts[i] != topicParts[i]) return false;
            }
            return true;
        }

        if (filterParts.Length != topicParts.Length)
            return false;

        for (int i = 0; i < filterParts.Length; i++)
        {
            if (filterParts[i] == "+") continue;
            if (filterParts[i] != topicParts[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Converts an MQTT wildcard filter into a .NET <see cref="System.Text.RegularExpressions.Regex"/> pattern.
    /// Useful for client-side cache implementations that prefer regex-based dispatch.
    /// </summary>
    public static string ToRegexPattern(string filter) =>
        "^" + filter
            .Replace("/", "\\/")
            .Replace("+", "[^/]+")
            .Replace("#", ".*") + "$";
}
