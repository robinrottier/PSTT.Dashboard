using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using MqttDashboard.Server.Services;

namespace MqttDashboard.Server.Filters;

/// <summary>
/// Action filter that returns 403 in read-only mode, or 401 for unauthenticated write
/// requests when auth is configured.
/// </summary>
public class RequireAdminFilter : IActionFilter
{
    private readonly IConfiguration _configuration;

    public RequireAdminFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Read-only mode (global flag or per-port): all write operations are blocked for everyone
        if (ReadOnlyHelper.IsReadOnly(_configuration, context.HttpContext))
        {
            context.Result = new ObjectResult(new { error = "Dashboard is in read-only mode" })
                { StatusCode = 403 };
            return;
        }

        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled) return;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            context.Result = new UnauthorizedObjectResult(new { error = "Admin authentication required" });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
