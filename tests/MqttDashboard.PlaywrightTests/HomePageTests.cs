using Microsoft.Playwright;

namespace MqttDashboard.PlaywrightTests;

/// <summary>
/// Smoke tests: verifies the home page loads and key toolbar elements are present.
/// Also verifies the server log is clean (no unexpected errors beyond known MQTT warnings).
/// </summary>
[Trait("Category","Playwright")]
public class HomePageTests : IClassFixture<PlaywrightWebAppFixture>
{
    private readonly PlaywrightWebAppFixture _fixture;

    public HomePageTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private Task<IPage> NewPageAsync() =>
        _fixture.Browser!.NewPageAsync();

    [Fact]
    public async Task HomePage_Loads_TitleVisible()
    {
        var page = await NewPageAsync();
        try
        {
        // Use DOMContentLoaded — Blazor Server keeps a WebSocket open which prevents
        // WaitUntilState.Load from ever completing in a reasonable time.
        await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        // The AppBar should contain the app name
        var header = page.Locator("header.mud-appbar");
        await header.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Assertions.Expect(header).ToBeVisibleAsync();

        // The "MQTT Dashboard" product name should be present somewhere in the title area
        var titleText = page.Locator(".appbar-title-inner");
        await Assertions.Expect(titleText).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task HomePage_Loads_HamburgerMenuVisible()
    {
        var page = await NewPageAsync();
        try
        {
        await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        // The hamburger menu icon (the MudMenu activator button inside .appbar-menu-pin)
        var hamburger = page.Locator(".appbar-menu-pin button");
        await hamburger.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Assertions.Expect(hamburger).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task HomePage_Loads_MqttStatusIconVisible()
    {
        var page = await NewPageAsync();
        try
        {
        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        // The MQTT status icon is inside .toolbar-hide-xs which is only visible at >=450px.
        var mqttIcon = page.Locator(".toolbar-hide-xs svg").First;
        await mqttIcon.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Assertions.Expect(mqttIcon).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    /// <summary>
    /// After loading the page, the server log must not contain any [ERR] lines beyond
    /// the expected MQTT-connection-refused warnings (there is intentionally no broker).
    /// This catches unhandled exceptions, middleware errors, and missing static assets.
    /// </summary>
    [Fact]
    public async Task ServerLog_HasNoUnexpectedErrors()
    {
        // Trigger a page load so the server handles at least one real request.
        var page = await NewPageAsync();
        try
        {
            await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.Locator("header.mud-appbar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        }
        finally { await page.CloseAsync(); }

        var log = _fixture.ServerLog;
        var errorLines = log
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase))
            // Expected: MQTT can't connect to the intentionally-absent broker.
            .Where(line => !line.Contains("MqttClientService", StringComparison.OrdinalIgnoreCase)
                        && !line.Contains("MqttCommunicationException", StringComparison.OrdinalIgnoreCase)
                        && !line.Contains("SocketException", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            errorLines.Count == 0,
            $"Unexpected [ERR] lines in server log:\n{string.Join('\n', errorLines)}\n\n--- Full server log ---\n{log}");
    }
}

