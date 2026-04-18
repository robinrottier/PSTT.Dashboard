using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

namespace MqttDashboard.IntegrationTests;

/// <summary>
/// Helpers for creating <see cref="HubConnection"/> instances that connect to the
/// in-process test server via HTTP long-polling (avoids WebSocket complexity in tests).
/// </summary>
public static class HubConnectionHelper
{
    /// <summary>
    /// Creates a <see cref="HubConnection"/> connected to the test server's SignalR hub.
    /// Handlers should be registered on the returned connection BEFORE calling
    /// <see cref="HubConnection.StartAsync"/> so no messages are missed.
    /// </summary>
    public static HubConnection Create(WebApplicationFactory<Program> factory, string hubPath = "datahub")
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
    /// Waits for <paramref name="connection"/> to receive a message on <paramref name="methodName"/>
    /// within <paramref name="timeout"/>. Returns the received value or throws <see cref="TimeoutException"/>.
    /// </summary>
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

    /// <summary>Waits for a two-parameter message (e.g. <c>MqttConnectionStatus</c>).</summary>
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

    /// <summary>Waits for a three-parameter message (e.g. <c>ReceiveMqttData</c>).</summary>
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
