using MqttDashboard.Services;
using MqttDashboard.Models;
using Microsoft.Extensions.Configuration;

namespace MqttDashboard.Client.Tests;

public class ApplicationStateTests
{
    private static ApplicationState CreateState(int? maxHistory = null)
    {
        if (maxHistory == null) return new ApplicationState();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                { ["App:MaxMessageHistory"] = maxHistory.ToString() })
            .Build();
        return new ApplicationState(config);
    }

    [Fact]
    public void AddMessage_DefaultCap_DoesNotExceed500()
    {
        var state = CreateState();
        for (int i = 0; i < 600; i++)
            state.AddMessage(new MqttDataMessage { Topic = "t", Payload = i.ToString(), Timestamp = DateTime.UtcNow });

        Assert.Equal(500, state.Messages.Count);
    }

    [Fact]
    public void AddMessage_CustomCap_DoesNotExceedConfiguredLimit()
    {
        var state = CreateState(maxHistory: 10);
        for (int i = 0; i < 20; i++)
            state.AddMessage(new MqttDataMessage { Topic = "t", Payload = i.ToString(), Timestamp = DateTime.UtcNow });

        Assert.Equal(10, state.Messages.Count);
    }

    [Fact]
    public void AddMessage_RemovesOldestFirst()
    {
        var state = CreateState(maxHistory: 3);
        state.AddMessage(new MqttDataMessage { Topic = "t", Payload = "first", Timestamp = DateTime.UtcNow });
        state.AddMessage(new MqttDataMessage { Topic = "t", Payload = "second", Timestamp = DateTime.UtcNow });
        state.AddMessage(new MqttDataMessage { Topic = "t", Payload = "third", Timestamp = DateTime.UtcNow });
        state.AddMessage(new MqttDataMessage { Topic = "t", Payload = "fourth", Timestamp = DateTime.UtcNow });

        var msgs = state.RecentMessages(3);
        Assert.DoesNotContain(msgs, m => m.Payload == "first");
        Assert.Contains(msgs, m => m.Payload == "fourth");
    }

    [Fact]
    public async Task AddSubscriptionAsync_AddsToSubscribedTopics()
    {
        var state = CreateState();
        // Without a state service wired up, it should still add to the in-memory set
        // (SaveSubscriptionsAsync will be a no-op since _stateService is null)
        await state.AddSubscriptionAsync("test/topic");
        Assert.Contains("test/topic", state.SubscribedTopics);
    }

    [Fact]
    public async Task RemoveSubscriptionAsync_RemovesFromSubscribedTopics()
    {
        var state = CreateState();
        await state.AddSubscriptionAsync("test/topic");
        await state.RemoveSubscriptionAsync("test/topic");
        Assert.DoesNotContain("test/topic", state.SubscribedTopics);
    }
}
