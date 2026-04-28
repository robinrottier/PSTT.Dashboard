using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MudBlazor;
using PSTT.Data;

namespace PSTT.Dashboard.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
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

        // ApplicationState is scoped: each circuit/WASM instance gets its own.
        // ICache<string,string> is resolved from DI — either the scoped circuit cache (Blazor Server)
        // or the singleton RemoteCache (WASM). Falls back to a plain Cache if none is registered.
        services.AddScoped<ApplicationState>(sp =>
            new ApplicationState(
                sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>(),
                sp.GetService<ICache<string, string>>()));

        // Replace Blazor's default no-op IErrorBoundaryLogger with our diagnostic logger
        // so all <ErrorBoundary>-caught exceptions appear in Serilog with full stack traces
        // and trigger Debugger.Break() in DEBUG builds when a debugger is attached.
        services.AddScoped<IErrorBoundaryLogger, DiagnosticErrorLogger>();

        services.AddScoped<LocalStorageService>();
        services.AddScoped<MqttInitializationService>();

        return services;
    }
}


