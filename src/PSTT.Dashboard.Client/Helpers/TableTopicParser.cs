namespace PSTT.Dashboard.Helpers;

/// <summary>
/// Utility methods for the table widget's topic pattern matching.
/// Extracted as a standalone static class so they can be unit-tested independently.
/// </summary>
public static class TableTopicParser
{
    /// <summary>
    /// Converts a pattern like "sensors/{row}/{col}" to an MQTT wildcard "sensors/+/+"
    /// suitable for use with <c>BridgedDataCache.GetSnapshot()</c> and <c>Subscribe()</c>.
    /// Any <c>{placeholder}</c> segment is replaced with <c>+</c>.
    /// </summary>
    public static string PatternToWildcard(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(pattern, @"\{[^}]+\}", "+");
    }

    /// <summary>
    /// Given a pattern (e.g. "sensors/{row}/{col}") and an actual MQTT topic
    /// (e.g. "sensors/room1/temp"), extracts the values bound to the {row} and {col}
    /// placeholders. Returns <c>false</c> if the topic segment count does not match the
    /// pattern, or if a literal segment in the pattern does not equal the topic segment.
    /// </summary>
    /// <param name="pattern">The user-configured pattern string.</param>
    /// <param name="topic">An actual MQTT topic key from the data cache.</param>
    /// <param name="row">Receives the value of the {row} placeholder, or null if absent.</param>
    /// <param name="col">Receives the value of the {col} / {column} placeholder, or null if absent.</param>
    /// <returns><c>true</c> if at least one placeholder was matched successfully.</returns>
    public static bool TryExtractSegments(
        string? pattern, string topic,
        out string? row, out string? col)
    {
        row = null; col = null;
        if (string.IsNullOrEmpty(pattern)) return false;

        var patternParts = pattern.Split('/');
        var topicParts   = topic.Split('/');
        if (patternParts.Length != topicParts.Length) return false;

        for (int i = 0; i < patternParts.Length; i++)
        {
            var p = patternParts[i];
            if (p.StartsWith('{') && p.EndsWith('}'))
            {
                var name = p[1..^1].ToLowerInvariant();
                if (name == "row")               row = topicParts[i];
                else if (name is "col" or "column") col = topicParts[i];
                // Unknown placeholder names are silently ignored
            }
            else if (p != "+" && p != "#" && p != topicParts[i])
            {
                // Literal segment mismatch — topic does not match this pattern
                return false;
            }
        }

        return row != null || col != null;
    }
}
