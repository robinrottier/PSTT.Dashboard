using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MqttDashboard.Data;
using MqttDashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMqttDashboardServices();

// Add HttpClient for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add SignalRDataServer (HTTP WebSocket client, runs in browser)
builder.Services.AddScoped<SignalRDataServer>();
builder.Services.AddScoped<IDataServer>(sp => sp.GetRequiredService<SignalRDataServer>());

// Add DashboardService (needs HttpClient)
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Add AuthService (needs HttpClient)
builder.Services.AddScoped<IAuthService, AuthService>();

await builder.Build().RunAsync();

