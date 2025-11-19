using DWSIM.OTS.Orchestrator.Models;
using DWSIM.OTS.Orchestrator.Services;
using Microsoft.AspNetCore.Mvc;

namespace DWSIM.OTS.Orchestrator.Controllers;

/// <summary>
/// Orchestrator API for managing multiple simulation sessions
/// </summary>
[ApiController]
[Route("api/v1")]
public class OrchestratorController : ControllerBase
{
    private readonly ISessionPoolManager _poolManager;
    private readonly ILogger<OrchestratorController> _logger;

    public OrchestratorController(
        ISessionPoolManager poolManager,
        ILogger<OrchestratorController> logger)
    {
        _poolManager = poolManager;
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<OrchestratorHealth>> GetHealth()
    {
        var health = await _poolManager.GetHealthAsync();
        return Ok(health);
    }

    /// <summary>
    /// Get pool statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<SessionStatistics>> GetStatistics()
    {
        var stats = await _poolManager.GetStatisticsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Create a new simulation session
    /// </summary>
    /// <param name="request">Session creation parameters</param>
    /// <returns>Created session information</returns>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            _logger.LogInformation("Creating session for flowsheet {Flowsheet}", request.FlowsheetName);

            var response = await _poolManager.CreateSessionAsync(request);

            return CreatedAtAction(
                nameof(GetSession),
                new { sessionId = response.SessionId },
                response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create session - no available hosts");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get session information
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(ManagedSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagedSession>> GetSession(string sessionId)
    {
        var session = await _poolManager.GetSessionAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }

        return Ok(session);
    }

    /// <summary>
    /// List all sessions
    /// </summary>
    /// <param name="status">Optional status filter</param>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<SessionListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionListItem>>> ListSessions([FromQuery] SessionStatus? status = null)
    {
        var sessions = await _poolManager.ListSessionsAsync(status);
        return Ok(sessions);
    }

    /// <summary>
    /// Start a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="request">Start parameters</param>
    [HttpPost("sessions/{sessionId}/start")]
    [ProducesResponseType(typeof(SessionControlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionControlResponse>> StartSession(
        string sessionId,
        [FromBody] StartSessionRequest request)
    {
        try
        {
            _logger.LogInformation("Starting session {SessionId}", sessionId);
            var response = await _poolManager.StartSessionAsync(sessionId, request);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pause a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpPost("sessions/{sessionId}/pause")]
    [ProducesResponseType(typeof(SessionControlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionControlResponse>> PauseSession(string sessionId)
    {
        try
        {
            _logger.LogInformation("Pausing session {SessionId}", sessionId);
            var response = await _poolManager.PauseSessionAsync(sessionId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpPost("sessions/{sessionId}/stop")]
    [ProducesResponseType(typeof(SessionControlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionControlResponse>> StopSession(string sessionId)
    {
        try
        {
            _logger.LogInformation("Stopping session {SessionId}", sessionId);
            var response = await _poolManager.StopSessionAsync(sessionId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        var deleted = await _poolManager.DeleteSessionAsync(sessionId);

        if (!deleted)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Read a tag value
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="tagPath">Tag path (e.g., "Streams.Feed.Temperature")</param>
    [HttpGet("sessions/{sessionId}/tags/{**tagPath}")]
    [ProducesResponseType(typeof(TagValueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagValueResponse>> ReadTag(string sessionId, string tagPath)
    {
        try
        {
            var response = await _poolManager.ReadTagAsync(sessionId, tagPath);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tag {TagPath} from session {SessionId}", tagPath, sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Write a tag value
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="tagPath">Tag path</param>
    /// <param name="request">Tag write request</param>
    [HttpPost("sessions/{sessionId}/tags/{**tagPath}")]
    [ProducesResponseType(typeof(TagValueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagValueResponse>> WriteTag(
        string sessionId,
        string tagPath,
        [FromBody] TagOperationRequest request)
    {
        try
        {
            var response = await _poolManager.WriteTagAsync(
                sessionId,
                tagPath,
                request.Value ?? 0,
                null); // TODO: Get operator ID from auth context

            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Session {sessionId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing tag {TagPath} to session {SessionId}", tagPath, sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get session event log
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpGet("sessions/{sessionId}/events")]
    [ProducesResponseType(typeof(List<EventLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EventLogEntry>>> GetSessionEvents(string sessionId)
    {
        try
        {
            var events = await _poolManager.GetSessionEventsAsync(sessionId);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events for session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }
}
