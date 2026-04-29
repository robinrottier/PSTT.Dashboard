using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PSTT.Dashboard.Server.Services;

namespace PSTT.Dashboard.Server.Filters;

/// <summary>
/// Action filter that allows access when the request carries a valid
/// <c>Authorization: Bearer {token}</c> header matching the configured API token,
/// OR when the user is authenticated as admin via cookie.
/// Used on dashboard data endpoints that remote installations need to access.
/// </summary>
public class ApiTokenAuthFilter : IActionFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiTokenAuthFilter> _logger;

    public ApiTokenAuthFilter(IConfiguration configuration, ILogger<ApiTokenAuthFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, context.HttpContext))
        {
            _logger.LogWarning("[ApiTokenAuthFilter] Rejected {Method} {Path} — server is in read-only mode",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            context.Result = new ObjectResult(new { error = "Dashboard is in read-only mode" })
                { StatusCode = 403 };
            return;
        }

        // Cookie-based admin auth
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogDebug("[ApiTokenAuthFilter] Allowed {Method} {Path} via cookie auth",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return;
        }

        // Bearer token auth — accepted even without admin password configured
        var storedToken = _configuration["RemoteAccess:ApiToken"];
        if (!string.IsNullOrEmpty(storedToken))
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                if (token == storedToken)
                {
                    _logger.LogDebug("[ApiTokenAuthFilter] Allowed {Method} {Path} via Bearer token",
                        context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                    return;
                }

                _logger.LogWarning("[ApiTokenAuthFilter] Bearer token mismatch for {Method} {Path} — expected prefix '{Expected}...', got '{Got}...'",
                    context.HttpContext.Request.Method, context.HttpContext.Request.Path,
                    storedToken[..Math.Min(8, storedToken.Length)],
                    token[..Math.Min(8, token.Length)]);
            }
            else
            {
                _logger.LogWarning("[ApiTokenAuthFilter] No/invalid Authorization header for {Method} {Path} — header: '{Header}'",
                    context.HttpContext.Request.Method, context.HttpContext.Request.Path, authHeader ?? "(none)");
            }
        }

        // No auth configured at all (no admin hash, no API token) — allow all (existing open-access behaviour)
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled && string.IsNullOrEmpty(storedToken))
        {
            _logger.LogDebug("[ApiTokenAuthFilter] Allowed {Method} {Path} — no auth configured (open access)",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return;
        }

        _logger.LogWarning("[ApiTokenAuthFilter] Unauthorized {Method} {Path}",
            context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
