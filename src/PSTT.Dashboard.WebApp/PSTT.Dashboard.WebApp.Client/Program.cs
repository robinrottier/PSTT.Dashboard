using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PSTT.Data;
using PSTT.Remote;
using PSTT.Dashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddDashboardServices();

// Add HttpClient for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Build RemoteCache<string> and register as ICache<string,string>.
// ConnectAsync is called by MqttInitializationService after the component mounts.
var hubUrl = builder.HostEnvironment.BaseAddress.TrimEnd('/') + "/cachehub";
var remoteCache = new RemoteCacheBuilder<string>()
    .WithSignalRTransport(hubUrl)
    .WithUtf8Encoding()
    .Build();
builder.Services.AddSingleton<ICache<string,string>>(remoteCache);
builder.Services.AddSingleton(remoteCache);

// Add DashboardService (needs HttpClient)
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Add AuthService (needs HttpClient)
builder.Services.AddScoped<IAuthService, AuthService>();

// Add HTTP-backed service implementations for WASM (server-side uses direct injection)
builder.Services.AddScoped<IRemoteRepoService, HttpRemoteRepoService>();
builder.Services.AddScoped<IAppSettingsService, HttpAppSettingsService>();
builder.Services.AddScoped<ISetupService, HttpSetupService>();
builder.Services.AddScoped<IUpdateStatusService, HttpUpdateStatusService>();
builder.Services.AddScoped<IStartupSettingsService, HttpStartupSettingsService>();
builder.Services.AddScoped<IRemoteAccessService, HttpRemoteAccessService>();

await builder.Build().RunAsync();

