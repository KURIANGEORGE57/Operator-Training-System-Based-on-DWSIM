using DWSIM.OTS.SimulationHost.Models;

namespace DWSIM.OTS.SimulationHost.Services;

/// <summary>
/// Interface for scenario execution and management
/// </summary>
public interface IScenarioExecutor
{
    /// <summary>
    /// Load a scenario from file
    /// </summary>
    Task<Scenario> LoadScenarioAsync(string scenarioPath);

    /// <summary>
    /// Start running a scenario on a session
    /// </summary>
    Task<RunScenarioResponse> RunScenarioAsync(string sessionId, string scenarioPath, double timeFactor = 1.0);

    /// <summary>
    /// Stop a running scenario
    /// </summary>
    Task StopScenarioAsync(string runId);

    /// <summary>
    /// Get status of a running scenario
    /// </summary>
    Task<ScenarioStatusResponse?> GetStatusAsync(string runId);

    /// <summary>
    /// List all scenario runs
    /// </summary>
    Task<List<ScenarioStatusResponse>> ListRunsAsync();
}
