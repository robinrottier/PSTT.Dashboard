using Microsoft.AspNetCore.Mvc;
using MqttDashboard.Models;
using MqttDashboard.Server.Services;
using MqttDashboard.Server.Filters;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
public class DashboardController : ControllerBase
{
    private readonly DashboardStorageService _storageService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(DashboardStorageService storageService, ILogger<DashboardController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardModel>> GetDiagram()
    {
        _logger.LogInformation("[DashboardController] GET diagram requested");
        try
        {
            var dashboard = await _storageService.LoadDiagramAsync();

            if (dashboard == null)
            {
                _logger.LogWarning("[DashboardController] No diagram found, returning 404");
                return NotFound();
            }

            _logger.LogInformation("[DashboardController] Returning dashboard with {PageCount} pages", dashboard.Pages.Count);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DashboardController] Error in GET diagram");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost]
    [ServiceFilter(typeof(RequireAdminFilter))]
    public async Task<ActionResult> SaveDashboard([FromBody] DashboardModel dashboard)
    {
        _logger.LogInformation("[DashboardController] POST diagram requested with {PageCount} pages", 
            dashboard?.Pages?.Count ?? 0);

        try
        {
            if (dashboard == null)
            {
                _logger.LogWarning("[DashboardController] DashboardModel is null");
                return BadRequest("Dashboard cannot be null");
            }

            _logger.LogInformation("[DashboardController] Saving dashboard...");
            var success = await _storageService.SaveDiagramAsync(dashboard);

            if (!success)
            {
                _logger.LogError("[DashboardController] Storage service returned false");
                return StatusCode(500, "Failed to save dashboard");
            }

            _logger.LogInformation("[DashboardController] Dashboard saved successfully");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DashboardController] Error in POST dashboard: {Message}", ex.Message);
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpGet("list")]
    public async Task<ActionResult<List<string>>> ListDiagrams()
    {
        var names = await _storageService.ListDiagramNamesAsync();
        return Ok(names);
    }

    [HttpGet("{name}")]
    public async Task<ActionResult<DashboardModel>> GetDiagramByName(string name)
    {
        var diagram = await _storageService.LoadDiagramByNameAsync(name);
        if (diagram == null) return NotFound();
        return Ok(diagram);
    }

    [HttpPost("{name}")]
    [ServiceFilter(typeof(RequireAdminFilter))]
    public async Task<ActionResult> SaveDiagramByName(string name, [FromBody] DashboardModel dashboard)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name required");
        var success = await _storageService.SaveDiagramByNameAsync(name, dashboard);
        return success ? Ok() : StatusCode(500, "Failed to save");
    }

    [HttpDelete("{name}")]
    [ServiceFilter(typeof(RequireAdminFilter))]
    public async Task<ActionResult> DeleteDiagramByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name required");
        var success = await _storageService.DeleteDashboardByNameAsync(name);
        return success ? Ok() : NotFound();
    }
}
