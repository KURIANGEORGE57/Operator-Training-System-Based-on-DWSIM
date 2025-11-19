namespace DWSIM.OTS.Orchestrator.Models;

/// <summary>
/// Represents a managed simulation session in the pool
/// </summary>
public class ManagedSession
{
    public required string SessionId { get; set; }
    public required string HostUrl { get; set; }
    public required string FlowsheetName { get; set; }
    public string? SessionName { get; set; }
    public string? OperatorId { get; set; }
    public string? ScenarioId { get; set; }
    public SessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Session status enumeration
/// </summary>
public enum SessionStatus
{
    Created,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed
}

/// <summary>
/// Request to create a new session
/// </summary>
public class CreateSessionRequest
{
    public required string FlowsheetName { get; set; }
    public string? SessionName { get; set; }
    public string? OperatorId { get; set; }
    public string? ScenarioId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response after creating a session
/// </summary>
public class CreateSessionResponse
{
    public required string SessionId { get; set; }
    public required string HostUrl { get; set; }
    public SessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to start a session
/// </summary>
public class StartSessionRequest
{
    public string StartMode { get; set; } = "run";
    public double? TimeFactor { get; set; } = 1.0;
}

/// <summary>
/// Session control response
/// </summary>
public class SessionControlResponse
{
    public required string SessionId { get; set; }
    public SessionStatus Status { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Tag read/write request
/// </summary>
public class TagOperationRequest
{
    public required string TagPath { get; set; }
    public object? Value { get; set; }
}

/// <summary>
/// Tag value response
/// </summary>
public class TagValueResponse
{
    public required string TagPath { get; set; }
    public object? Value { get; set; }
    public string Unit { get; set; } = "";
    public string Quality { get; set; } = "GOOD";
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Event log entry
/// </summary>
public class EventLogEntry
{
    public DateTime Timestamp { get; set; }
    public required string EventType { get; set; }
    public double SimTime { get; set; }
    public string? OperatorId { get; set; }
    public string? TagPath { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Session statistics
/// </summary>
public class SessionStatistics
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int AvailableHosts { get; set; }
    public int TotalCapacity { get; set; }
    public double UtilizationPercent { get; set; }
}

/// <summary>
/// Orchestrator health status
/// </summary>
public class OrchestratorHealth
{
    public string Status { get; set; } = "healthy";
    public DateTime Timestamp { get; set; }
    public SessionStatistics? Statistics { get; set; }
    public List<HostStatus> Hosts { get; set; } = new();
}

/// <summary>
/// Host status information
/// </summary>
public class HostStatus
{
    public required string HostUrl { get; set; }
    public bool IsHealthy { get; set; }
    public int ActiveSessions { get; set; }
    public int MaxSessions { get; set; }
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// Session list item
/// </summary>
public class SessionListItem
{
    public required string SessionId { get; set; }
    public required string SessionName { get; set; }
    public string? OperatorId { get; set; }
    public SessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Database configuration
/// </summary>
public class DatabaseConfig
{
    public required string ConnectionString { get; set; }
    public int BatchSize { get; set; } = 100;
    public int FlushIntervalSeconds { get; set; } = 5;
}

/// <summary>
/// Pool configuration
/// </summary>
public class PoolConfig
{
    public List<string> HostUrls { get; set; } = new() { "http://localhost:5000" };
    public int MaxSessionsPerHost { get; set; } = 10;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public int SessionTimeoutMinutes { get; set; } = 240;
}
