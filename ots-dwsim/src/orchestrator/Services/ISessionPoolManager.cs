using DWSIM.OTS.Orchestrator.Models;

namespace DWSIM.OTS.Orchestrator.Services;

/// <summary>
/// Interface for managing a pool of DWSIM simulation sessions across multiple hosts
/// </summary>
public interface ISessionPoolManager
{
    /// <summary>
    /// Create a new session on an available host
    /// </summary>
    Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request);

    /// <summary>
    /// Get session information
    /// </summary>
    Task<ManagedSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// List all sessions
    /// </summary>
    Task<List<SessionListItem>> ListSessionsAsync(SessionStatus? filterByStatus = null);

    /// <summary>
    /// Start a session
    /// </summary>
    Task<SessionControlResponse> StartSessionAsync(string sessionId, StartSessionRequest request);

    /// <summary>
    /// Pause a session
    /// </summary>
    Task<SessionControlResponse> PauseSessionAsync(string sessionId);

    /// <summary>
    /// Stop a session
    /// </summary>
    Task<SessionControlResponse> StopSessionAsync(string sessionId);

    /// <summary>
    /// Delete a session
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId);

    /// <summary>
    /// Read a tag value from a session
    /// </summary>
    Task<TagValueResponse> ReadTagAsync(string sessionId, string tagPath);

    /// <summary>
    /// Write a tag value to a session
    /// </summary>
    Task<TagValueResponse> WriteTagAsync(string sessionId, string tagPath, object value, string? operatorId = null);

    /// <summary>
    /// Get session events
    /// </summary>
    Task<List<EventLogEntry>> GetSessionEventsAsync(string sessionId);

    /// <summary>
    /// Get pool statistics
    /// </summary>
    Task<SessionStatistics> GetStatisticsAsync();

    /// <summary>
    /// Get orchestrator health status
    /// </summary>
    Task<OrchestratorHealth> GetHealthAsync();
}
