using MqttDashboard.Server.Services;
using MqttDashboard.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace MqttDashboard.Server.Tests;

public class DashboardStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DashboardStorageService _service;

    public DashboardStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DiagramStorage:DataDirectory"] = _tempDir })
            .Build();

        _service = new DashboardStorageService(env.Object, config, NullLogger<DashboardStorageService>.Instance);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var state = new DashboardModel { Name = "Test Diagram" };
        state.Pages.Add(new DashboardPageModel
        {
            Nodes = [new TextNodeData { Id = "n1", Title = "Node 1", X = 10, Y = 20, Width = 120, Height = 90 }]
        });

        var saved = await _service.SaveDiagramAsync(state);
        Assert.True(saved);

        var loaded = await _service.LoadDiagramAsync();
        Assert.NotNull(loaded);
        Assert.Equal("Test Diagram", loaded!.Name);
        Assert.Single(loaded.Pages);
        Assert.Single(loaded.Pages[0].Nodes);
        Assert.Equal("Node 1", loaded.Pages[0].Nodes[0].Title);
    }

    [Fact]
    public async Task Load_WhenNoFile_ReturnsNull()
    {
        var loaded = await _service.LoadDiagramAsync();
        Assert.Null(loaded);
    }

    [Fact]
    public async Task ConcurrentSaves_DoNotCorruptData()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _service.SaveDiagramAsync(new DashboardModel { Name = $"Diagram {i}" }));
        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r));
        // File should be valid JSON — load should succeed
        var loaded = await _service.LoadDiagramAsync();
        Assert.NotNull(loaded);
    }

    [Fact]
    public void DashboardsPath_IsSubdirectoryOfStoragePath()
    {
        Assert.Equal(Path.Combine(_tempDir, "dashboards"), _service.DashboardsPath);
    }

    [Fact]
    public async Task MigrateLegacyFiles_MovesJsonFilesToDashboardsSubdir()
    {
        // Create a new temp dir with legacy dashboard files in the root
        var migrationDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(migrationDir);
        var legacyFile = Path.Combine(migrationDir, "myboard.json");
        var stateFile = Path.Combine(migrationDir, "applicationstate.json");
        await File.WriteAllTextAsync(legacyFile, "{}");
        await File.WriteAllTextAsync(stateFile, "{}");

        try
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(migrationDir);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DiagramStorage:DataDirectory"] = migrationDir })
                .Build();

            _ = new DashboardStorageService(env.Object, config, NullLogger<DashboardStorageService>.Instance);

            // Dashboard file should have been moved to dashboards/
            Assert.True(File.Exists(Path.Combine(migrationDir, "dashboards", "myboard.json")));
            Assert.False(File.Exists(legacyFile));
            // applicationstate.json should remain in the root (not migrated)
            Assert.True(File.Exists(stateFile));
        }
        finally
        {
            Directory.Delete(migrationDir, recursive: true);
        }
    }

    [Fact]
    public void StoragePath_UsesConfiguredDirectory()
    {
        Assert.Equal(_tempDir, _service.StoragePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
