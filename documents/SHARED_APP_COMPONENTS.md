# Shared App.razor and Error.razor Components

## Overview

Successfully moved `App.razor` and `Error.razor` from individual server projects to the shared **MqttDashboard** library, allowing both MqttDashboard.WebAppWasmOnly and MqttDashboard.WebAppServerOnly to reuse the same root components.

## Architecture

### Shared Components in MqttDashboard

```
MqttDashboard/
├── App/
│   ├── App.razor                    # NEW: Shared root component
│   ├── AppHeadContent.razor         # Shared head content (existing)
│   ├── AppBodyContent.razor         # Shared body scripts (existing)
│   └── Routes.razor                 # Shared routing (existing)
└── Pages/
    ├── Error.razor                  # NEW: Shared error page
    ├── Home.razor
    ├── MqttData.razor
    └── ... other pages
```

### Usage in Server Projects

**MqttDashboard.WebAppWasmOnly (WebAssembly):**
```razor
@using MqttDashboard.App

<MqttDashboard.App.App RenderMode="InteractiveWebAssembly">
    <AdditionalStylesheets>
        <link rel="stylesheet" href="MqttDashboard.WebAppWasmOnly.styles.css" />
        <link rel="stylesheet" href="app.css" />
    </AdditionalStylesheets>
</MqttDashboard.App.App>
```

**MqttDashboard.WebAppServerOnly (Server-side Blazor):**
```razor
@using MqttDashboard.App
@using MqttDashboard.WebAppServerOnly.Components.Layout

<MqttDashboard.App.App RenderMode="InteractiveServer">
    <AdditionalStylesheets>
        <link rel="stylesheet" href="app.css" />
        <link rel="stylesheet" href="MqttDashboard.WebAppServerOnly.styles.css" />
    </AdditionalStylesheets>
</MqttDashboard.App.App>

@* Server-specific reconnection modal *@
<ReconnectModal />
```

## App.razor Component Design

### Features

1. **Parameterized Render Mode**
   - Accepts `RenderMode` parameter
   - Each project specifies its render mode (WebAssembly, Server, etc.)

2. **Flexible Stylesheet Injection**
   - `AdditionalStylesheets` parameter allows project-specific CSS
   - Scoped styles are automatically included

3. **Shared Structure**
   - Common HTML structure
   - Shared head content (AppHeadContent)
   - Shared body scripts (AppBodyContent)
   - Shared routing (Routes)

### Implementation

```razor
@using MqttDashboard.App
@using Microsoft.AspNetCore.Components.Web

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <ResourcePreloader />

    <AppHeadContent />

    @* Project-specific styles injected here *@
    @RenderStylesheets()

    <ImportMap />
    <link rel="icon" type="image/png" href="favicon.png" />
    <HeadOutlet @rendermode="@RenderMode" />
</head>
<body>
    <Routes @rendermode="@RenderMode" />
    <AppBodyContent />
</body>
</html>

@code {
    [Parameter]
    public IComponentRenderMode? RenderMode { get; set; }

    [Parameter]
    public RenderFragment? AdditionalStylesheets { get; set; }

    private RenderFragment RenderStylesheets() => builder =>
    {
        if (AdditionalStylesheets != null)
        {
            AdditionalStylesheets(builder);
        }
    };
}
```

## Error.razor Component Design

### Features

1. **Cross-Platform Compatible**
   - Works in both WebAssembly and Server scenarios
   - Uses `Activity.Current?.Id` instead of server-specific `HttpContext`

2. **MudBlazor Styled**
   - Modern, consistent error page design
   - Uses MudBlazor components for better UX

3. **Request ID Display**
   - Shows diagnostic request ID when available
   - Helps with debugging and support

### Implementation

```razor
@page "/Error"
@using System.Diagnostics

<PageTitle>Error</PageTitle>

<MudContainer Class="mt-4">
    <MudPaper Elevation="3" Class="pa-4">
        <MudText Typo="Typo.h3" Color="Color.Error">
            <MudIcon Icon="@Icons.Material.Filled.Error" /> Error
        </MudText>
        
        <MudText Typo="Typo.h5" Color="Color.Error">
            An error occurred while processing your request.
        </MudText>

        @if (ShowRequestId)
        {
            <MudAlert Severity="Severity.Info">
                <strong>Request ID:</strong> <code>@RequestId</code>
            </MudAlert>
        }
        
        @* Development mode information *@
    </MudPaper>
</MudContainer>

@code {
    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized()
    {
        RequestId = Activity.Current?.Id;
    }
}
```

