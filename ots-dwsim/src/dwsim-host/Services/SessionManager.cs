using DWSIM.OTS.SimulationHost.Models;
using DWSIM.Interfaces;
using System.Collections.Concurrent;

namespace DWSIM.OTS.SimulationHost.Services;

public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, SimulationSession> _sessions = new();
    private readonly IFlowsheetRepository _flowsheetRepository;
    private readonly ILogger<SessionManager> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public SessionManager(IFlowsheetRepository flowsheetRepository, ILogger<SessionManager> logger)
    {
        _flowsheetRepository = flowsheetRepository;
        _logger = logger;
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();
            var flowsheetPath = await _flowsheetRepository.ResolveFlowsheetPathAsync(request.Flowsheet);

            if (flowsheetPath == null)
            {
                throw new FileNotFoundException($"Flowsheet not found: {request.Flowsheet}");
            }

            var session = new SimulationSession
            {
                SessionId = sessionId,
                SessionName = string.IsNullOrEmpty(request.SessionName) ? $"Session-{sessionId[..8]}" : request.SessionName,
                Status = SessionStatus.Created,
                CreatedAt = DateTime.UtcNow,
                SimTime = DateTime.UtcNow,
                TimeFactor = 1.0,
                FlowsheetPath = flowsheetPath,
                Seed = request.Seed,
                Environment = request.Environment ?? new Dictionary<string, object>(),
                MaxRealTimeSec = request.MaxRealTimeSec,
                EventLog = new List<EventLogEntry>()
            };

            // Initialize DWSIM automation interface in a separate task
            await Task.Run(() =>
            {
                try
                {
                    session.AutomationInterface = new DWSIM.Automation.Automation2();
                    session.Flowsheet = session.AutomationInterface.LoadFlowsheet(flowsheetPath);

                    _logger.LogInformation("Loaded flowsheet for session {SessionId}: {FlowsheetPath}",
                        sessionId, flowsheetPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load flowsheet for session {SessionId}", sessionId);
                    session.Status = SessionStatus.Error;
                    throw;
                }
            });

            _sessions.TryAdd(sessionId, session);

            _logger.LogInformation("Created session {SessionId} with flowsheet {Flowsheet}",
                sessionId, request.Flowsheet);

            return new CreateSessionResponse
            {
                SessionId = sessionId,
                Status = session.Status,
                CreatedAt = session.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            throw;
        }
    }

    public Task<SessionInfo?> GetSessionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult<SessionInfo?>(session.ToSessionInfo());
        }
        return Task.FromResult<SessionInfo?>(null);
    }

    public Task<List<SessionInfo>> GetAllSessionsAsync()
    {
        var sessions = _sessions.Values.Select(s => s.ToSessionInfo()).ToList();
        return Task.FromResult(sessions);
    }

    public async Task<StartSessionResponse> StartSessionAsync(string sessionId, StartSessionRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        await Task.Run(() =>
        {
            try
            {
                if (session.Flowsheet == null)
                {
                    throw new InvalidOperationException("Flowsheet not loaded");
                }

                // Calculate the flowsheet
                var errors = session.AutomationInterface?.CalculateFlowsheet2(session.Flowsheet);

                if (errors != null && errors.Count > 0)
                {
                    _logger.LogWarning("Session {SessionId} started with {ErrorCount} calculation errors",
                        sessionId, errors.Count);
                    foreach (var error in errors)
                    {
                        _logger.LogWarning("  Error: {Message}", error.Message);
                    }
                }

                session.Status = SessionStatus.Running;
                session.StartedAt = DateTime.UtcNow;
                session.SimTime = DateTime.UtcNow;
                session.TimeFactor = 1.0;

                // Log start event
                session.EventLog.Add(new EventLogEntry
                {
                    Type = "session_start",
                    SimTime = session.SimTime,
                    RealTime = DateTime.UtcNow
                });

                _logger.LogInformation("Started session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting session {SessionId}", sessionId);
                session.Status = SessionStatus.Error;
                throw;
            }
        });

        return new StartSessionResponse
        {
            SessionId = sessionId,
            Status = session.Status,
            SimTime = session.SimTime,
            TimeFactor = session.TimeFactor
        };
    }

    public Task<SessionInfo> PauseSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        session.Status = SessionStatus.Paused;

        session.EventLog.Add(new EventLogEntry
        {
            Type = "session_pause",
            SimTime = session.SimTime,
            RealTime = DateTime.UtcNow
        });

        _logger.LogInformation("Paused session {SessionId}", sessionId);

        return Task.FromResult(session.ToSessionInfo());
    }

    public Task<SessionInfo> StopSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        session.Status = SessionStatus.Stopped;
        session.StoppedAt = DateTime.UtcNow;

        session.EventLog.Add(new EventLogEntry
        {
            Type = "session_stop",
            SimTime = session.SimTime,
            RealTime = DateTime.UtcNow
        });

        // Clean up resources
        session.AutomationInterface?.ReleaseResources();

        _logger.LogInformation("Stopped session {SessionId}", sessionId);

        return Task.FromResult(session.ToSessionInfo());
    }

    public Task<SnapshotResponse> CreateSnapshotAsync(string sessionId, SnapshotRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        if (session.Flowsheet == null || session.AutomationInterface == null)
        {
            throw new InvalidOperationException("Flowsheet not loaded");
        }

        var snapshotId = $"snap-{Guid.NewGuid().ToString()[..8]}";
        var savedAt = DateTime.UtcNow;

        try
        {
            // Serialize the flowsheet to XML
            var flowsheetXml = session.Flowsheet.SaveToXML();

            // Create snapshot data structure
            var snapshot = new Snapshot
            {
                SnapshotId = snapshotId,
                Name = request.Name,
                SessionId = sessionId,
                CreatedAt = savedAt,
                SimTime = session.SimTime,
                TimeFactor = session.TimeFactor,
                FlowsheetXml = flowsheetXml.ToString(),
                Environment = new Dictionary<string, object>(session.Environment)
            };

            // Store snapshot in session
            if (session.Snapshots == null)
            {
                session.Snapshots = new Dictionary<string, Snapshot>();
            }
            session.Snapshots[snapshotId] = snapshot;

            _logger.LogInformation("Created snapshot {SnapshotId} for session {SessionId} with name {Name}",
                snapshotId, sessionId, request.Name);

            session.EventLog.Add(new EventLogEntry
            {
                Type = "snapshot_created",
                Target = request.Name,
                Value = snapshotId,
                SimTime = session.SimTime,
                RealTime = DateTime.UtcNow
            });

            return Task.FromResult(new SnapshotResponse
            {
                SnapshotId = snapshotId,
                SavedAt = savedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot for session {SessionId}", sessionId);
            throw;
        }
    }

    public Task<SessionInfo> RestoreSnapshotAsync(string sessionId, RestoreSnapshotRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        if (session.Snapshots == null || !session.Snapshots.ContainsKey(request.SnapshotId))
        {
            throw new KeyNotFoundException($"Snapshot {request.SnapshotId} not found for session {sessionId}");
        }

        try
        {
            var snapshot = session.Snapshots[request.SnapshotId];

            _logger.LogInformation("Restoring snapshot {SnapshotId} for session {SessionId}",
                request.SnapshotId, sessionId);

            // Parse the XML and load into the flowsheet
            var xdoc = System.Xml.Linq.XDocument.Parse(snapshot.FlowsheetXml);

            // Load the flowsheet state from XML
            if (session.Flowsheet != null)
            {
                session.Flowsheet.LoadFromXML(xdoc);
            }

            // Restore session state
            session.SimTime = snapshot.SimTime;
            session.TimeFactor = snapshot.TimeFactor;
            session.Environment = new Dictionary<string, object>(snapshot.Environment);

            _logger.LogInformation("Successfully restored snapshot {SnapshotId}", request.SnapshotId);

            session.EventLog.Add(new EventLogEntry
            {
                Type = "snapshot_restored",
                Target = request.SnapshotId,
                Value = snapshot.Name,
                SimTime = session.SimTime,
                RealTime = DateTime.UtcNow
            });

            return Task.FromResult(session.ToSessionInfo());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring snapshot {SnapshotId} for session {SessionId}",
                request.SnapshotId, sessionId);
            throw;
        }
    }

    public async Task<StartSessionResponse> StepSessionAsync(string sessionId, TimeStepRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        await Task.Run(() =>
        {
            // Advance simulation time
            session.SimTime = session.SimTime.AddSeconds(request.Seconds);

            // Recalculate flowsheet
            if (session.Flowsheet != null && session.AutomationInterface != null)
            {
                var errors = session.AutomationInterface.CalculateFlowsheet2(session.Flowsheet);
                if (errors != null && errors.Count > 0)
                {
                    _logger.LogWarning("Step calculation had {ErrorCount} errors", errors.Count);
                }
            }

            _logger.LogDebug("Stepped session {SessionId} by {Seconds}s to {SimTime}",
                sessionId, request.Seconds, session.SimTime);
        });

        return new StartSessionResponse
        {
            SessionId = sessionId,
            Status = session.Status,
            SimTime = session.SimTime,
            TimeFactor = session.TimeFactor
        };
    }

    public Task<TimeFactorResponse> SetTimeFactorAsync(string sessionId, TimeFactorRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        session.TimeFactor = request.Factor;

        session.EventLog.Add(new EventLogEntry
        {
            Type = "time_factor_changed",
            Value = request.Factor,
            SimTime = session.SimTime,
            RealTime = DateTime.UtcNow
        });

        _logger.LogInformation("Set time factor to {Factor} for session {SessionId}",
            request.Factor, sessionId);

        return Task.FromResult(new TimeFactorResponse
        {
            TimeFactor = session.TimeFactor
        });
    }

    public Task<TagValue?> ReadTagAsync(string sessionId, string tagPath)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        if (session.Flowsheet == null)
        {
            throw new InvalidOperationException("Flowsheet not loaded");
        }

        try
        {
            // Parse tag path: Streams.Feed.Temperature or Units.Reactor1.Temperature
            var parts = tagPath.Split('.');

            if (parts.Length < 3)
            {
                throw new ArgumentException($"Invalid tag path format. Expected: Category.ObjectName.PropertyName, got: {tagPath}");
            }

            var category = parts[0]; // Streams, Units, Controllers, etc.
            var objectName = parts[1];
            var propertyName = parts[2];

            _logger.LogDebug("Reading tag {TagPath} from session {SessionId}", tagPath, sessionId);

            // Find the simulation object by name
            ISimulationObject? simObject = null;

            // Try to find object in SimulationObjects dictionary
            foreach (var kvp in session.Flowsheet.SimulationObjects)
            {
                if (kvp.Value.Name == objectName)
                {
                    simObject = kvp.Value;
                    break;
                }
            }

            if (simObject == null)
            {
                _logger.LogWarning("Object {ObjectName} not found in flowsheet", objectName);
                return Task.FromResult<TagValue?>(null);
            }

            // Map common property names to DWSIM property codes
            string propertyCode = MapPropertyNameToCode(propertyName);

            // Read the property value using DWSIM API
            object? value = simObject.GetPropertyValue(propertyCode, session.Flowsheet.FlowsheetOptions.SelectedUnitSystem);
            string? units = simObject.GetPropertyUnit(propertyCode, session.Flowsheet.FlowsheetOptions.SelectedUnitSystem);

            return Task.FromResult<TagValue?>(new TagValue
            {
                Tag = tagPath,
                Value = value,
                Units = units,
                SimTime = session.SimTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tag {TagPath} from session {SessionId}", tagPath, sessionId);
            return Task.FromResult<TagValue?>(null);
        }
    }

    public Task<WriteTagResponse> WriteTagAsync(string sessionId, string tagPath, WriteTagRequest request)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        if (session.Flowsheet == null)
        {
            throw new InvalidOperationException("Flowsheet not loaded");
        }

        try
        {
            // Parse tag path: Streams.Feed.Temperature or Units.Reactor1.Temperature
            var parts = tagPath.Split('.');

            if (parts.Length < 3)
            {
                throw new ArgumentException($"Invalid tag path format. Expected: Category.ObjectName.PropertyName, got: {tagPath}");
            }

            var category = parts[0];
            var objectName = parts[1];
            var propertyName = parts[2];

            _logger.LogInformation("Writing tag {TagPath} = {Value} in session {SessionId} by user {User}",
                tagPath, request.Value, sessionId, request.User);

            // Find the simulation object by name
            ISimulationObject? simObject = null;

            foreach (var kvp in session.Flowsheet.SimulationObjects)
            {
                if (kvp.Value.Name == objectName)
                {
                    simObject = kvp.Value;
                    break;
                }
            }

            if (simObject == null)
            {
                throw new ArgumentException($"Object {objectName} not found in flowsheet");
            }

            // Map common property names to DWSIM property codes
            string propertyCode = MapPropertyNameToCode(propertyName);

            // Write the property value using DWSIM API
            bool success = simObject.SetPropertyValue(propertyCode, request.Value, session.Flowsheet.FlowsheetOptions.SelectedUnitSystem);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to set property {propertyName} on {objectName}");
            }

            // Log the write event
            session.EventLog.Add(new EventLogEntry
            {
                Type = "operator_action",
                User = request.User,
                Action = "write",
                Target = tagPath,
                Value = request.Value,
                SimTime = session.SimTime,
                RealTime = DateTime.UtcNow
            });

            return Task.FromResult(new WriteTagResponse
            {
                Status = "ok",
                AppliedValue = request.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing tag {TagPath} in session {SessionId}", tagPath, sessionId);
            throw;
        }
    }

    public Task<List<EventLogEntry>> GetEventsAsync(string sessionId, DateTime? from, DateTime? to)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session not found: {sessionId}");
        }

        var events = session.EventLog.AsEnumerable();

        if (from.HasValue)
        {
            events = events.Where(e => e.SimTime >= from.Value);
        }

        if (to.HasValue)
        {
            events = events.Where(e => e.SimTime <= to.Value);
        }

        return Task.FromResult(events.ToList());
    }

    /// <summary>
    /// Maps common property names to DWSIM property codes.
    /// See: https://dwsim.org/wiki/index.php?title=Object_Property_Codes
    /// </summary>
    private string MapPropertyNameToCode(string propertyName)
    {
        return propertyName.ToLower() switch
        {
            // Material Stream properties
            "temperature" => "PROP_MS_0",       // Temperature
            "pressure" => "PROP_MS_1",          // Pressure
            "massflow" => "PROP_MS_2",          // Mass Flow
            "molarflow" => "PROP_MS_3",         // Molar Flow
            "volumeflow" => "PROP_MS_4",        // Volumetric Flow
            "enthalpy" => "PROP_MS_5",          // Molar Enthalpy
            "entropy" => "PROP_MS_6",           // Molar Entropy
            "density" => "PROP_MS_7",           // Density
            "molecularweight" => "PROP_MS_8",   // Molecular Weight
            "specificheat" => "PROP_MS_9",      // Specific Heat (Cp)

            // Energy Stream properties
            "energyflow" => "PROP_ES_0",        // Energy Flow

            // Unit Operation common properties
            "deltap" => "PROP_UO_0",            // Pressure Drop
            "heatload" => "PROP_UO_1",          // Heat Load/Duty
            "efficiency" => "PROP_UO_2",        // Efficiency
            "power" => "PROP_UO_3",             // Power

            // Pump/Compressor
            "dischargepressure" => "PROP_PP_0", // Discharge Pressure (Pump)
            "deltapress" => "PROP_PP_1",        // Delta P (Pump)

            // Controller properties
            "pv" => "PROP_CO_0",                // Process Variable
            "sp" => "PROP_CO_1",                // Setpoint
            "mv" => "PROP_CO_2",                // Manipulated Variable
            "kp" => "PROP_CO_3",                // Proportional Gain
            "ki" => "PROP_CO_4",                // Integral Time
            "kd" => "PROP_CO_5",                // Derivative Time

            // Default - return as-is if not mapped
            _ => propertyName
        };
    }
}

