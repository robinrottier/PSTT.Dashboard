using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace PSTT.Dashboard.Services;

/// <summary>
/// Replaces the default Blazor IErrorBoundaryLogger.
/// Logs unhandled component exceptions with full stack traces via the standard ILogger
/// (which routes to Serilog on the server side).
/// In DEBUG builds, breaks into the debugger when one is attached — set a breakpoint
/// on <see cref="LogErrorAsync"/> to intercept all component exceptions in one place.
/// </summary>
public class DiagnosticErrorLogger(ILogger<DiagnosticErrorLogger> logger) : IErrorBoundaryLogger
{
    public ValueTask LogErrorAsync(Exception exception)
    {
        logger.LogError(exception, "Unhandled exception in Blazor component tree");

#if DEBUG
        if (System.Diagnostics.Debugger.IsAttached)
            System.Diagnostics.Debugger.Break();
#endif

        return ValueTask.CompletedTask;
    }
}
