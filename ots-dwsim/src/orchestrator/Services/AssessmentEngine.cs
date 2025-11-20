using DWSIM.OTS.Orchestrator.Models;
using System.Collections.Concurrent;

namespace DWSIM.OTS.Orchestrator.Services;

/// <summary>
/// Engine for evaluating trainee performance based on KPIs
/// </summary>
public class AssessmentEngine : IAssessmentEngine
{
    private readonly ISessionPoolManager _sessionManager;
    private readonly ITimescaleDbLogger _logger;
    private readonly ILogger<AssessmentEngine> _appLogger;
    private readonly ConcurrentDictionary<string, AssessmentReport> _assessments = new();
    private readonly AssessmentConfig _config;

    public AssessmentEngine(
        ISessionPoolManager sessionManager,
        ITimescaleDbLogger logger,
        ILogger<AssessmentEngine> appLogger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _appLogger = appLogger;

        // Default KPI configuration
        _config = new AssessmentConfig
        {
            Kpis = new List<KpiDefinition>
            {
                new KpiDefinition
                {
                    Name = "Completion Time",
                    Description = "Time taken to complete the training scenario",
                    Type = "time",
                    Weight = 1.5,
                    TargetValue = TimeSpan.FromMinutes(30),
                    Threshold = TimeSpan.FromMinutes(45)
                },
                new KpiDefinition
                {
                    Name = "Operator Actions",
                    Description = "Number of operator interventions",
                    Type = "count",
                    Weight = 1.0,
                    TargetValue = 10,
                    Threshold = 20
                },
                new KpiDefinition
                {
                    Name = "Snapshot Usage",
                    Description = "Effective use of save/restore functionality",
                    Type = "count",
                    Weight = 0.5
                },
                new KpiDefinition
                {
                    Name = "Error Recovery",
                    Description = "Number of times snapshot was restored",
                    Type = "count",
                    Weight = 0.8,
                    TargetValue = 0,
                    Threshold = 3
                }
            },
            PassingScore = 70.0
        };
    }

    public async Task<AssessmentReport> GenerateAssessmentAsync(string sessionId, string? scenarioId = null)
    {
        _appLogger.LogInformation("Generating assessment for session {SessionId}", sessionId);

        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Calculate performance metrics
        var metrics = await CalculateMetricsAsync(sessionId);

        // Evaluate each KPI
        var kpiResults = new List<KpiResult>();
        double totalWeightedScore = 0;
        double totalWeight = 0;

        foreach (var kpi in _config.Kpis)
        {
            var result = await EvaluateKpiAsync(kpi, session, metrics);
            kpiResults.Add(result);

            totalWeightedScore += result.Score * kpi.Weight;
            totalWeight += kpi.Weight * result.MaxScore;
        }

        // Calculate overall score (0-100)
        var overallScore = totalWeight > 0 ? (totalWeightedScore / totalWeight) * 100 : 0;

        // Determine grade
        var grade = CalculateGrade(overallScore);

        // Determine pass/fail
        var passed = overallScore >= _config.PassingScore;

        var assessmentId = $"assess-{Guid.NewGuid().ToString()[..8]}";
        var report = new AssessmentReport
        {
            AssessmentId = assessmentId,
            SessionId = sessionId,
            ScenarioId = scenarioId,
            GeneratedAt = DateTime.UtcNow,
            OperatorId = session.OperatorId ?? "unknown",
            OverallScore = Math.Round(overallScore, 2),
            KpiResults = kpiResults,
            Metrics = new Dictionary<string, object>
            {
                ["session_duration"] = metrics.SessionDuration.ToString(),
                ["total_actions"] = metrics.TotalOperatorActions,
                ["tag_writes"] = metrics.TagWrites,
                ["snapshots_created"] = metrics.SnapshotsCreated,
                ["snapshots_restored"] = metrics.SnapshotsRestored,
                ["events_processed"] = metrics.EventsProcessed
            },
            Grade = grade,
            Passed = passed,
            Comments = GenerateComments(passed, overallScore, kpiResults)
        };

        _assessments[assessmentId] = report;

        _appLogger.LogInformation("Assessment {AssessmentId} generated: Score={Score}, Grade={Grade}, Passed={Passed}",
            assessmentId, overallScore, grade, passed);

        return report;
    }

    public Task<AssessmentReport?> GetAssessmentAsync(string assessmentId)
    {
        _assessments.TryGetValue(assessmentId, out var report);
        return Task.FromResult(report);
    }

    public Task<List<AssessmentListItem>> ListAssessmentsAsync(string? operatorId = null)
    {
        var assessments = _assessments.Values
            .Where(a => operatorId == null || a.OperatorId == operatorId)
            .OrderByDescending(a => a.GeneratedAt)
            .Select(a => new AssessmentListItem
            {
                AssessmentId = a.AssessmentId,
                SessionName = a.SessionId,
                OperatorId = a.OperatorId,
                GeneratedAt = a.GeneratedAt,
                OverallScore = a.OverallScore,
                Grade = a.Grade,
                Passed = a.Passed
            })
            .ToList();

        return Task.FromResult(assessments);
    }

