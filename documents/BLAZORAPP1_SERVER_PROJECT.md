# MqttDashboard.Server Project - Server-Side Library

## Overview

Created a new `MqttDashboard.Server` class library to contain server-side only code, properly separating client-side and server-side concerns in the Blazor solution architecture.

## Architecture

### Project Structure

```
MqttDashboard/
├── MqttDashboard/                          # Client-side library (WebAssembly compatible)
│   ├── Pages/                           # Blazor pages and components
│   ├── Services/                        # Client-side services
│   │   ├── ApplicationState.cs          # Shared application state
│   │   ├── SignalRService.cs            # SignalR client connection
│   │   ├── LocalStorageService.cs       # Browser local storage
│   │   └── ServiceCollectionExtensions.cs  # Client service registration
│   └── Models/                          # Shared data models
│
├── MqttDashboard.Server/                   # Server-side library (NEW)
│   ├── Hubs/                            # SignalR Hubs
│   │   ├── MqttDataHub.cs              # SignalR Hub for MQTT
│   │   └── MqttTopicSubscriptionManager.cs  # Topic subscription manager
│   ├── Services/                        # Server-side services
│   │   └── MqttClientService.cs        # MQTT broker connection
│   └── Extensions/                      # Service registration extensions
│       └── ServiceCollectionExtensions.cs  # Server service registration
│
└── MqttDashboard.WebAppWasmOnly/               # Server application
    ├── MqttDashboard.WebAppWasmOnly/           # Server project
    │   └── Program.cs                   # Server startup
    └── MqttDashboard.WebAppWasmOnly.Client/    # WebAssembly client project
        └── Program.cs                   # Client startup
```

## What Was Moved

### From MqttDashboard → MqttDashboard.Server

**Files Moved:**
- `Hubs\MqttDataHub.cs` - SignalR Hub
- `Hubs\MqttTopicSubscriptionManager.cs` - Subscription manager

**Namespace Changed:**
- Old: `MqttDashboard.Hubs`
- New: `MqttDashboard.Server.Hubs`

### From MqttDashboard.WebAppWasmOnly → MqttDashboard.Server

**Files Moved:**
- `Services\MqttClientService.cs` - MQTT client background service

**Namespace Changed:**
- Old: `MqttDashboard.WebAppWasmOnly.Services`
- New: `MqttDashboard.Server.Services`

## MqttDashboard.Server Project Configuration

### Project File
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="MQTTnet" Version="5.1.0.1559" />
  </ItemGroup>
</Project>
```

### Dependencies
- **Framework**: Microsoft.AspNetCore.App (Full ASP.NET Core)
- **Package**: MQTTnet (MQTT client library)

## MqttDashboard Project Configuration (Updated)

### Project File
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Include="browser" />  <!-- WebAssembly compatible -->
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.3" />
    <PackageReference Include="MudBlazor" Version="8.15.0" />
  </ItemGroup>
</Project>
```

### What Was Removed from MqttDashboard
- ❌ `Microsoft.AspNetCore.SignalR.Core` package (server-side)
- ❌ `MQTTnet` package (server-side)
- ❌ `SERVER_SIDE` compilation constant (no longer needed)
- ❌ Server-side Hub and services

## Service Registration

### Client-Side Services (MqttDashboard)

**Extension Method:**
```csharp
// MqttDashboard\Services\ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMqttDashboardServices(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddScoped<ApplicationState>();
        services.AddScoped<LocalStorageService>();
        services.AddScoped<SignalRService>();
        return services;
    }
}
```

**Usage in WebAssembly Client:**
```csharp
// MqttDashboard.WebAppWasmOnly.Client\Program.cs
builder.Services.AddMqttDashboardServices();
```

### Server-Side Services (MqttDashboard.Server)

**Extension Method:**
```csharp
// MqttDashboard.Server\Extensions\ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMqttDashboardServerServices(this IServiceCollection services)
    {
        services.AddSingleton<MqttTopicSubscriptionManager>();
        services.AddHostedService<MqttClientService>();
        return services;
    }
}
```

**Usage in Server:**
```csharp
// MqttDashboard.WebAppWasmOnly\Program.cs
using MqttDashboard.Services;
using MqttDashboard.Server.Hubs;
using MqttDashboard.Server.Extensions;

builder.Services.AddMqttDashboardServices();        // Client services
builder.Services.AddMqttDashboardServerServices();  // Server services
builder.Services.AddSignalR();

// ...

app.MapHub<MqttDataHub>("/mqttdatahub");
```