// Internal session state class
internal class SimulationSession
{
    public required string SessionId { get; set; }
    public required string SessionName { get; set; }
    public SessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public DateTime SimTime { get; set; }
    public double TimeFactor { get; set; }
    public required string FlowsheetPath { get; set; }
    public int Seed { get; set; }
    public Dictionary<string, object> Environment { get; set; } = new();
    public int MaxRealTimeSec { get; set; }
    public List<EventLogEntry> EventLog { get; set; } = new();

    // DWSIM objects
    public DWSIM.Automation.Automation2? AutomationInterface { get; set; }
    public IFlowsheet? Flowsheet { get; set; }

    // Snapshots
    public Dictionary<string, Snapshot>? Snapshots { get; set; }

    public SessionInfo ToSessionInfo()
    {
        return new SessionInfo
        {
            SessionId = SessionId,
            SessionName = SessionName,
            Status = Status,
            CreatedAt = CreatedAt,
            StartedAt = StartedAt,
            StoppedAt = StoppedAt,
            SimTime = SimTime,
            TimeFactor = TimeFactor,
            FlowsheetPath = FlowsheetPath
        };
    }
}

// Snapshot data class
internal class Snapshot
{
    public required string SnapshotId { get; set; }
    public required string Name { get; set; }
    public required string SessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SimTime { get; set; }
    public double TimeFactor { get; set; }
    public required string FlowsheetXml { get; set; }
    public Dictionary<string, object> Environment { get; set; } = new();
}
