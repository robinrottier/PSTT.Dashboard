using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSTT.Data;
using PSTT.Remote;
using PSTT.Remote.AspNetCore.Extensions;
using PSTT.Remote.Transport.Tcp;

namespace PSTT.Dashboard.Server.Tests;

/// <summary>
/// Integration tests for the AddCacheTcpServer DI extension wired in Dashboard.Server.
///
/// These tests verify the Dashboard-level glue (ServiceCollectionExtensions.AddCacheTcpServer)
/// rather than the raw PSTT TCP round-trip (covered in PSTT.Remote.Tests).
///
/// Each test:
///   1. Wires a CacheWithWildcards upstream via AddCacheTcpServer (port 0 = OS-assigned).
///   2. Starts the IHostedService so the TCP listener is live.
///   3. Connects a RemoteCacheBuilder TCP client (in-process, loopback).
///   4. Verifies the expected data-flow scenario.
/// </summary>
public class TcpCacheServerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return false;
    }

    /// <summary>
    /// Build a ServiceProvider with AddCacheTcpServer wired to <paramref name="upstream"/>,
    /// start all hosted services (TcpCacheServerLifetime), and return the provider + bound port.
    /// </summary>
    private static async Task<(IServiceProvider sp, int port)> BuildAndStartAsync(
        CacheWithWildcards<string, string> upstream)
    {
        var services = new ServiceCollection();
        services.AddCacheTcpServer<string>(
            upstream,
            port: 0,                               // OS-assigned port
            serializer:    v => System.Text.Encoding.UTF8.GetBytes(v),
            deserializer:  b => System.Text.Encoding.UTF8.GetString(b),
            forwardPublish: true);

        var sp = services.BuildServiceProvider();

        // Start all hosted services (TcpCacheServerLifetime)
        foreach (var hs in sp.GetServices<IHostedService>())
            await hs.StartAsync(CancellationToken.None);

        var transport = sp.GetRequiredService<TcpServerTransport>();
        return (sp, transport.BoundPort);
    }

    private static RemoteCache<string> CreateClient(int port)
        => new RemoteCacheBuilder<string>()
            .WithTcpTransport("127.0.0.1", port)
            .WithUtf8Encoding()
            .Build();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_ReceivesUpstreamPublish()
    {
        var upstream = new CacheWithWildcards<string, string>();
        var (sp, port) = await BuildAndStartAsync(upstream);
        await using var client = CreateClient(port);

        await client.ConnectAsync();

        string? received = null;
        var sub = client.Subscribe("sensors/temp", async s => { received = s.Value; });
        await Task.Delay(100); // allow Subscribe message to reach server

        await upstream.PublishAsync("sensors/temp", "22.5");

        Assert.True(await WaitForAsync(() => received == "22.5"),
            "TCP client did not receive upstream publish");

        client.Unsubscribe(sub);
        await sp.GetRequiredService<RemoteCacheServer<string>>().StopAsync();
    }

    [Fact]
    public async Task Subscribe_Wildcard_ReceivesMultipleTopics()
    {
        var upstream = new CacheWithWildcards<string, string>();
        var (sp, port) = await BuildAndStartAsync(upstream);
        await using var client = CreateClient(port);

        await client.ConnectAsync();

        var keys = new List<string>();
        var sub = client.Subscribe("devices/+/state", async s =>
        {
            lock (keys) keys.Add(s.Key);
        });
        await Task.Delay(100);

        await upstream.PublishAsync("devices/a/state", "on");
        await upstream.PublishAsync("devices/b/state", "off");

        Assert.True(await WaitForAsync(() => { lock (keys) return keys.Count >= 2; }),
            $"Expected 2 wildcard messages, got {keys.Count}");

        client.Unsubscribe(sub);
        await sp.GetRequiredService<RemoteCacheServer<string>>().StopAsync();
    }

    [Fact]
    public async Task Publish_ForwardedToUpstream()
    {
        var upstream = new CacheWithWildcards<string, string>();
        var (sp, port) = await BuildAndStartAsync(upstream);
        await using var client = CreateClient(port);

        await client.ConnectAsync();
        await Task.Delay(100);

        string? upstreamReceived = null;
        upstream.Subscribe("control/cmd", async s => { upstreamReceived = s.Value; });

        await client.PublishAsync("control/cmd", "ACTIVATE");

        Assert.True(await WaitForAsync(() => upstreamReceived == "ACTIVATE"),
            "Client publish was not forwarded to upstream");

        await sp.GetRequiredService<RemoteCacheServer<string>>().StopAsync();
    }

    [Fact]
    public async Task MultipleClients_AllReceivePublish()
    {
        var upstream = new CacheWithWildcards<string, string>();
        var (sp, port) = await BuildAndStartAsync(upstream);

        await using var clientA = CreateClient(port);
        await using var clientB = CreateClient(port);

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        string? recA = null, recB = null;
        clientA.Subscribe("shared/topic", async s => { recA = s.Value; });
        clientB.Subscribe("shared/topic", async s => { recB = s.Value; });
        await Task.Delay(100);

        await upstream.PublishAsync("shared/topic", "broadcast");

        Assert.True(await WaitForAsync(() => recA == "broadcast" && recB == "broadcast"),
            $"Not all clients received the broadcast (A={recA}, B={recB})");

        await sp.GetRequiredService<RemoteCacheServer<string>>().StopAsync();
    }

    [Fact]
    public async Task HostedServiceLifetime_StartStop_Works()
    {
        var upstream = new CacheWithWildcards<string, string>();
        var (sp, port) = await BuildAndStartAsync(upstream);

        // Verify port is actually open by connecting
        await using var client = CreateClient(port);
        await client.ConnectAsync(); // would throw if server not listening

        // Stop via hosted service
        foreach (var hs in sp.GetServices<IHostedService>())
            await hs.StopAsync(CancellationToken.None);

        // Give the listener time to close
        await Task.Delay(100);

        // Subsequent connection attempt should fail (port closed)
        await using var client2 = CreateClient(port);
        await Assert.ThrowsAnyAsync<Exception>(() => client2.ConnectAsync());
    }
}
