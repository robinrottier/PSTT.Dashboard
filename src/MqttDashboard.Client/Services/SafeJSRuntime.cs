using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MqttDashboard.Services;

/// <summary>
/// Wraps an <see cref="IJSRuntime"/> to silently absorb two failure cases that would
/// otherwise crash the Blazor Server circuit:
/// <list type="bullet">
///   <item><see cref="InvalidOperationException"/> — thrown when a component calls JS
///   interop during server-side prerendering or before the circuit is fully
///   interactive.</item>
///   <item><see cref="JSDisconnectedException"/> — thrown when the SignalR connection
///   has already been torn down but an in-flight JS call still tries to respond.</item>
/// </list>
/// <para>
/// <b>Usage:</b> wrap a known <see cref="IJSRuntime"/> instance in specific components
/// that need guarded calls. Do <b>not</b> register via
/// <see cref="SafeJSRuntimeExtensions.AddSafeJSRuntime"/> — see the warning on that
/// method. The correct fix for premature JS interop during prerendering is to guard
/// call sites with <c>RendererInfo.IsInteractive</c> or move them into
/// <c>OnAfterRenderAsync</c>.
/// </para>
/// </summary>
public sealed class SafeJSRuntime : IJSRuntime
{
    private readonly IJSRuntime _inner;
    private readonly ILogger<SafeJSRuntime> _log;

    public SafeJSRuntime(IJSRuntime inner, ILogger<SafeJSRuntime> log)
    {
        _inner = inner;
        _log = log;
    }

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        try
        {
            return await _inner.InvokeAsync<TValue>(identifier, args);
        }
        catch (InvalidOperationException ex)
        {
            _log.LogDebug(ex, "JS interop '{Identifier}' skipped — circuit not ready", identifier);
            return default!;
        }
        catch (JSDisconnectedException)
        {
            return default!;
        }
    }

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        try
        {
            return await _inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
        catch (InvalidOperationException ex)
        {
            _log.LogDebug(ex, "JS interop '{Identifier}' skipped — circuit not ready", identifier);
            return default!;
        }
        catch (JSDisconnectedException)
        {
            return default!;
        }
        catch (OperationCanceledException)
        {
            return default!;
        }
    }
}

public static class SafeJSRuntimeExtensions
{
    /// <summary>
    /// ⚠️ DO NOT CALL — retained for reference only.
    /// <para>
    /// Replacing <see cref="IJSRuntime"/> in the DI container is incompatible with
    /// Blazor Server's internal architecture. The circuit infrastructure casts the
    /// resolved <c>IJSRuntime</c> directly to its internal <c>RemoteJSRuntime</c>
    /// concrete type. Substituting <see cref="SafeJSRuntime"/> breaks that cast,
    /// causing <see cref="InvalidCastException"/> during circuit initialisation and
    /// preventing the page from loading.
    /// </para>
    /// <para>
    /// Fix premature JS interop during prerendering by guarding call sites with
    /// <c>RendererInfo.IsInteractive</c> or moving JS calls into
    /// <c>OnAfterRenderAsync</c>.
    /// </para>
    /// </summary>
    [Obsolete("Do not use — breaks Blazor Server circuit initialisation. See XML doc for details.", error: false)]
    public static IServiceCollection AddSafeJSRuntime(this IServiceCollection services)
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(IJSRuntime));
        if (original is null) return services;

        services.Remove(original);

        services.Add(new ServiceDescriptor(
            typeof(IJSRuntime),
            sp =>
            {
                var inner = original switch
                {
                    { ImplementationFactory: not null } =>
                        (IJSRuntime)original.ImplementationFactory(sp),
                    { ImplementationType: not null } =>
                        (IJSRuntime)ActivatorUtilities.CreateInstance(sp, original.ImplementationType),
                    { ImplementationInstance: not null } =>
                        (IJSRuntime)original.ImplementationInstance,
                    _ => throw new InvalidOperationException(
                        "Unrecognised IJSRuntime service descriptor — cannot wrap with SafeJSRuntime.")
                };
                return new SafeJSRuntime(inner, sp.GetRequiredService<ILogger<SafeJSRuntime>>());
            },
            original.Lifetime));

        return services;
    }
}
