using System.Text.Json.Serialization;

namespace DWSIM.OTS.SimulationHost.Models;

/// <summary>
/// Represents a complete training scenario
/// </summary>
public class Scenario
{
    [JsonPropertyName("scenario_id")]
    public required string ScenarioId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("seed")]
    public required int Seed { get; set; }

    [JsonPropertyName("initial_state")]
    public required Dictionary<string, object> InitialState { get; set; }

    [JsonPropertyName("events")]
    public required List<ScenarioEvent> Events { get; set; }

    [JsonPropertyName("metadata")]
    public ScenarioMetadata? Metadata { get; set; }
}

/// <summary>
/// Event in a scenario timeline
/// </summary>
public class ScenarioEvent
{
    [JsonPropertyName("time_s")]
    public required double TimeSeconds { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; } // fault, set, controller, note, random_fault, alarm, operator_prompt

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("repeat")]
    public EventRepeat? Repeat { get; set; }
}

/// <summary>
/// Repeat configuration for periodic events
/// </summary>
public class EventRepeat
{
    [JsonPropertyName("interval_s")]
    public required double IntervalSeconds { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; } // null = infinite
}

/// <summary>
/// Scenario metadata
/// </summary>
public class ScenarioMetadata
{
    [JsonPropertyName("recommended_time_factor")]
    public double? RecommendedTimeFactor { get; set; }

    [JsonPropertyName("expected_duration_seconds")]
    public int? ExpectedDurationSeconds { get; set; }

    [JsonPropertyName("difficulty")]
    public string? Difficulty { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("learning_objectives")]
    public List<string>? LearningObjectives { get; set; }

    [JsonPropertyName("required_flowsheet")]
    public string? RequiredFlowsheet { get; set; }

    [JsonPropertyName("pass_criteria")]
    public PassCriteria? PassCriteria { get; set; }
}

/// <summary>
/// Assessment criteria for scenario completion
/// </summary>
public class PassCriteria
{
    [JsonPropertyName("max_alarms")]
    public int? MaxAlarms { get; set; }

    [JsonPropertyName("max_time_s")]
    public int? MaxTimeSeconds { get; set; }

    [JsonPropertyName("required_actions")]
    public List<string>? RequiredActions { get; set; }

    [JsonPropertyName("target_values")]
    public Dictionary<string, TargetValue>? TargetValues { get; set; }
}

/// <summary>
/// Target value with tolerance
/// </summary>
public class TargetValue
{
    [JsonPropertyName("value")]
    public required double Value { get; set; }

    [JsonPropertyName("tolerance")]
    public double? Tolerance { get; set; }
}

/// <summary>
/// Running scenario instance
/// </summary>
public class ScenarioRun
{
    public required string RunId { get; set; }
    public required string SessionId { get; set; }
    public required string ScenarioId { get; set; }
    public required Scenario Scenario { get; set; }
    public ScenarioRunStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double ElapsedSimTimeSeconds { get; set; }
    public int EventsExecuted { get; set; }
    public int TotalEvents { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public Task? ExecutionTask { get; set; }
}

/// <summary>
/// Scenario execution status
/// </summary>
public enum ScenarioRunStatus
{
    Initializing,
    Running,
    Paused,
    Completed,
    Stopped,
    Failed
}

/// <summary>
/// Request to run a scenario
/// </summary>
public class RunScenarioRequest
{
    public required string ScenarioPath { get; set; }
    public double? TimeFactor { get; set; } = 1.0;
}

/// <summary>
/// Response when starting a scenario
/// </summary>
public class RunScenarioResponse
{
    public required string RunId { get; set; }
    public required string ScenarioId { get; set; }
    public ScenarioRunStatus Status { get; set; }
    public int TotalEvents { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Scenario status response
/// </summary>
public class ScenarioStatusResponse
{
    public required string RunId { get; set; }
    public required string ScenarioId { get; set; }
    public ScenarioRunStatus Status { get; set; }
    public double ElapsedSimTimeSeconds { get; set; }
    public int EventsExecuted { get; set; }
    public int TotalEvents { get; set; }
    public double ProgressPercent { get; set; }
}
