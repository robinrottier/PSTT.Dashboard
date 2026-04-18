using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace MqttDashboard.Server.Services;

/// <summary>
/// Short-lived one-use tokens issued by ServerAuthService for the Blazor Server login flow.
/// The Blazor Server circuit validates the password and issues a token; the browser redeems
/// it via a fresh GET request so the server can set the auth cookie on a live HTTP response.
/// </summary>
public sealed class LoginTokenStore
{
    private readonly record struct Entry(DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> _tokens = new();

    /// <summary>Issues a new one-use token valid for 60 seconds.</summary>
    public string Issue()
    {
        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = new Entry(DateTimeOffset.UtcNow.AddSeconds(60));
        PurgeExpired();
        return token;
    }

    /// <summary>Returns true (and removes the token) if the token is valid and not expired.</summary>
    public bool TryRedeem(string token)
    {
        if (_tokens.TryRemove(token, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return true;
        // Already expired — ensure removed
        _tokens.TryRemove(token, out _);
        return false;
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _tokens.Keys.ToArray())
            if (_tokens.TryGetValue(key, out var e) && e.ExpiresAt <= now)
                _tokens.TryRemove(key, out _);
    }
}
