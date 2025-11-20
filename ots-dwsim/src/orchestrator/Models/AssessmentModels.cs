using System.Text.Json.Serialization;

namespace DWSIM.OTS.Orchestrator.Models;

/// <summary>
/// Training session assessment generation request
/// </summary>
public class GenerateAssessmentRequest
{
    public required string SessionId { get; set; }
    public string? ScenarioId { get; set; }
}

/// <summary>
/// Assessment report for a training session
/// </summary>
public class AssessmentReport
{
    public required string AssessmentId { get; set; }
    public required string SessionId { get; set; }
    public string? ScenarioId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public required string OperatorId { get; set; }
    public double OverallScore { get; set; }
    public required List<KpiResult> KpiResults { get; set; }
    public required Dictionary<string, object> Metrics { get; set; }
    public string Grade { get; set; } = "N/A";
    public bool Passed { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Individual KPI evaluation result
/// </summary>
public class KpiResult
{
    public required string KpiName { get; set; }
    public required string Description { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public double Weight { get; set; }
    public bool Passed { get; set; }
    public required object ActualValue { get; set; }
    public object? TargetValue { get; set; }
    public object? Threshold { get; set; }
    public string? Feedback { get; set; }
}

/// <summary>
/// KPI definition for assessment
/// </summary>
public class KpiDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Type { get; set; } // time, count, value, boolean
    public double Weight { get; set; } = 1.0;
    public object? TargetValue { get; set; }
    public object? Threshold { get; set; }
    public bool Required { get; set; } = false;
}

/// <summary>
/// Session performance metrics
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan SessionDuration { get; set; }
    public int TotalOperatorActions { get; set; }
    public int TagWrites { get; set; }
    public int SnapshotsCreated { get; set; }
    public int SnapshotsRestored { get; set; }
    public double AverageResponseTime { get; set; }
    public int EventsProcessed { get; set; }
    public Dictionary<string, int> EventTypeBreakdown { get; set; } = new();
}

/// <summary>
/// Assessment configuration
/// </summary>
public class AssessmentConfig
{
    public required List<KpiDefinition> Kpis { get; set; }
    public double PassingScore { get; set; } = 70.0;
    public bool StrictMode { get; set; } = false;
    public Dictionary<string, string> GradeThresholds { get; set; } = new()
    {
        ["A"] = "90",
        ["B"] = "80",
        ["C"] = "70",
        ["D"] = "60",
        ["F"] = "0"
    };
}

/// <summary>
/// List of assessments response
/// </summary>
public class AssessmentListItem
{
    public required string AssessmentId { get; set; }
    public required string SessionName { get; set; }
    public required string OperatorId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public double OverallScore { get; set; }
    public string Grade { get; set; } = "N/A";
    public bool Passed { get; set; }
}
