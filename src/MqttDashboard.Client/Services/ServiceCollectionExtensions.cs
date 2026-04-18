using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MudBlazor;

namespace MqttDashboard.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMqttDashboardServices(this IServiceCollection services)
    {
        services.AddMudServices(config =>
        {
            // Configure Snackbar to auto-dismiss and not persist across navigation
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 4000; // 4 seconds
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            config.SnackbarConfiguration.MaxDisplayedSnackbars = 3;

            // Important: Clear snackbars on dispose to prevent persistence across navigation
            config.SnackbarConfiguration.ClearAfterNavigation = true;
        });

        services.AddScoped<ApplicationState>();
        services.AddScoped<LocalStorageService>();
        services.AddScoped<MqttInitializationService>();

        // DashboardService is only needed on client-side where HttpClient is available
        // Do not register here - it will be registered in client Program.cs

        // ⚠️ Do not re-enable: replacing IJSRuntime in DI breaks Blazor Server's internal
        // RemoteJSRuntime cast during circuit init → InvalidCastException on page load.
        // Fix is to guard JS interop call sites with RendererInfo.IsInteractive instead.
        // services.AddSafeJSRuntime();

        return services;
    }
}


