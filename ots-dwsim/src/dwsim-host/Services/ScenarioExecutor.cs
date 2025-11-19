using DWSIM.OTS.SimulationHost.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace DWSIM.OTS.SimulationHost.Services;

public class ScenarioExecutor : IScenarioExecutor
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ScenarioExecutor> _logger;
    private readonly ConcurrentDictionary<string, Scenario> _scenarios = new();
    private readonly ConcurrentDictionary<string, ScenarioRunStatus> _scenarioRuns = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly string _scenariosPath;

    public ScenarioExecutor(
        ISessionManager sessionManager,
        ILogger<ScenarioExecutor> logger,
        IConfiguration configuration)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _scenariosPath = configuration.GetValue<string>("ScenariosPath") ?? "samples/scenarios";
    }

    public async Task<Scenario> LoadScenarioAsync(string scenarioPath)
    {
        try
        {
            _logger.LogInformation("Loading scenario from {Path}", scenarioPath);

            var json = await File.ReadAllTextAsync(scenarioPath);
            var scenario = JsonConvert.DeserializeObject<Scenario>(json);

            if (scenario == null)
            {
                throw new InvalidOperationException("Failed to deserialize scenario");
            }

            _scenarios.TryAdd(scenario.ScenarioId, scenario);

            _logger.LogInformation("Loaded scenario {ScenarioId}: {Title}", scenario.ScenarioId, scenario.Title);

            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scenario from {Path}", scenarioPath);
            throw;
        }
    }

    public async Task<string> RunScenarioAsync(string sessionId, string scenarioId, bool autostart = true)
    {
        // Validate session exists
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Load scenario if not already loaded
        Scenario? scenario;
        if (!_scenarios.TryGetValue(scenarioId, out scenario))
        {
            // Try to load from filesystem
            var scenarioFile = Path.Combine(_scenariosPath, $"{scenarioId}.json");
            if (!File.Exists(scenarioFile))
            {
                throw new FileNotFoundException($"Scenario file not found: {scenarioFile}");
            }
            scenario = await LoadScenarioAsync(scenarioFile);
        }

        // Create scenario run
        var scenarioRunId = $"run-{Guid.NewGuid().ToString()[..8]}";
        var runStatus = new ScenarioRunStatus
        {
            ScenarioRunId = scenarioRunId,
            ScenarioId = scenarioId,
            SessionId = sessionId,
            Status = autostart ? ScenarioStatus.Running : ScenarioStatus.Scheduled,
            StartedAt = DateTime.UtcNow,
            EventsExecuted = 0,
            TotalEvents = scenario.Events.Count,
            CurrentSimTime = 0
        };

        _scenarioRuns.TryAdd(scenarioRunId, runStatus);

        _logger.LogInformation("Starting scenario run {ScenarioRunId} for session {SessionId}",
            scenarioRunId, sessionId);

        if (autostart)
        {
            // Start scenario execution in background
            var cts = new CancellationTokenSource();
            _cancellationTokens.TryAdd(scenarioRunId, cts);

            _ = Task.Run(async () => await ExecuteScenarioAsync(scenarioRunId, scenario, cts.Token));
        }

        return scenarioRunId;
    }

    public async Task StopScenarioAsync(string scenarioRunId)
    {
        if (!_scenarioRuns.TryGetValue(scenarioRunId, out var runStatus))
        {
            throw new KeyNotFoundException($"Scenario run {scenarioRunId} not found");
        }

        if (_cancellationTokens.TryRemove(scenarioRunId, out var cts))
        {
            cts.Cancel();
        }

        runStatus.Status = ScenarioStatus.Cancelled;
        runStatus.CompletedAt = DateTime.UtcNow;

        _logger.LogInformation("Stopped scenario run {ScenarioRunId}", scenarioRunId);

        await Task.CompletedTask;
    }

    public async Task PauseScenarioAsync(string scenarioRunId)
    {
        if (!_scenarioRuns.TryGetValue(scenarioRunId, out var runStatus))
        {
            throw new KeyNotFoundException($"Scenario run {scenarioRunId} not found");
        }

        runStatus.Status = ScenarioStatus.Paused;

        _logger.LogInformation("Paused scenario run {ScenarioRunId}", scenarioRunId);

        await Task.CompletedTask;
    }

    public async Task ResumeScenarioAsync(string scenarioRunId)
    {
        if (!_scenarioRuns.TryGetValue(scenarioRunId, out var runStatus))
        {
            throw new KeyNotFoundException($"Scenario run {scenarioRunId} not found");
        }

        runStatus.Status = ScenarioStatus.Running;

        _logger.LogInformation("Resumed scenario run {ScenarioRunId}", scenarioRunId);

        await Task.CompletedTask;
    }

    public Task<ScenarioRunStatus?> GetStatusAsync(string scenarioRunId)
    {
        _scenarioRuns.TryGetValue(scenarioRunId, out var runStatus);
        return Task.FromResult(runStatus);
    }

    public Task<List<Scenario>> GetAvailableScenariosAsync()
    {
        var scenarios = _scenarios.Values.ToList();
        return Task.FromResult(scenarios);
    }

    private async Task ExecuteScenarioAsync(string scenarioRunId, Scenario scenario, CancellationToken cancellationToken)
    {
        if (!_scenarioRuns.TryGetValue(scenarioRunId, out var runStatus))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Executing scenario {ScenarioId} for run {ScenarioRunId}",
                scenario.ScenarioId, scenarioRunId);

            // Apply initial state if specified
            if (scenario.InitialState != null)
            {
                await ApplyInitialStateAsync(runStatus.SessionId, scenario.InitialState);
            }

            // Sort events by time
            var sortedEvents = scenario.Events.OrderBy(e => e.TimeS).ToList();

            var session = await _sessionManager.GetSessionAsync(runStatus.SessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {runStatus.SessionId} not found");
            }

            double lastEventTime = 0;

            foreach (var evt in sortedEvents)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Wait for paused scenarios
                while (runStatus.Status == ScenarioStatus.Paused)
                {
                    await Task.Delay(100, cancellationToken);
                }

                // Wait until simulation time reaches event time
                var targetSimTime = session.SimTime.AddSeconds(evt.TimeS - lastEventTime);
                while (session.SimTime < targetSimTime && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                    session = await _sessionManager.GetSessionAsync(runStatus.SessionId);
                    if (session == null) break;
                }

                // Execute the event
                try
                {
                    await ExecuteEventAsync(runStatus.SessionId, evt);
                    runStatus.EventsExecuted++;
                    runStatus.CurrentSimTime = evt.TimeS;

                    _logger.LogInformation("Executed event {EventType} at {TimeS}s for run {ScenarioRunId}",
                        evt.Type, evt.TimeS, scenarioRunId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing event at {TimeS}s", evt.TimeS);
                    runStatus.Errors.Add($"Event at {evt.TimeS}s: {ex.Message}");
                }

                lastEventTime = evt.TimeS;
            }

            runStatus.Status = cancellationToken.IsCancellationRequested ? ScenarioStatus.Cancelled : ScenarioStatus.Completed;
            runStatus.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Completed scenario run {ScenarioRunId}", scenarioRunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scenario run {ScenarioRunId}", scenarioRunId);
            runStatus.Status = ScenarioStatus.Failed;
            runStatus.CompletedAt = DateTime.UtcNow;
            runStatus.Errors.Add($"Execution failed: {ex.Message}");
        }
    }

    private async Task ApplyInitialStateAsync(string sessionId, Dictionary<string, object> initialState)
    {
        foreach (var kvp in initialState)
        {
            try
            {
                var writeRequest = new WriteTagRequest
                {
                    Value = kvp.Value,
                    User = "scenario_executor",
                    Mode = "automated"
                };

                await _sessionManager.WriteTagAsync(sessionId, kvp.Key, writeRequest);

                _logger.LogDebug("Applied initial state: {Tag} = {Value}", kvp.Key, kvp.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying initial state for {Tag}", kvp.Key);
            }
        }
    }

    private async Task ExecuteEventAsync(string sessionId, ScenarioEvent evt)
    {
        switch (evt.Type.ToLower())
        {
            case "set":
                await ExecuteSetEventAsync(sessionId, evt);
                break;

            case "fault":
                await ExecuteFaultEventAsync(sessionId, evt);
                break;

            case "controller":
                await ExecuteControllerEventAsync(sessionId, evt);
                break;

            case "note":
                await ExecuteNoteEventAsync(sessionId, evt);
                break;

            default:
                _logger.LogWarning("Unknown event type: {EventType}", evt.Type);
                break;
        }
    }

    private async Task ExecuteSetEventAsync(string sessionId, ScenarioEvent evt)
    {
        if (string.IsNullOrEmpty(evt.Target) || evt.Payload == null)
        {
            throw new ArgumentException("Set event requires target and payload");
        }

        var writeRequest = new WriteTagRequest
        {
            Value = evt.Payload,
            User = "scenario",
            Mode = "automated"
        };

        await _sessionManager.WriteTagAsync(sessionId, evt.Target, writeRequest);
    }

    private async Task ExecuteFaultEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Faults are similar to set events but logged differently
        if (string.IsNullOrEmpty(evt.Target) || evt.Payload == null)
        {
            throw new ArgumentException("Fault event requires target and payload");
        }

        var writeRequest = new WriteTagRequest
        {
            Value = evt.Payload,
            User = "scenario_fault",
            Mode = "fault_injection"
        };

        await _sessionManager.WriteTagAsync(sessionId, evt.Target, writeRequest);

        _logger.LogWarning("Injected fault: {Target} = {Payload}", evt.Target, evt.Payload);
    }

    private async Task ExecuteControllerEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Controller events modify controller parameters
        if (string.IsNullOrEmpty(evt.Target) || evt.Payload == null)
        {
            throw new ArgumentException("Controller event requires target and payload");
        }

        var writeRequest = new WriteTagRequest
        {
            Value = evt.Payload,
            User = "scenario_controller",
            Mode = "automated"
        };

        await _sessionManager.WriteTagAsync(sessionId, evt.Target, writeRequest);
    }

    private Task ExecuteNoteEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Notes are informational and just logged
        if (evt.Payload != null)
        {
            _logger.LogInformation("Scenario note: {Note}", evt.Payload.ToString());
        }

        return Task.CompletedTask;
    }
}
