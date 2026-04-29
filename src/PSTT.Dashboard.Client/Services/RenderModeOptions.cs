namespace PSTT.Dashboard.Services;

/// <summary>
/// Singleton options describing the render mode and caching the loopback address for
/// server-to-self HTTP calls in Blazor Server circuits where IHttpContextAccessor
/// is unavailable (async context differs from the HTTP request context).
/// </summary>
public class RenderModeOptions
{
    /// <summary>True when WASM is available as the client runtime (InteractiveAuto or InteractiveWebAssembly).</summary>
    public bool IsWasmCapable { get; init; }

    private Uri? _loopbackAddress;

    /// <summary>The HTTP address (including scheme, host, and port) that Kestrel is listening on. Set by the first incoming HTTP request or startup callback.</summary>
    public Uri? LoopbackAddress => _loopbackAddress;

    /// <summary>
    /// Caches the loopback address if not already set. Thread-safe; safe to call concurrently.
    /// </summary>
    public void CacheLoopbackAddress(Uri address)
    {
        if (address != null && _loopbackAddress == null)
            System.Threading.Interlocked.CompareExchange(ref _loopbackAddress, address, null);
    }

    /// <summary>The local TCP port Kestrel is listening on. Legacy property for backward compatibility.</summary>
    public int LoopbackPort => _loopbackAddress?.Port ?? 0;

    /// <summary>
    /// Caches the loopback port if not already set. Thread-safe; safe to call concurrently.
    /// Legacy method - prefer CacheLoopbackAddress for more accurate address resolution.
    /// </summary>
    public void CacheLoopbackPort(int port)
    {
        if (port > 0 && _loopbackAddress == null)
            CacheLoopbackAddress(new Uri($"http://localhost:{port}/"));
    }
}
