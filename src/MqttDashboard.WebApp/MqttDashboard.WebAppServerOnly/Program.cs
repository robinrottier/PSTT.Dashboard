using MqttDashboard.WebAppServerOnly.Components;
using MqttDashboard.Server.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddMqttDashboardSerilog();
    builder.AddMqttDashboardDataDirectory();
    builder.AddMqttDashboard(BlazorRenderMode.InteractiveServer);

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.UseMqttDashboard<App>(BlazorRenderMode.InteractiveServer);
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

// Expose Program for WebApplicationFactory<Program> in integration/Playwright tests.
public partial class Program { }
