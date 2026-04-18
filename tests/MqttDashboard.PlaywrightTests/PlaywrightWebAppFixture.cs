using System.Diagnostics;
using System.Text;
using Microsoft.Playwright;

namespace MqttDashboard.PlaywrightTests;

/// <summary>
/// xUnit class fixture that starts a real Kestrel instance of the server-only app
/// on a random port, initialises a headless Chromium browser, then tears both down
/// after the test class completes.
///
/// Uses <c>dotnet run --no-build --project &lt;path&gt;</c> to start the server
/// (the project is already built as a dependency of this test project).
///
/// Server stdout/stderr are captured asynchronously so pipe buffers never fill.
/// Call <see cref="ServerLog"/> at any point to inspect what the server has logged.
///
/// One-time browser setup (run once after build):
///   <code>pwsh bin/Debug/net10.0/playwright.ps1 install chromium</code>
/// </summary>
public sealed class PlaywrightWebAppFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private readonly StringBuilder _serverLog = new();
    private readonly string _tempDataDir =
        Path.Combine(Path.GetTempPath(), "pw_mqttdashboard_" + Guid.NewGuid().ToString("N"));

    public string BaseUrl { get; private set; } = "";
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }

    /// <summary>All stdout+stderr written by the server process so far.</summary>
    public string ServerLog { get { lock (_serverLog) return _serverLog.ToString(); } }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDataDir);

        var port = FindFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var serverProjectPath = FindServerProjectPath();

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            // --no-build: the server project is built as a dependency of this test project.
            Arguments = $"run --no-build --project \"{serverProjectPath}\" --no-launch-profile -- --urls \"{BaseUrl}\"",
            UseShellExecute = false,
            // Redirect output so tests can assert on log content (e.g. no unexpected errors).
            // Use BeginOutputReadLine / BeginErrorReadLine (async) so the pipe buffer never
            // fills and blocks the server process.
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Add env overrides to the inherited environment.
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Test";
        psi.Environment["DiagramStorage__DataDirectory"] = _tempDataDir;
        // Point at a non-existent broker so MqttClientService fails fast / silently.
        psi.Environment["MqttSettings__Broker"] = "127.0.0.1";
        psi.Environment["MqttSettings__Port"] = "19999";

        _serverProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start server process.");

        // Consume stdout/stderr on background threads — prevents OS pipe-buffer deadlock.
        _serverProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) lock (_serverLog) _serverLog.AppendLine(e.Data);
        };
        _serverProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) lock (_serverLog) _serverLog.AppendLine(e.Data);
        };
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        await WaitForServerAsync(TimeSpan.FromSeconds(60));

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.DisposeAsync();
        Playwright?.Dispose();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill(entireProcessTree: true);
            await _serverProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _serverProcess.Dispose();
        }

        if (Directory.Exists(_tempDataDir))
            try { Directory.Delete(_tempDataDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task WaitForServerAsync(TimeSpan timeout)
    {
        // Use ?ignoreMqtt so the endpoint returns 200 even without a broker —
        // the parameter was specifically added for test/startup probes.
        var probeUrl = $"{BaseUrl}/healthz?ignoreMqtt";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_serverProcess?.HasExited == true)
                throw new InvalidOperationException(
                    "Server process exited before becoming healthy.\n" + ServerLog);

            try
            {
                var resp = await http.GetAsync(probeUrl);
                if (resp.IsSuccessStatusCode)
                    return;

                // Got an HTTP response but not 2xx — the server is up but something is
                // wrong. Fail immediately rather than waiting out the full timeout.
                var body = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Server probe {probeUrl} returned {(int)resp.StatusCode} {resp.StatusCode}.\n{body}\n\nServer log so far:\n{ServerLog}");
            }
            catch (HttpRequestException)
            {
                // Connection refused — server not listening yet. Keep polling.
            }
            catch (TaskCanceledException)
            {
                // Request timeout — server slow to respond. Keep polling.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Server at {probeUrl} did not respond within {timeout}.\nServer log:\n{ServerLog}");
    }

    private static string FindServerProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.slnx").Any())
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException(
                "Could not find solution root from " + AppContext.BaseDirectory);

        return Path.Combine(
            dir.FullName, "src", "MqttDashboard.WebApp", "MqttDashboard.WebAppServerOnly");
    }

    private static int FindFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
