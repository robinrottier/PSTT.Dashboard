# Moving SignalR Hub to Shared Library

## Summary

Successfully moved the SignalR Hub and MQTT Topic Subscription Manager from the server-specific project (`MqttDashboard.WebAppWasmOnly`) to the shared `MqttDashboard` library, making them reusable across multiple server projects.

## Changes Made

### 1. Updated MqttDashboard.csproj

**Removed:**
- `<SupportedPlatform Include="browser" />` restriction
- `Microsoft.AspNetCore.Components` package (redundant)

**Added:**
- `Microsoft.AspNetCore.SignalR.Core` (v1.1.0) - Provides Hub base class
- `DefineConstants` with `SERVER_SIDE` for conditional compilation

**Final Configuration:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup>
    <DefineConstants>$(DefineConstants);SERVER_SIDE</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Core" Version="1.1.0" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.3" />
    <PackageReference Include="MudBlazor" Version="8.15.0" />
  </ItemGroup>
</Project>
```

### 2. Moved Files to MqttDashboard\Hubs\

**From:** `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\Hubs\`  
**To:** `MqttDashboard\Hubs\`

Files moved:
- `MqttDataHub.cs` - SignalR Hub for MQTT topic subscriptions
- `MqttTopicSubscriptionManager.cs` - Reference-counted topic subscription manager

**Namespace Changes:**
- Old: `MqttDashboard.WebAppWasmOnly.Hubs`
- New: `MqttDashboard.Hubs`

### 3. Updated Server Project References

**File:** `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\Program.cs`

```csharp
// Before
using MqttDashboard.WebAppWasmOnly.Hubs;
using MqttDashboard.WebAppWasmOnly.Services;

// After
using MqttDashboard.Hubs;
using MqttDashboard.WebAppWasmOnly.Services;
```

**File:** `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\Services\MqttClientService.cs`

```csharp
// Before
using MqttDashboard.WebAppWasmOnly.Hubs;

// After
using MqttDashboard.Hubs;
```

### 4. Removed Duplicate Files

Deleted from `MqttDashboard.WebAppWasmOnly\MqttDashboard.WebAppWasmOnly\`:
- `Hubs\MqttDataHub.cs`
- `Services\MqttTopicSubscriptionManager.cs`

## Architecture

### MqttDashboard - Shared Library

Now contains both client-side and server-side components:

**Client-Side:**
- Blazor components (`.razor` files)
- `SignalRService` (SignalR client)
- `ApplicationState`
- `LocalStorageService`

**Server-Side:**
- `MqttDataHub` (SignalR Hub)
- `MqttTopicSubscriptionManager`

### MqttDashboard.WebAppWasmOnly - Server Project

**Retains:**
- `MqttClientService` (BackgroundService for MQTT broker connection)
- Server-specific configuration and startup

**References:**
- MqttDashboard library for Hub and shared components

## Benefits

1. **Code Reusability** - Hub can be used by multiple server projects
2. **Single Source of Truth** - Hub logic in one location
3. **Easier Maintenance** - Update Hub in one place
4. **Consistency** - All servers use the same Hub implementation
5. **Type Safety** - Shared types between client and server

## Compatibility

✅ **WebAssembly Client** - Can still reference MqttDashboard for components and client services  
✅ **Server Projects** - Can reference MqttDashboard for Hub and shared components  
✅ **SignalR Client** - Uses `Microsoft.AspNetCore.SignalR.Client` (works in WASM)  
✅ **SignalR Hub** - Uses `Microsoft.AspNetCore.SignalR.Core` (works on server)

## Package Strategy

- **SignalR.Client** (10.0.3) - For WebAssembly clients to connect to hubs
- **SignalR.Core** (1.1.0) - For Hub base class (server-side)
- No framework reference needed - Package approach works cross-platform

## Important Notes

⚠️ **Do Not Add** `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to MqttDashboard  
- This causes "browser-wasm" runtime pack errors when WebAssembly projects reference it
- Use package references instead

⚠️ **SERVER_SIDE Constant**  
- Defined for potential conditional compilation
- Can be used to exclude server-only code from client builds if needed

## Testing Checklist

- [x] Build successful
- [ ] Server project can inject MqttDataHub
- [ ] SignalR Hub works correctly
- [ ] MQTT topic subscriptions work
- [ ] WebAssembly client can connect to Hub
- [ ] Multiple clients can subscribe to topics
- [ ] Reference counting works correctly

## Future Considerations

1. **Multi-Targeting** - Could target `net10.0` and `net10.0-browser` separately
2. **Conditional Compilation** - Use `#if SERVER_SIDE` if needed
3. **Separate Projects** - Consider splitting into MqttDashboard.Client and MqttDashboard.Server if complexity grows
