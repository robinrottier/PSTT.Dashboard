namespace MqttDashboard.Services;

/// <summary>
/// Server-side abstraction that tells client services whether there is an active HTTP request
/// (SSR pre-render) or not (Blazor Server interactive circuit).
/// Not registered in WASM — will be null when injected optionally.
/// </summary>
public interface IServerContextAccessor
{
    /// <summary>True when an HTTP request is currently being processed (SSR phase, not circuit).</summary>
    bool IsInServerHttpRequest { get; }

    /// <summary>
    /// Stores the current request's local port in <see cref="RenderModeOptions"/> for later
    /// use by Blazor Server circuits where the HTTP context is no longer accessible.
    /// </summary>
    void CacheLocalPort();
}
