using DWSIM.OTS.Orchestrator.Models;

namespace DWSIM.OTS.Orchestrator.Services;

/// <summary>
/// Interface for logging events and process variables to TimescaleDB
/// </summary>
public interface ITimescaleDbLogger
{
    /// <summary>
    /// Log a session event
    /// </summary>
    Task LogEventAsync(string sessionId, EventLogEntry entry);

    /// <summary>
    /// Log multiple events in batch
    /// </summary>
    Task LogEventsBatchAsync(string sessionId, IEnumerable<EventLogEntry> entries);

    /// <summary>
    /// Log a process variable value
    /// </summary>
    Task LogProcessVariableAsync(string sessionId, string tagPath, double value, double simTime);

    /// <summary>
    /// Log multiple process variables in batch
    /// </summary>
    Task LogProcessVariablesBatchAsync(string sessionId, IEnumerable<(string tagPath, double value, double simTime)> variables);

    /// <summary>
    /// Create or update session metadata
    /// </summary>
    Task UpsertSessionAsync(ManagedSession session);

    /// <summary>
    /// Get events for a session
    /// </summary>
    Task<List<EventLogEntry>> GetSessionEventsAsync(string sessionId, DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Get process variable history
    /// </summary>
    Task<List<(DateTime time, double value)>> GetProcessVariableHistoryAsync(
        string sessionId,
        string tagPath,
        DateTime? startTime = null,
        DateTime? endTime = null);

    /// <summary>
    /// Health check for database connection
    /// </summary>
    Task<bool> IsHealthyAsync();
}