## Benefits

### 1. Code Reusability
- ✅ Single source of truth for app structure
- ✅ Shared error handling UI
- ✅ Consistent user experience across projects

### 2. Maintainability
- ✅ Update app structure in one place
- ✅ Error page changes apply to all projects
- ✅ Easier to maintain consistency

### 3. Flexibility
- ✅ Each project can customize stylesheets
- ✅ Each project specifies its own render mode
- ✅ Server-specific features can be added (like ReconnectModal)

### 4. DRY Principle
- ✅ Don't Repeat Yourself
- ✅ Reduces duplicate code
- ✅ Fewer files to maintain

## Project-Specific Customizations

### WebAssembly Project
```razor
<MqttDashboard.App.App RenderMode="InteractiveWebAssembly">
    <AdditionalStylesheets>
        <link rel="stylesheet" href="MqttDashboard.WebAppWasmOnly.styles.css" />
        <link rel="stylesheet" href="app.css" />
    </AdditionalStylesheets>
</MqttDashboard.App.App>
```

**Characteristics:**
- Uses WebAssembly render mode
- Includes project-specific scoped styles
- No server-specific components

### Server Project
```razor
<MqttDashboard.App.App RenderMode="InteractiveServer">
    <AdditionalStylesheets>
        <link rel="stylesheet" href="app.css" />
        <link rel="stylesheet" href="MqttDashboard.WebAppServerOnly.styles.css" />
    </AdditionalStylesheets>
</MqttDashboard.App.App>

<ReconnectModal />
```

**Characteristics:**
- Uses Server render mode
- Includes project-specific scoped styles
- Adds ReconnectModal for SignalR reconnection handling

## Migration Steps Taken

1. **Created Shared App.razor**
   - Added to `MqttDashboard\App\App.razor`
   - Made render mode parameterized
   - Added flexible stylesheet injection

2. **Created Shared Error.razor**
   - Added to `MqttDashboard\Pages\Error.razor`
   - Removed server-specific HttpContext dependency
   - Styled with MudBlazor components

3. **Updated MqttDashboard.WebAppWasmOnly**
   - Replaced full App.razor with wrapper
   - Removed local Error.razor
   - Specified WebAssembly render mode

4. **Updated MqttDashboard.WebAppServerOnly**
   - Replaced full App.razor with wrapper
   - Removed local Error.razor
   - Specified Server render mode
   - Kept ReconnectModal for server-specific functionality

5. **Removed Duplicate Files**
   - Deleted `MqttDashboard.WebAppWasmOnly\Components\Pages\Error.razor`
   - Deleted `MqttDashboard.WebAppServerOnly\Components\Pages\Error.razor`

## Extending to Other Projects

To use the shared components in other projects:

```razor
@* In your project's App.razor *@
@using MqttDashboard.App

<MqttDashboard.App.App RenderMode="YourRenderMode">
    <AdditionalStylesheets>
        <link rel="stylesheet" href="your-project.styles.css" />
        <link rel="stylesheet" href="your-custom.css" />
    </AdditionalStylesheets>
</MqttDashboard.App.App>

@* Add any project-specific components here *@
<YourCustomComponent />
```

## What Can Be Shared

✅ **Good candidates for shared components:**
- App.razor (root component)
- Error.razor (error page)
- NotFound.razor (404 page)
- Layout components (if truly universal)
- Page components
- Reusable UI components

❌ **What should NOT be shared:**
- Server-specific error handling (HttpContext usage)
- Platform-specific features (ReconnectModal for Server only)
- Project-specific routing logic
- Environment-specific configuration

## Testing Checklist

- [x] Build successful
- [ ] MqttDashboard.WebAppWasmOnly loads correctly
- [ ] MqttDashboard.WebAppServerOnly loads correctly
- [ ] Error page displays in WebAssembly project
- [ ] Error page displays in Server project
- [ ] Request ID shows when error occurs
- [ ] Project-specific styles load correctly
- [ ] ReconnectModal works in Server project
- [ ] Routing works in both projects

## Future Enhancements

1. **Additional Shared Pages**
   - Consider moving NotFound.razor to shared library
   - Create shared authentication pages

2. **Layout Templates**
   - Create layout component library
   - Support multiple layout themes

3. **Error Handling**
   - Enhanced error boundaries
   - Custom error logging integration

4. **Accessibility**
   - Ensure ARIA labels
   - Keyboard navigation support
