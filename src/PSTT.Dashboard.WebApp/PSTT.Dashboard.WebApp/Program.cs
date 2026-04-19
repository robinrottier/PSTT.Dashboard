using PSTT.Dashboard.WebApp.Components;
using PSTT.Dashboard.Server.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddDashboardSerilog();
    builder.AddDashboardDataDirectory();

    var renderModeConfig = builder.Configuration["RenderMode"] ?? "Auto";
    var renderMode = renderModeConfig.ToLowerInvariant() switch
    {
        "webassembly" => BlazorRenderMode.InteractiveWebAssembly,
        "server"      => BlazorRenderMode.InteractiveServer,
        _             => BlazorRenderMode.InteractiveAuto
    };

    builder.AddDashboard(renderMode);

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.UseDashboard<App>(renderMode);
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