## Component Responsibilities

### MqttDashboard (Client Library)
✅ **Can Contain:**
- Razor components (.razor files)
- Client-side services (SignalR client, LocalStorage)
- Shared state management (ApplicationState)
- Data models shared between client and server
- UI logic and event handlers

❌ **Cannot Contain:**
- SignalR Hubs
- Background services (IHostedService)
- MQTT client connections
- Server-side database access
- Server-only dependencies

### MqttDashboard.Server (Server Library)
✅ **Can Contain:**
- SignalR Hubs
- Background services (IHostedService)
- MQTT client connections
- Server-side business logic
- Database access services
- External API integrations

❌ **Cannot Contain:**
- Razor components (UI)
- Browser-specific code (LocalStorage, JS interop)
- WebAssembly-specific dependencies

### MqttDashboard.WebAppWasmOnly (Server Application)
✅ **Can Contain:**
- Server startup configuration
- Middleware configuration
- Application-specific settings
- Hosting configuration

❌ **Should Not Contain:**
- Reusable server-side services (move to MqttDashboard.Server)
- Reusable client components (move to MqttDashboard)

## Benefits of This Architecture

1. **Clear Separation of Concerns**
   - Client code clearly separated from server code
   - No mixing of incompatible dependencies

2. **Reusability**
   - `MqttDashboard.Server` can be referenced by multiple server projects
   - Server logic is portable across different hosting scenarios

3. **Maintainability**
   - Easier to understand what code runs where
   - Fewer build issues with platform compatibility

4. **Type Safety**
   - Shared types can still be in `MqttDashboard`
   - Server and client can reference common models

5. **Testability**
   - Server services can be tested independently
   - No need to mock browser-specific APIs in server tests

## Migration Guide for Other Projects

To adopt this architecture in other projects:

1. **Create MqttDashboard.Server project:**
   ```bash
   dotnet new classlib -n MqttDashboard.Server -f net10.0
   ```

2. **Add ASP.NET Core framework reference:**
   ```xml
   <FrameworkReference Include="Microsoft.AspNetCore.App" />
   ```

3. **Move server-side code:**
   - SignalR Hubs → `MqttDashboard.Server\Hubs\`
   - Background services → `MqttDashboard.Server\Services\`
   - Server-only logic → `MqttDashboard.Server\`

4. **Update namespaces:**
   ```csharp
   // Old
   namespace MqttDashboard.Hubs;
   
   // New
   namespace MqttDashboard.Server.Hubs;
   ```

5. **Add project reference:**
   ```xml
   <ProjectReference Include="..\..\MqttDashboard.Server\MqttDashboard.Server.csproj" />
   ```

6. **Update using statements:**
   ```csharp
   using MqttDashboard.Server.Hubs;
   using MqttDashboard.Server.Services;
   using MqttDashboard.Server.Extensions;
   ```

7. **Register services:**
   ```csharp
   builder.Services.AddMqttDashboardServerServices();
   ```

## Build Status

✅ **Build Successful**

All projects compile correctly with the new architecture.

## Testing Checklist

- [x] MqttDashboard builds successfully
- [x] MqttDashboard.Server builds successfully
- [x] MqttDashboard.WebAppWasmOnly server builds successfully
- [x] MqttDashboard.WebAppWasmOnly client builds successfully
- [ ] Server can access MqttDataHub
- [ ] Client can connect to SignalR hub
- [ ] MQTT messages flow correctly
- [ ] Multiple clients can subscribe to topics
- [ ] Topic reference counting works

## Future Enhancements

1. **Additional Server Services**
   - Create database access services in MqttDashboard.Server
   - Add authentication/authorization services
   - Create API integration services

2. **Shared Contracts**
   - Consider `MqttDashboard.Contracts` for shared DTOs/interfaces
   - Versioning strategy for contracts

3. **Multiple Server Implementations**
   - Azure Functions hosting
   - Docker containerization
   - Different cloud providers

4. **Testing**
   - Unit tests for MqttDashboard.Server services
   - Integration tests for SignalR hubs
   - MQTT mock/simulator for testing
