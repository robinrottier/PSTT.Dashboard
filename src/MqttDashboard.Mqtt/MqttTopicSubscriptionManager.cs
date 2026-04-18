using System.Collections.Concurrent;
using MqttDashboard.Data;

namespace MqttDashboard.Mqtt;

/// <summary>
/// Ref-counts broker-level MQTT subscriptions across all subscribers (SignalR hub clients
/// and the server-side <c>MqttDataServer</c>).
/// <para>
/// When the first subscriber registers interest in a topic, <see cref="OnTopicSubscribeRequested"/>
/// is fired to subscribe at the broker. When the last subscriber leaves, the broker subscription
/// is not released immediately — a <paramref name="gracePeriod"/> delay applies first.
/// If a new subscriber arrives during that window the pending unsubscribe is cancelled, avoiding
/// churn when a Blazor circuit reconnects.
/// </para>
/// </summary>
public class MqttTopicSubscriptionManager
{
    private readonly TimeSpan _gracePeriod;
    private readonly ConcurrentDictionary<string, TopicSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _clientTopics = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingUnsubs = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public event Func<string, Task>? OnTopicSubscribeRequested;
    public event Func<string, Task>? OnTopicUnsubscribeRequested;

    /// <param name="gracePeriodMs">
    /// Milliseconds to wait after the last subscriber leaves before actually unsubscribing
    /// from the broker. Defaults to 30 000 ms (30 s). Pass 0 to disable the grace period.
    /// </param>
    public MqttTopicSubscriptionManager(int gracePeriodMs = 30_000)
    {
        _gracePeriod = TimeSpan.FromMilliseconds(gracePeriodMs);
    }

    public async Task<bool> SubscribeClientToTopicAsync(string connectionId, string topic)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Cancel any pending unsubscribe for this topic before it fires.
            CancelPendingUnsubscribe(topic);

            // Track topics for this client
            if (!_clientTopics.TryGetValue(connectionId, out var clientTopics))
            {
                clientTopics = new HashSet<string>();
                _clientTopics[connectionId] = clientTopics;
            }

            if (clientTopics.Contains(topic))
                return false; // Already subscribed

            clientTopics.Add(topic);

            var subscription = _subscriptions.GetOrAdd(topic, _ => new TopicSubscription(topic));
            subscription.AddClient(connectionId);

            // First subscriber — request a broker-level subscription.
            if (subscription.RefCount == 1 && OnTopicSubscribeRequested != null)
                await OnTopicSubscribeRequested.Invoke(topic);

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> UnsubscribeClientFromTopicAsync(string connectionId, string topic)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_clientTopics.TryGetValue(connectionId, out var clientTopics))
                return false;

            if (!clientTopics.Remove(topic))
                return false;

            if (!_subscriptions.TryGetValue(topic, out var subscription))
                return false;

            subscription.RemoveClient(connectionId);

            if (subscription.RefCount == 0)
            {
                _subscriptions.TryRemove(topic, out _);
                ScheduleUnsubscribe(topic);
            }

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UnsubscribeClientFromAllTopicsAsync(string connectionId)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_clientTopics.TryRemove(connectionId, out var clientTopics))
                return;

            foreach (var topic in clientTopics)
            {
                if (_subscriptions.TryGetValue(topic, out var subscription))
                {
                    subscription.RemoveClient(connectionId);

                    if (subscription.RefCount == 0)
                    {
                        _subscriptions.TryRemove(topic, out _);
                        ScheduleUnsubscribe(topic);
                    }
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public HashSet<string> GetInterestedClients(string topic)
    {
        var interestedClients = new HashSet<string>();
        foreach (var subscription in _subscriptions.Values)
        {
            if (TopicMatcher.Matches(subscription.Topic, topic))
                foreach (var client in subscription.GetClients())
                    interestedClients.Add(client);
        }
        return interestedClients;
    }

    /// <summary>Returns true if the given topic subscription <paramref name="filter"/> matches a concrete <paramref name="topic"/>.</summary>
    public bool TopicMatchesFilter(string filter, string topic) => TopicMatcher.Matches(filter, topic);

    public List<string> GetClientSubscriptions(string connectionId)
    {
        if (_clientTopics.TryGetValue(connectionId, out var topics))
            return topics.ToList();
        return [];
    }

    // ── Grace-period helpers ─────────────────────────────────────────────────────

    private void ScheduleUnsubscribe(string topic)
    {
        if (_gracePeriod == TimeSpan.Zero)
        {
            // No grace period — fire immediately (but still async to keep the semaphore released).
            _ = FireUnsubscribeAsync(topic);
            return;
        }

        var cts = new CancellationTokenSource();
        _pendingUnsubs[topic] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_gracePeriod, cts.Token);
                // Grace period elapsed with no resubscribe — actually unsubscribe from broker.
                _pendingUnsubs.TryRemove(topic, out _);
                await FireUnsubscribeAsync(topic);
            }
            catch (OperationCanceledException)
            {
                // Resubscribed during grace period — nothing to do.
            }
        }, cts.Token);
    }

    private void CancelPendingUnsubscribe(string topic)
    {
        if (_pendingUnsubs.TryRemove(topic, out var cts))
            cts.Cancel();
    }

    private async Task FireUnsubscribeAsync(string topic)
    {
        if (OnTopicUnsubscribeRequested != null)
            await OnTopicUnsubscribeRequested.Invoke(topic);
    }

    // ── Inner subscription record ────────────────────────────────────────────────

    private class TopicSubscription
    {
        private readonly HashSet<string> _clients = new();
        public string Topic { get; }
        public int RefCount => _clients.Count;

        public TopicSubscription(string topic) { Topic = topic; }

        public void AddClient(string connectionId) => _clients.Add(connectionId);
        public void RemoveClient(string connectionId) => _clients.Remove(connectionId);
        public HashSet<string> GetClients() => new(_clients);
    }
}
