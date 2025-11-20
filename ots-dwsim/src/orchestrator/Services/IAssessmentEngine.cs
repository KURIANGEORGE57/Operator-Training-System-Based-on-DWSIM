using DWSIM.OTS.Orchestrator.Models;

namespace DWSIM.OTS.Orchestrator.Services;

/// <summary>
/// Interface for trainee performance assessment
/// </summary>
public interface IAssessmentEngine
{
    /// <summary>
    /// Generate assessment report for a training session
    /// </summary>
    Task<AssessmentReport> GenerateAssessmentAsync(string sessionId, string? scenarioId = null);

    /// <summary>
    /// Get assessment report by ID
    /// </summary>
    Task<AssessmentReport?> GetAssessmentAsync(string assessmentId);

    /// <summary>
    /// List all assessments
    /// </summary>
    Task<List<AssessmentListItem>> ListAssessmentsAsync(string? operatorId = null);

    /// <summary>
    /// Calculate performance metrics from session events
    /// </summary>
    Task<PerformanceMetrics> CalculateMetricsAsync(string sessionId);
}
