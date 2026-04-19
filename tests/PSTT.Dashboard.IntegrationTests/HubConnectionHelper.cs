using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using PSTT.Remote;

namespace PSTT.Dashboard.IntegrationTests;

/// <summary>
/// Helpers for creating SignalR <see cref="HubConnection"/> and PSTT <see cref="RemoteCache{TValue}"/>
/// instances that connect to the in-process test server via HTTP long-polling.
/// </summary>
public static class HubConnectionHelper
{
    /// <summary>
    /// Creates a <see cref="HubConnection"/> connected to the test server's SignalR hub.
    /// For most tests, prefer <see cref="CreateRemoteCache"/> which wraps this in the PSTT
    /// <see cref="RemoteCache{TValue}"/> client.
    /// </summary>
    public static HubConnection Create(WebApplicationFactory<Program> factory, string hubPath = "cachehub")
    {
        var httpClient = factory.CreateClient();

        return new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, hubPath), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
    }

    /// <summary>
    /// Creates a <see cref="RemoteCache{TValue}"/> wired to the test server's <c>/cachehub</c>
    /// endpoint via a test-server-backed <see cref="HubConnection"/>.
    /// Call <see cref="RemoteCache{TValue}.ConnectAsync"/> on the result before subscribing.
    /// </summary>
    public static RemoteCache<string> CreateRemoteCache(WebApplicationFactory<Program> factory)
    {
        var connection = Create(factory);
        return new RemoteCacheBuilder<string>()
            .WithSignalRTransport(connection)
            .WithUtf8Encoding()
            .Build();
    }

    /// <summary>Waits for <paramref name="connection"/> to receive a message within <paramref name="timeout"/>.</summary>
    public static Task<T> WaitForAsync<T>(
        HubConnection connection, string methodName, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? registration = null;
        registration = connection.On<T>(methodName, value =>
        {
            tcs.TrySetResult(value);
            registration?.Dispose();
        });
        return tcs.Task.WaitAsync(timeout);
    }

    /// <summary>Waits for a two-parameter message.</summary>
    public static Task<(T1, T2)> WaitForAsync<T1, T2>(
        HubConnection connection, string methodName, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<(T1, T2)>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? registration = null;
        registration = connection.On<T1, T2>(methodName, (a, b) =>
        {
            tcs.TrySetResult((a, b));
            registration?.Dispose();
        });
        return tcs.Task.WaitAsync(timeout);
    }

    /// <summary>Waits for a three-parameter message.</summary>
    public static Task<(T1, T2, T3)> WaitForAsync<T1, T2, T3>(
        HubConnection connection, string methodName, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<(T1, T2, T3)>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? registration = null;
        registration = connection.On<T1, T2, T3>(methodName, (a, b, c) =>
        {
            tcs.TrySetResult((a, b, c));
            registration?.Dispose();
        });
        return tcs.Task.WaitAsync(timeout);
    }
}
