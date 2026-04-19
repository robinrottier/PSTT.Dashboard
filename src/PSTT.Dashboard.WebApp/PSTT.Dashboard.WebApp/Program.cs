using PSTT.Dashboard.WebApp.Components;
using PSTT.Dashboard.Server.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddMqttDashboardSerilog();
    builder.AddMqttDashboardDataDirectory();

    var renderModeConfig = builder.Configuration["RenderMode"] ?? "Auto";
    var renderMode = renderModeConfig.ToLowerInvariant() switch
    {
        "webassembly" => BlazorRenderMode.InteractiveWebAssembly,
        "server"      => BlazorRenderMode.InteractiveServer,
        _             => BlazorRenderMode.InteractiveAuto
    };

    builder.AddMqttDashboard(renderMode);

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.UseMqttDashboard<App>(renderMode);
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
