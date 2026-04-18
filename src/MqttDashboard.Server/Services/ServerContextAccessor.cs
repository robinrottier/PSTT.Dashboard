using MqttDashboard.Services;
using Microsoft.AspNetCore.Http;

namespace MqttDashboard.Server.Services;

/// <summary>
/// Provides server-side HTTP context information to client services without exposing
/// IHttpContextAccessor (unavailable in the Razor class library SDK) directly.
/// </summary>
public class ServerContextAccessor : IServerContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RenderModeOptions _renderModeOptions;

    public ServerContextAccessor(IHttpContextAccessor httpContextAccessor, RenderModeOptions renderModeOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _renderModeOptions = renderModeOptions;
    }

    public bool IsInServerHttpRequest => _httpContextAccessor.HttpContext != null;

    public void CacheLocalPort()
    {
        var port = _httpContextAccessor.HttpContext?.Connection.LocalPort ?? 0;
        if (port > 0)
            _renderModeOptions.CacheLoopbackPort(port);
    }
}
