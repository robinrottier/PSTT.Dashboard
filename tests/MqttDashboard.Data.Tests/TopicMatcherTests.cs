using MqttDashboard.Data;

namespace MqttDashboard.Data.Tests;

public class TopicMatcherTests
{
    [Theory]
    [InlineData("sensor/temp", "sensor/temp", true)]
    [InlineData("sensor/temp", "sensor/humidity", false)]
    [InlineData("sensor/+/temp", "sensor/living/temp", true)]
    [InlineData("sensor/+/temp", "sensor/living/humidity", false)]
    [InlineData("sensor/+/temp", "sensor/temp", false)] // different level count
    [InlineData("sensor/#", "sensor/temp", true)]
    [InlineData("sensor/#", "sensor/room/temp", true)]
    [InlineData("sensor/#", "other/temp", false)]
    [InlineData("#", "any/topic/at/all", true)]
    [InlineData("a/+/c", "a/b/c", true)]
    [InlineData("a/+/c", "a/b/d", false)]
    public void Matches_ReturnsExpected(string filter, string topic, bool expected)
    {
        Assert.Equal(expected, TopicMatcher.Matches(filter, topic));
    }

    [Fact]
    public void ToRegexPattern_GeneratesMatchingRegex()
    {
        var pattern = TopicMatcher.ToRegexPattern("sensor/+/temp");
        Assert.Matches(pattern, "sensor/room/temp");
        Assert.DoesNotMatch(pattern, "sensor/temp");
    }
}
