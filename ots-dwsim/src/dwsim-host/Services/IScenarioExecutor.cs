using DWSIM.OTS.SimulationHost.Models;

namespace DWSIM.OTS.SimulationHost.Services;

public interface IScenarioExecutor
{
    /// <summary>
    /// Loads a scenario from a JSON file.
    /// </summary>
    Task<Scenario> LoadScenarioAsync(string scenarioPath);

    /// <summary>
    /// Runs a scenario on a session.
    /// </summary>
    Task<string> RunScenarioAsync(string sessionId, string scenarioId, bool autostart = true);

    /// <summary>
    /// Stops a running scenario.
    /// </summary>
    Task StopScenarioAsync(string scenarioRunId);

    /// <summary>
    /// Pauses a running scenario.
    /// </summary>
    Task PauseScenarioAsync(string scenarioRunId);

    /// <summary>
    /// Resumes a paused scenario.
    /// </summary>
    Task ResumeScenarioAsync(string scenarioRunId);

    /// <summary>
    /// Gets the status of a scenario run.
    /// </summary>
    Task<ScenarioRunStatus?> GetStatusAsync(string scenarioRunId);

    /// <summary>
    /// Gets all loaded scenarios.
    /// </summary>
    Task<List<Scenario>> GetAvailableScenariosAsync();
}
