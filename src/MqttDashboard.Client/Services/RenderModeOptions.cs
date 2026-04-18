namespace MqttDashboard.Services;

/// <summary>
/// Singleton options describing the render mode and caching the loopback port for
/// server-to-self HTTP calls in Blazor Server circuits where IHttpContextAccessor
/// is unavailable (async context differs from the HTTP request context).
/// </summary>
public class RenderModeOptions
{
    /// <summary>True when WASM is available as the client runtime (InteractiveAuto or InteractiveWebAssembly).</summary>
    public bool IsWasmCapable { get; init; }

    private int _loopbackPort;

    /// <summary>The local TCP port Kestrel is listening on. Set by the first incoming HTTP request.</summary>
    public int LoopbackPort => _loopbackPort;

    /// <summary>
    /// Caches the loopback port if not already set. Thread-safe; safe to call concurrently.
    /// </summary>
    public void CacheLoopbackPort(int port) =>
        System.Threading.Interlocked.CompareExchange(ref _loopbackPort, port, 0);
}
