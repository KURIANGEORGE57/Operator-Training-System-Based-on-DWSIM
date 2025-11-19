namespace DWSIM.OTS.SimulationHost.Models;

public class Scenario
{
    public required string ScenarioId { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Seed { get; set; }
    public Dictionary<string, object>? InitialState { get; set; }
    public List<ScenarioEvent> Events { get; set; } = new();
    public ScenarioMetadata? Metadata { get; set; }
}

public class ScenarioEvent
{
    public double TimeS { get; set; }
    public required string Type { get; set; }  // set, fault, controller, note
    public string? Target { get; set; }
    public object? Payload { get; set; }
    public int? RepeatEvery { get; set; }
    public int? RepeatUntil { get; set; }
}

public class ScenarioMetadata
{
    public double RecommendedTimeFactor { get; set; } = 1.0;
    public int ExpectedDurationSeconds { get; set; }
    public string Difficulty { get; set; } = "intermediate";
    public List<string> LearningObjectives { get; set; } = new();
}

public class ScenarioRunStatus
{
    public required string ScenarioRunId { get; set; }
    public required string ScenarioId { get; set; }
    public required string SessionId { get; set; }
    public ScenarioStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int EventsExecuted { get; set; }
    public int TotalEvents { get; set; }
    public double CurrentSimTime { get; set; }
    public List<string> Errors { get; set; } = new();
}

public enum ScenarioStatus
{
    Scheduled,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
