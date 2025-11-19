using Microsoft.AspNetCore.Mvc;
using DWSIM.OTS.SimulationHost.Models;
using DWSIM.OTS.SimulationHost.Services;

namespace DWSIM.OTS.SimulationHost.Controllers;

[ApiController]
[Route("api/v1/scenarios")]
[Produces("application/json")]
public class ScenariosController : ControllerBase
{
    private readonly IScenarioExecutor _scenarioExecutor;
    private readonly ILogger<ScenariosController> _logger;

    public ScenariosController(IScenarioExecutor scenarioExecutor, ILogger<ScenariosController> logger)
    {
        _scenarioExecutor = scenarioExecutor;
        _logger = logger;
    }

    /// <summary>
    /// Get all available scenarios
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Scenario>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Scenario>>> GetAvailableScenarios()
    {
        try
        {
            var scenarios = await _scenarioExecutor.GetAvailableScenariosAsync();
            return Ok(scenarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available scenarios");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Load a scenario from file
    /// </summary>
    [HttpPost("load")]
    [ProducesResponseType(typeof(Scenario), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Scenario>> LoadScenario([FromQuery] string path)
    {
        try
        {
            var scenario = await _scenarioExecutor.LoadScenarioAsync(path);
            return Ok(scenario);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scenario file not found: {Path}", path);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scenario from {Path}", path);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Run a scenario on a session
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(RunScenarioResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunScenarioResponse>> RunScenario([FromBody] RunScenarioRequest request)
    {
        try
        {
            var scenarioRunId = await _scenarioExecutor.RunScenarioAsync(
                request.SessionId,
                request.ScenarioId,
                request.Autostart);

            var response = new RunScenarioResponse
            {
                Status = "scheduled",
                ScenarioRunId = scenarioRunId
            };

            return AcceptedAtAction(nameof(GetScenarioStatus), new { runId = scenarioRunId }, response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Session not found");
            return NotFound(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scenario not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running scenario");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get scenario run status
    /// </summary>
    [HttpGet("{runId}/status")]
    [ProducesResponseType(typeof(ScenarioRunStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScenarioRunStatus>> GetScenarioStatus(string runId)
    {
        try
        {
            var status = await _scenarioExecutor.GetStatusAsync(runId);
            if (status == null)
            {
                return NotFound(new { error = $"Scenario run not found: {runId}" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scenario status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Stop a running scenario
    /// </summary>
    [HttpPost("{runId}/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopScenario(string runId)
    {
        try
        {
            await _scenarioExecutor.StopScenarioAsync(runId);
            return Ok(new { message = "Scenario stopped" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scenario run not found: {RunId}", runId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping scenario");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Pause a running scenario
    /// </summary>
    [HttpPost("{runId}/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PauseScenario(string runId)
    {
        try
        {
            await _scenarioExecutor.PauseScenarioAsync(runId);
            return Ok(new { message = "Scenario paused" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scenario run not found: {RunId}", runId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing scenario");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Resume a paused scenario
    /// </summary>
    [HttpPost("{runId}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeScenario(string runId)
    {
        try
        {
            await _scenarioExecutor.ResumeScenarioAsync(runId);
            return Ok(new { message = "Scenario resumed" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Scenario run not found: {RunId}", runId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming scenario");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

// Request models
public class RunScenarioRequest
{
    public required string SessionId { get; set; }
    public required string ScenarioId { get; set; }
    public bool Autostart { get; set; } = true;
}
