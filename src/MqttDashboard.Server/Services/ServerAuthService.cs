using MqttDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of IAuthService for server-only deployments.
/// Reads auth status directly from IConfiguration and the current user principal,
/// eliminating the loopback HTTP call that caused "Failed to get auth status" warnings.
/// During SSR pre-render, HttpContext is available and carries the real user identity.
/// During Blazor Server circuits, AuthenticationStateProvider is resolved lazily via
/// IServiceProvider so that missing auth configuration does not break DI resolution.
/// Login in Blazor Server mode uses a one-time token flow because the circuit's HttpContext
/// is read-only (the WebSocket upgrade response has already been sent).
/// </summary>
public class ServerAuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly LoginTokenStore _tokenStore;
    private readonly ILogger<ServerAuthService>? _logger;

    public ServerAuthService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        LoginTokenStore tokenStore,
        ILogger<ServerAuthService>? logger = null)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<(bool isAdmin, bool authEnabled, bool readOnly)> GetStatusAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var readOnly = ReadOnlyHelper.IsReadOnly(_configuration, httpContext);
        if (readOnly)
            return (false, false, true);

        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled)
            return (true, false, false);

        // During SSR pre-render: HttpContext is available with the real user identity.
        if (httpContext != null && !httpContext.Response.HasStarted)
            return (httpContext.User.Identity?.IsAuthenticated == true, true, false);

        // During Blazor Server interactive circuit: HttpContext is the WebSocket upgrade
        // context (read-only). Use AuthenticationStateProvider instead.
        try
        {
            var asp = _serviceProvider.GetService<AuthenticationStateProvider>();
            if (asp != null)
            {
                var state = await asp.GetAuthenticationStateAsync();
                return (state.User.Identity?.IsAuthenticated == true, true, false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "AuthenticationStateProvider unavailable in circuit; defaulting to unauthenticated");
        }

        return (false, true, false);
    }

    public async Task<bool> LoginAsync(string password)
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrEmpty(hash)) return true;
        if (string.IsNullOrEmpty(password)) return false;

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }

        if (!valid) return false;

        var httpContext = _httpContextAccessor.HttpContext;

        // In .NET 8+, the Blazor Server circuit exposes the WebSocket upgrade HttpContext
        // (not null), but its response has already been committed — we cannot set cookies.
        // Callers should use GetLoginRedirectAsync() for the Blazor Server token flow.
        if (httpContext == null || httpContext.Response.HasStarted)
        {
            _logger?.LogDebug(
                "LoginAsync: cannot set auth cookie ({Reason}). Use GetLoginRedirectAsync for Blazor Server login.",
                httpContext == null ? "no HTTP context" : "response already started");
            return false;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return true;
    }

    /// <summary>
    /// Validates the password and, if correct, issues a short-lived one-use token.
    /// Returns a redirect URL the browser should navigate to (with forceLoad) so the server
    /// can set the auth cookie on a fresh HTTP response. Returns null if the password is wrong.
    /// </summary>
    public Task<string?> GetLoginRedirectAsync(string password)
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrEmpty(hash))
        {
            // Auth not configured — no login needed; callers should check GetStatusAsync first.
            return Task.FromResult<string?>(null);
        }

        if (string.IsNullOrEmpty(password))
            return Task.FromResult<string?>(null);

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return Task.FromResult<string?>(null); }

        if (!valid)
            return Task.FromResult<string?>(null);

        var token = _tokenStore.Issue();
        return Task.FromResult<string?>($"/api/auth/redeem/{token}");
    }

    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null && !httpContext.Response.HasStarted)
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        else
            _logger?.LogWarning("Logout cannot clear auth cookie: no active HTTP context or response already started.");
    }
}
