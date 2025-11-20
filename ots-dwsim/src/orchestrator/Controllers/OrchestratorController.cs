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
    private readonly IAssessmentEngine _assessmentEngine;
    private readonly ILogger<OrchestratorController> _logger;

    public OrchestratorController(
        ISessionPoolManager poolManager,
        IAssessmentEngine assessmentEngine,
        ILogger<OrchestratorController> logger)
    {
        _poolManager = poolManager;
        _assessmentEngine = assessmentEngine;
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

    // ============================================
    // Assessment Endpoints
    // ============================================

    /// <summary>
    /// Generate an assessment report for a session
    /// </summary>
    /// <param name="request">Assessment generation request</param>
    /// <returns>Generated assessment report</returns>
    [HttpPost("assessments")]
    [ProducesResponseType(typeof(AssessmentReport), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentReport>> GenerateAssessment([FromBody] GenerateAssessmentRequest request)
    {
        try
        {
            _logger.LogInformation("Generating assessment for session {SessionId}", request.SessionId);

            // Verify session exists
            var session = await _poolManager.GetSessionAsync(request.SessionId);
            if (session == null)
            {
                return NotFound(new { error = $"Session {request.SessionId} not found" });
            }

            var report = await _assessmentEngine.GenerateAssessmentAsync(
                request.SessionId,
                request.ScenarioId);

            return CreatedAtAction(
                nameof(GetAssessment),
                new { assessmentId = report.AssessmentId },
                report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating assessment for session {SessionId}", request.SessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific assessment report
    /// </summary>
    /// <param name="assessmentId">Assessment ID</param>
    [HttpGet("assessments/{assessmentId}")]
    [ProducesResponseType(typeof(AssessmentReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentReport>> GetAssessment(string assessmentId)
    {
        try
        {
            var report = await _assessmentEngine.GetAssessmentAsync(assessmentId);

            if (report == null)
            {
                return NotFound(new { error = $"Assessment {assessmentId} not found" });
            }

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assessment {AssessmentId}", assessmentId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all assessments, optionally filtered by session
    /// </summary>
    /// <param name="sessionId">Optional session ID filter</param>
    [HttpGet("assessments")]
    [ProducesResponseType(typeof(List<AssessmentReport>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssessmentReport>>> ListAssessments([FromQuery] string? sessionId = null)
    {
        try
        {
            var reports = await _assessmentEngine.ListAssessmentsAsync(sessionId);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing assessments");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get performance metrics for a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpGet("sessions/{sessionId}/metrics")]
    [ProducesResponseType(typeof(PerformanceMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PerformanceMetrics>> GetSessionMetrics(string sessionId)
    {
        try
        {
            // Verify session exists
            var session = await _poolManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = $"Session {sessionId} not found" });
            }

            var metrics = await _assessmentEngine.CalculateMetricsAsync(sessionId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating metrics for session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }
}
