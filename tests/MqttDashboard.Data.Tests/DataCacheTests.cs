using MqttDashboard.Data;

namespace MqttDashboard.Data.Tests;

public class DataCacheTests
{
    [Fact]
    public void UpdateValue_Then_GetValue_ReturnsValue()
    {
        var cache = new DataCache();
        cache.UpdateValue("sensor/temp", "23.5");
        Assert.Equal("23.5", cache.GetValue("sensor/temp"));
    }

    [Fact]
    public void GetValue_UnknownTopic_ReturnsNull()
    {
        var cache = new DataCache();
        Assert.Null(cache.GetValue("unknown/topic"));
    }

    [Fact]
    public void TryGetValue_TypedMatch_ReturnsTrue()
    {
        var cache = new DataCache();
        cache.UpdateValue("t", "hello");
        Assert.True(cache.TryGetValue<string>("t", out var val));
        Assert.Equal("hello", val);
    }

    [Fact]
    public void TryGetValue_WrongType_ReturnsFalse()
    {
        var cache = new DataCache();
        cache.UpdateValue("t", "hello");
        Assert.False(cache.TryGetValue<int>("t", out _));
    }

    [Fact]
    public void Subscribe_ExactTopic_CallbackFired()
    {
        var cache = new DataCache();
        string? received = null;
        using var _ = cache.Subscribe("sensor/temp", (topic, value) => received = value.ToString());
        cache.UpdateValue("sensor/temp", "42");
        Assert.Equal("42", received);
    }

    [Fact]
    public void Subscribe_WildcardPlus_MatchesSingleLevel()
    {
        var cache = new DataCache();
        var received = new List<string>();
        using var _ = cache.Subscribe("sensor/+/temp", (t, v) => received.Add(t));
        cache.UpdateValue("sensor/room1/temp", "20");
        cache.UpdateValue("sensor/room2/temp", "21");
        cache.UpdateValue("sensor/room1/humidity", "60"); // should NOT match
        Assert.Equal(2, received.Count);
        Assert.Contains("sensor/room1/temp", received);
        Assert.Contains("sensor/room2/temp", received);
    }

    [Fact]
    public void Subscribe_WildcardHash_MatchesMultipleLevels()
    {
        var cache = new DataCache();
        int callCount = 0;
        using var _ = cache.Subscribe("sensor/#", (t, v) => callCount++);
        cache.UpdateValue("sensor/temp", "1");
        cache.UpdateValue("sensor/room/temp", "2");
        cache.UpdateValue("other/topic", "3"); // should NOT match
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Dispose_WatcherHandle_StopsCallbacks()
    {
        var cache = new DataCache();
        int callCount = 0;
        var handle = cache.Subscribe("t", (_, _) => callCount++);
        cache.UpdateValue("t", "a");
        handle.Dispose();
        cache.UpdateValue("t", "b");
        Assert.Equal(1, callCount); // only the first update
    }

    [Fact]
    public void Dispose_WildcardHandle_StopsCallbacks()
    {
        var cache = new DataCache();
        int callCount = 0;
        var handle = cache.Subscribe("sensor/+", (_, _) => callCount++);
        cache.UpdateValue("sensor/temp", "a");
        handle.Dispose();
        cache.UpdateValue("sensor/temp", "b");
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GetAllTopics_ReturnsStoredTopics()
    {
        var cache = new DataCache();
        cache.UpdateValue("a/b", "1");
        cache.UpdateValue("c/d", "2");
        Assert.Contains("a/b", cache.GetAllTopics());
        Assert.Contains("c/d", cache.GetAllTopics());
    }

    [Fact]
    public void GetValuesByPattern_FiltersCorrectly()
    {
        var cache = new DataCache();
        cache.UpdateValue("sensor/temp", "20");
        cache.UpdateValue("sensor/humidity", "50");
        cache.UpdateValue("other/value", "99");
        var result = cache.GetValuesByPattern("sensor/#");
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("sensor/temp"));
        Assert.True(result.ContainsKey("sensor/humidity"));
    }

    [Fact]
    public void Clear_RemovesAllValues()
    {
        var cache = new DataCache();
        cache.UpdateValue("t", "v");
        cache.Clear();
        Assert.Null(cache.GetValue("t"));
    }

    [Fact]
    public void UpdateValue_StripInvalidXmlChars_FromString()
    {
        var cache = new DataCache();
        // null byte is stripped
        cache.UpdateValue("t", "hello\0world");
        Assert.Equal("helloworld", cache.GetValue("t"));
    }
}
