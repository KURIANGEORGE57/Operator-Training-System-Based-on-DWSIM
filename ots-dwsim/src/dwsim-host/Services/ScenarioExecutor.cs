using DWSIM.OTS.SimulationHost.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DWSIM.OTS.SimulationHost.Services;

/// <summary>
/// Service for executing training scenarios with precise timing
/// </summary>
public class ScenarioExecutor : IScenarioExecutor
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ScenarioExecutor> _logger;
    private readonly ConcurrentDictionary<string, ScenarioRun> _runs = new();

    public ScenarioExecutor(ISessionManager sessionManager, ILogger<ScenarioExecutor> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<Scenario> LoadScenarioAsync(string scenarioPath)
    {
        try
        {
            if (!File.Exists(scenarioPath))
            {
                throw new FileNotFoundException($"Scenario file not found: {scenarioPath}");
            }

            var json = await File.ReadAllTextAsync(scenarioPath);
            var scenario = JsonSerializer.Deserialize<Scenario>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (scenario == null)
            {
                throw new InvalidDataException($"Failed to deserialize scenario from {scenarioPath}");
            }

            _logger.LogInformation("Loaded scenario {ScenarioId}: {Title} with {EventCount} events",
                scenario.ScenarioId, scenario.Title, scenario.Events.Count);

            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scenario from {Path}", scenarioPath);
            throw;
        }
    }

    public async Task<RunScenarioResponse> RunScenarioAsync(string sessionId, string scenarioPath, double timeFactor = 1.0)
    {
        // Load scenario
        var scenario = await LoadScenarioAsync(scenarioPath);

        // Verify session exists
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Create scenario run
        var runId = $"run-{Guid.NewGuid().ToString()[..8]}";
        var run = new ScenarioRun
        {
            RunId = runId,
            SessionId = sessionId,
            ScenarioId = scenario.ScenarioId,
            Scenario = scenario,
            Status = ScenarioRunStatus.Initializing,
            StartedAt = DateTime.UtcNow,
            ElapsedSimTimeSeconds = 0,
            EventsExecuted = 0,
            TotalEvents = scenario.Events.Count,
            CancellationTokenSource = new CancellationTokenSource()
        };

        _runs.TryAdd(runId, run);

        _logger.LogInformation("Starting scenario run {RunId} for session {SessionId}: {ScenarioId}",
            runId, sessionId, scenario.ScenarioId);

        // Start execution in background
        run.ExecutionTask = Task.Run(() => ExecuteScenarioAsync(run, timeFactor), run.CancellationTokenSource.Token);

        run.Status = ScenarioRunStatus.Running;

        return new RunScenarioResponse
        {
            RunId = runId,
            ScenarioId = scenario.ScenarioId,
            Status = run.Status,
            TotalEvents = run.TotalEvents,
            StartedAt = run.StartedAt
        };
    }

    public async Task StopScenarioAsync(string runId)
    {
        if (_runs.TryGetValue(runId, out var run))
        {
            _logger.LogInformation("Stopping scenario run {RunId}", runId);

            run.CancellationTokenSource?.Cancel();
            run.Status = ScenarioRunStatus.Stopped;
            run.CompletedAt = DateTime.UtcNow;

            if (run.ExecutionTask != null)
            {
                try
                {
                    await run.ExecutionTask;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Scenario run {RunId} cancelled successfully", runId);
                }
            }
        }
        else
        {
            throw new KeyNotFoundException($"Scenario run {runId} not found");
        }
    }

    public Task<ScenarioStatusResponse?> GetStatusAsync(string runId)
    {
        if (_runs.TryGetValue(runId, out var run))
        {
            var progress = run.TotalEvents > 0
                ? (double)run.EventsExecuted / run.TotalEvents * 100.0
                : 0.0;

            return Task.FromResult<ScenarioStatusResponse?>(new ScenarioStatusResponse
            {
                RunId = run.RunId,
                ScenarioId = run.ScenarioId,
                Status = run.Status,
                ElapsedSimTimeSeconds = run.ElapsedSimTimeSeconds,
                EventsExecuted = run.EventsExecuted,
                TotalEvents = run.TotalEvents,
                ProgressPercent = progress
            });
        }

        return Task.FromResult<ScenarioStatusResponse?>(null);
    }

    public Task<List<ScenarioStatusResponse>> ListRunsAsync()
    {
        var statuses = _runs.Values.Select(run =>
        {
            var progress = run.TotalEvents > 0
                ? (double)run.EventsExecuted / run.TotalEvents * 100.0
                : 0.0;

            return new ScenarioStatusResponse
            {
                RunId = run.RunId,
                ScenarioId = run.ScenarioId,
                Status = run.Status,
                ElapsedSimTimeSeconds = run.ElapsedSimTimeSeconds,
                EventsExecuted = run.EventsExecuted,
                TotalEvents = run.TotalEvents,
                ProgressPercent = progress
            };
        }).ToList();

        return Task.FromResult(statuses);
    }

    private async Task ExecuteScenarioAsync(ScenarioRun run, double timeFactor)
    {
        try
        {
            var scenario = run.Scenario;
            var sessionId = run.SessionId;
            var cancellationToken = run.CancellationTokenSource!.Token;

            _logger.LogInformation("Executing scenario {ScenarioId} for session {SessionId}",
                scenario.ScenarioId, sessionId);

            // Apply initial state
            await ApplyInitialStateAsync(sessionId, scenario.InitialState, cancellationToken);

            // Sort events by time
            var sortedEvents = scenario.Events.OrderBy(e => e.TimeSeconds).ToList();

            // Track repeating events
            var repeatingEvents = new List<(ScenarioEvent evt, double nextTime, int executed)>();

            double currentSimTime = 0.0;
            int eventIndex = 0;

            // Execute events
            while (eventIndex < sortedEvents.Count || repeatingEvents.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get next event time (either from main list or repeating events)
                double? nextEventTime = null;
                ScenarioEvent? nextEvent = null;
                bool isRepeat = false;
                int repeatIndex = -1;

                // Check main event list
                if (eventIndex < sortedEvents.Count)
                {
                    nextEventTime = sortedEvents[eventIndex].TimeSeconds;
                    nextEvent = sortedEvents[eventIndex];
                }

                // Check repeating events
                for (int i = 0; i < repeatingEvents.Count; i++)
                {
                    if (!nextEventTime.HasValue || repeatingEvents[i].nextTime < nextEventTime.Value)
                    {
                        nextEventTime = repeatingEvents[i].nextTime;
                        nextEvent = repeatingEvents[i].evt;
                        isRepeat = true;
                        repeatIndex = i;
                    }
                }

                if (nextEvent == null || !nextEventTime.HasValue)
                    break;

                // Wait until event time
                var waitTime = nextEventTime.Value - currentSimTime;
                if (waitTime > 0)
                {
                    // Wait in real time (adjusted by time factor)
                    var realWaitMs = (int)(waitTime * 1000.0 / timeFactor);
                    await Task.Delay(realWaitMs, cancellationToken);

                    currentSimTime = nextEventTime.Value;
                    run.ElapsedSimTimeSeconds = currentSimTime;
                }

                // Execute event
                await ExecuteEventAsync(sessionId, nextEvent, cancellationToken);

                run.EventsExecuted++;

                _logger.LogInformation("Executed event {Type} at t={Time}s for scenario run {RunId}",
                    nextEvent.Type, currentSimTime, run.RunId);

                // Handle event advancement
                if (isRepeat)
                {
                    // Update repeating event
                    var (evt, _, executed) = repeatingEvents[repeatIndex];
                    var newExecuted = executed + 1;

                    if (evt.Repeat!.Count.HasValue && newExecuted >= evt.Repeat.Count.Value)
                    {
                        // Remove completed repeating event
                        repeatingEvents.RemoveAt(repeatIndex);
                    }
                    else
                    {
                        // Schedule next occurrence
                        var nextTime = currentSimTime + evt.Repeat.IntervalSeconds;
                        repeatingEvents[repeatIndex] = (evt, nextTime, newExecuted);
                    }
                }
                else
                {
                    // Move to next event in main list
                    if (nextEvent.Repeat != null)
                    {
                        // Add to repeating events
                        var nextTime = currentSimTime + nextEvent.Repeat.IntervalSeconds;
                        repeatingEvents.Add((nextEvent, nextTime, 1));
                    }

                    eventIndex++;
                }
            }

            // Scenario completed successfully
            run.Status = ScenarioRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Scenario run {RunId} completed successfully. Executed {EventCount} events in {Duration}s sim time",
                run.RunId, run.EventsExecuted, run.ElapsedSimTimeSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scenario run {RunId} was cancelled", run.RunId);
            run.Status = ScenarioRunStatus.Stopped;
            run.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scenario run {RunId}", run.RunId);
            run.Status = ScenarioRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
        }
    }

    private async Task ApplyInitialStateAsync(string sessionId, Dictionary<string, object> initialState, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying initial state with {Count} values for session {SessionId}",
            initialState.Count, sessionId);

        foreach (var (tagPath, value) in initialState)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _sessionManager.WriteTagAsync(sessionId, tagPath, new WriteTagRequest
                {
                    Value = value,
                    User = "scenario_executor"
                });

                _logger.LogDebug("Set initial value {TagPath} = {Value}", tagPath, value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set initial value for {TagPath}", tagPath);
            }
        }
    }

    private async Task ExecuteEventAsync(string sessionId, ScenarioEvent evt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (evt.Type.ToLowerInvariant())
        {
            case "set":
                await HandleSetEventAsync(sessionId, evt);
                break;

            case "fault":
                await HandleFaultEventAsync(sessionId, evt);
                break;

            case "controller":
                await HandleControllerEventAsync(sessionId, evt);
                break;

            case "note":
                await HandleNoteEventAsync(sessionId, evt);
                break;

            case "random_fault":
                await HandleRandomFaultEventAsync(sessionId, evt);
                break;

            case "alarm":
                await HandleAlarmEventAsync(sessionId, evt);
                break;

            case "operator_prompt":
                await HandleOperatorPromptEventAsync(sessionId, evt);
                break;

            default:
                _logger.LogWarning("Unknown event type: {EventType}", evt.Type);
                break;
        }
    }

    private async Task HandleSetEventAsync(string sessionId, ScenarioEvent evt)
    {
        if (evt.Target == null)
        {
            _logger.LogWarning("Set event missing target");
            return;
        }

        await _sessionManager.WriteTagAsync(sessionId, evt.Target, new WriteTagRequest
        {
            Value = evt.Payload,
            User = "scenario"
        });

        _logger.LogInformation("Set {Target} = {Value}", evt.Target, evt.Payload);
    }

    private async Task HandleFaultEventAsync(string sessionId, ScenarioEvent evt)
    {
        if (evt.Target == null)
        {
            _logger.LogWarning("Fault event missing target");
            return;
        }

        // Fault is similar to set, but logged differently
        await _sessionManager.WriteTagAsync(sessionId, evt.Target, new WriteTagRequest
        {
            Value = evt.Payload,
            User = "scenario_fault"
        });

        _logger.LogWarning("FAULT injected: {Target} = {Value}", evt.Target, evt.Payload);
    }

    private async Task HandleControllerEventAsync(string sessionId, ScenarioEvent evt)
    {
        if (evt.Target == null)
        {
            _logger.LogWarning("Controller event missing target");
            return;
        }

        // Controller event - modify controller parameters
        await _sessionManager.WriteTagAsync(sessionId, evt.Target, new WriteTagRequest
        {
            Value = evt.Payload,
            User = "scenario_controller"
        });

        _logger.LogInformation("Modified controller {Target} = {Value}", evt.Target, evt.Payload);
    }

    private Task HandleNoteEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Note event - just log for instructor
        _logger.LogInformation("SCENARIO NOTE: {Note}", evt.Payload);

        // TODO: Could send this to instructor UI or event log
        return Task.CompletedTask;
    }

    private async Task HandleRandomFaultEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Random fault - select a random fault from payload
        if (evt.Payload is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var options = jsonElement.EnumerateArray().ToList();
            if (options.Count > 0)
            {
                var random = new Random();
                var selectedOption = options[random.Next(options.Count)];

                // Apply the selected fault
                if (evt.Target != null)
                {
                    await _sessionManager.WriteTagAsync(sessionId, evt.Target, new WriteTagRequest
                    {
                        Value = selectedOption,
                        User = "scenario_random_fault"
                    });

                    _logger.LogWarning("RANDOM FAULT injected: {Target} = {Value}", evt.Target, selectedOption);
                }
            }
        }
        else
        {
            _logger.LogWarning("Random fault event has invalid payload format");
        }
    }

    private Task HandleAlarmEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Alarm event - trigger an alarm
        _logger.LogWarning("ALARM triggered: {Message}", evt.Payload);

        // TODO: Integrate with alarm system
        return Task.CompletedTask;
    }

    private Task HandleOperatorPromptEventAsync(string sessionId, ScenarioEvent evt)
    {
        // Operator prompt - display message to operator
        _logger.LogInformation("OPERATOR PROMPT: {Message}", evt.Payload);

        // TODO: Send to operator HMI
        return Task.CompletedTask;
    }
}
