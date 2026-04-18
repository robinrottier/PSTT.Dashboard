using Microsoft.Playwright;

namespace MqttDashboard.PlaywrightTests;

/// <summary>
/// Tests the responsive AppBar behaviour at different viewport widths.
/// Covers hamburger visibility, edit-toggle hide/show, and menu opening.
/// </summary>
[Trait("Category","Playwright")]
public class AppBarTests : IClassFixture<PlaywrightWebAppFixture>
{
    private readonly PlaywrightWebAppFixture _fixture;

    public AppBarTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IPage> NewPageWithSizeAsync(int width, int height)
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        // Use DOMContentLoaded first so we don't block on Blazor's persistent WebSocket.
        await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        // Wait for the AppBar to be present (SSR renders it synchronously).
        await page.Locator("header.mud-appbar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        return page;
    }

    /// <summary>
    /// Waits for the Blazor Server circuit to be established so that interactive
    /// click handlers respond. Uses NetworkIdle — which fires once the WebSocket
    /// upgrade (= circuit handshake) completes and no further HTTP requests are
    /// in flight — as the circuit-ready signal.
    /// </summary>
    private static async Task WaitForBlazorCircuitAsync(IPage page)
    {
        // NetworkIdle fires when ≤0 in-flight HTTP requests for 500ms.
        // Blazor's initial HTTP requests (scripts, CSS) complete before the WebSocket
        // upgrade; the upgrade itself is the last HTTP exchange, so NetworkIdle fires
        // shortly after the circuit connects.
        await page.WaitForLoadStateAsync(
            LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = 20_000 });
    }

    [Fact]
    public async Task AppBar_NarrowViewport_HamburgerStillVisible()
    {
        var page = await NewPageWithSizeAsync(320, 600);
        try
        {
            // .appbar-menu-pin has flex-shrink:0 — the hamburger button must always be visible.
            var hamburger = page.Locator(".appbar-menu-pin button");
            await Assertions.Expect(hamburger).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AppBar_WideViewport_EditToggleVisible()
    {
        var page = await NewPageWithSizeAsync(1024, 768);
        try
        {
            // The edit toggle is inside .toolbar-hide-xs — only hidden at < 450px.
            // It is only rendered when not in read-only mode AND auth is not required.
            // The test environment (ASPNETCORE_ENVIRONMENT=Test, ReadOnly=false) should show it.
            var editSwitch = page.Locator(".toolbar-hide-xs .mud-switch-base").First;
            await editSwitch.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
            await Assertions.Expect(editSwitch).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AppBar_NarrowViewport_ToolbarItemsHidden()
    {
        var page = await NewPageWithSizeAsync(320, 600);
        try
        {
            // .toolbar-hide-xs items have display:none at < 450px (see MainLayout.razor.css).
            // The first .toolbar-hide-xs div holds the MQTT icon.
            var toolbarHideXs = page.Locator(".toolbar-hide-xs").First;
            var count = await toolbarHideXs.CountAsync();
            if (count > 0)
                await Assertions.Expect(toolbarHideXs).ToBeHiddenAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AppBar_HamburgerMenu_Opens()
    {
        var page = await NewPageWithSizeAsync(400, 700);
        try
        {
            // Must wait for the Blazor Server circuit before clicking — @onclick handlers
            // are not wired until SignalR connects.
            await WaitForBlazorCircuitAsync(page);

            var hamburger = page.Locator(".appbar-menu-pin button").First;
            await hamburger.ClickAsync();

            // MudBlazor 9 renders menu items with class .mud-menu-item inside .mud-menu-list.
            // Wait for the popover to be open, then check at least one item is visible.
            var menuItem = page.Locator(".mud-menu-item").First;
            await menuItem.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
            await Assertions.Expect(menuItem).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }
}