    public async Task<PerformanceMetrics> CalculateMetricsAsync(string sessionId)
    {
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Get session events
        var events = await _sessionManager.GetSessionEventsAsync(sessionId);

        // Calculate metrics
        var metrics = new PerformanceMetrics
        {
            SessionDuration = session.EndedAt.HasValue
                ? session.EndedAt.Value - session.CreatedAt
                : DateTime.UtcNow - session.CreatedAt,
            TotalOperatorActions = events.Count(e => e.EventType == "operator_action"),
            TagWrites = events.Count(e => e.EventType == "operator_action" && e.TagPath != null),
            SnapshotsCreated = events.Count(e => e.EventType == "snapshot_created"),
            SnapshotsRestored = events.Count(e => e.EventType == "snapshot_restored"),
            EventsProcessed = events.Count
        };

        // Event type breakdown
        metrics.EventTypeBreakdown = events
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate average response time (simplified - time between events)
        if (events.Count > 1)
        {
            var sortedEvents = events.OrderBy(e => e.Timestamp).ToList();
            var totalMs = 0.0;
            for (int i = 1; i < sortedEvents.Count; i++)
            {
                totalMs += (sortedEvents[i].Timestamp - sortedEvents[i - 1].Timestamp).TotalMilliseconds;
            }
            metrics.AverageResponseTime = totalMs / (events.Count - 1);
        }

        return metrics;
    }

    private async Task<KpiResult> EvaluateKpiAsync(KpiDefinition kpi, ManagedSession session, PerformanceMetrics metrics)
    {
        object actualValue;
        double score;
        double maxScore = 100;
        bool passed;
        string? feedback = null;

        switch (kpi.Name)
        {
            case "Completion Time":
                actualValue = metrics.SessionDuration;
                var targetTime = (TimeSpan)kpi.TargetValue!;
                var thresholdTime = (TimeSpan)kpi.Threshold!;

                if (metrics.SessionDuration <= targetTime)
                {
                    score = maxScore;
                    feedback = $"Excellent! Completed in {metrics.SessionDuration:mm\\:ss}";
                }
                else if (metrics.SessionDuration <= thresholdTime)
                {
                    var ratio = (thresholdTime - metrics.SessionDuration).TotalSeconds /
                                (thresholdTime - targetTime).TotalSeconds;
                    score = maxScore * Math.Max(0, ratio);
                    feedback = $"Completed in {metrics.SessionDuration:mm\\:ss}, could be faster";
                }
                else
                {
                    score = 0;
                    feedback = $"Took too long: {metrics.SessionDuration:mm\\:ss}";
                }
                passed = metrics.SessionDuration <= thresholdTime;
                break;

            case "Operator Actions":
                actualValue = metrics.TotalOperatorActions;
                var targetActions = Convert.ToInt32(kpi.TargetValue);
                var thresholdActions = Convert.ToInt32(kpi.Threshold);

                if (metrics.TotalOperatorActions <= targetActions)
                {
                    score = maxScore;
                    feedback = "Efficient operation with minimal interventions";
                }
                else if (metrics.TotalOperatorActions <= thresholdActions)
                {
                    score = maxScore * (1.0 - (double)(metrics.TotalOperatorActions - targetActions) /
                                       (thresholdActions - targetActions));
                    feedback = $"{metrics.TotalOperatorActions} actions - more than optimal";
                }
                else
                {
                    score = 0;
                    feedback = $"Too many actions: {metrics.TotalOperatorActions}";
                }
                passed = metrics.TotalOperatorActions <= thresholdActions;
                break;

            case "Snapshot Usage":
                actualValue = metrics.SnapshotsCreated;
                score = metrics.SnapshotsCreated > 0 ? maxScore : 50;
                passed = true;
                feedback = metrics.SnapshotsCreated > 0
                    ? $"Good use of snapshots ({metrics.SnapshotsCreated} created)"
                    : "No snapshots created";
                break;

            case "Error Recovery":
                actualValue = metrics.SnapshotsRestored;
                var targetRestores = Convert.ToInt32(kpi.TargetValue ?? 0);
                var thresholdRestores = Convert.ToInt32(kpi.Threshold ?? 3);

                if (metrics.SnapshotsRestored <= targetRestores)
                {
                    score = maxScore;
                    feedback = "No errors requiring recovery";
                }
                else if (metrics.SnapshotsRestored <= thresholdRestores)
                {
                    score = maxScore * (1.0 - (double)metrics.SnapshotsRestored / thresholdRestores);
                    feedback = $"{metrics.SnapshotsRestored} recovery attempts";
                }
                else
                {
                    score = 0;
                    feedback = $"Too many errors: {metrics.SnapshotsRestored} recoveries";
                }
                passed = metrics.SnapshotsRestored <= thresholdRestores;
                break;

            default:
                actualValue = 0;
                score = 0;
                passed = false;
                feedback = "Unknown KPI";
                break;
        }

        return new KpiResult
        {
            KpiName = kpi.Name,
            Description = kpi.Description,
            Score = Math.Round(score, 2),
            MaxScore = maxScore,
            Weight = kpi.Weight,
            Passed = passed,
            ActualValue = actualValue,
            TargetValue = kpi.TargetValue,
            Threshold = kpi.Threshold,
            Feedback = feedback
        };
    }

    private string CalculateGrade(double score)
    {
        if (score >= 90) return "A";
        if (score >= 80) return "B";
        if (score >= 70) return "C";
        if (score >= 60) return "D";
        return "F";
    }

    private string GenerateComments(bool passed, double score, List<KpiResult> kpis)
    {
        if (passed && score >= 90)
        {
            return "Excellent performance! You demonstrated strong understanding of process control and made efficient decisions.";
        }
        else if (passed && score >= 80)
        {
            return "Good performance. You successfully completed the training with room for improvement in efficiency.";
        }
        else if (passed)
        {
            return "Satisfactory performance. Consider reviewing the areas where you lost points and practice more scenarios.";
        }
        else
        {
            var failedKpis = kpis.Where(k => !k.Passed).Select(k => k.KpiName);
            return $"Training not passed. Focus on improving: {string.Join(", ", failedKpis)}. Additional practice recommended.";
        }
    }
}
