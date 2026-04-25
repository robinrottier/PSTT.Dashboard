using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
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

    public ApiTokenAuthFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (ReadOnlyHelper.IsReadOnly(_configuration, context.HttpContext))
        {
            context.Result = new ObjectResult(new { error = "Dashboard is in read-only mode" })
                { StatusCode = 403 };
            return;
        }

        // Cookie-based admin auth
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            return;

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
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ApiTokenAuthFilter] Token mismatch: expected '{storedToken.Substring(0, 8)}...', got '{token.Substring(0, Math.Min(8, token.Length))}...'");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ApiTokenAuthFilter] No Bearer token in Authorization header. Header value: '{authHeader}'");
            }
        }

        // No auth configured (no admin hash, no API token) — allow all (existing open-access behaviour)
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled && string.IsNullOrEmpty(storedToken))
            return;

        System.Diagnostics.Debug.WriteLine($"[ApiTokenAuthFilter] Authorization failed for {context.HttpContext.Request.Method} {context.HttpContext.Request.Path}");
        context.Result = new UnauthorizedObjectResult(new { error = "Authentication required" });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
