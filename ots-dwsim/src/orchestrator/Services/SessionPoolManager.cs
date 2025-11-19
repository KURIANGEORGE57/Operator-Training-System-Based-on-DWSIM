using DWSIM.OTS.Orchestrator.Models;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

namespace DWSIM.OTS.Orchestrator.Services;

/// <summary>
/// Manages a pool of DWSIM simulation sessions across multiple hosts
/// </summary>
public class SessionPoolManager : ISessionPoolManager
{
    private readonly PoolConfig _config;
    private readonly ITimescaleDbLogger _logger;
    private readonly ILogger<SessionPoolManager> _appLogger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ManagedSession> _sessions = new();
    private readonly ConcurrentDictionary<string, HostStatus> _hostStatuses = new();
    private readonly Timer _healthCheckTimer;

    public SessionPoolManager(
        PoolConfig config,
        ITimescaleDbLogger logger,
        ILogger<SessionPoolManager> appLogger,
        HttpClient httpClient)
    {
        _config = config;
        _logger = logger;
        _appLogger = appLogger;
        _httpClient = httpClient;

        // Initialize host statuses
        foreach (var hostUrl in _config.HostUrls)
        {
            _hostStatuses[hostUrl] = new HostStatus
            {
                HostUrl = hostUrl,
                IsHealthy = false,
                ActiveSessions = 0,
                MaxSessions = _config.MaxSessionsPerHost,
                LastChecked = DateTime.MinValue
            };
        }

        // Start health check timer
        _healthCheckTimer = new Timer(
            async _ => await PerformHealthChecksAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds)
        );

        _appLogger.LogInformation("SessionPoolManager initialized with {HostCount} hosts", _config.HostUrls.Count);
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request)
    {
        // Find available host
        var host = await SelectAvailableHostAsync();
        if (host == null)
        {
            throw new InvalidOperationException("No available hosts in the pool");
        }

        _appLogger.LogInformation("Creating session on host {HostUrl}", host.HostUrl);

        try
        {
            // Call DWSIM host API to create session
            var response = await _httpClient.PostAsJsonAsync(
                $"{host.HostUrl}/api/v1/sessions",
                new
                {
                    flowsheet = request.FlowsheetName,
                    session_name = request.SessionName ?? $"session_{Guid.NewGuid():N}",
                    metadata = request.Metadata ?? new Dictionary<string, object>()
                });

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var sessionId = result.GetProperty("session_id").GetString()!;

            // Create managed session
            var session = new ManagedSession
            {
                SessionId = sessionId,
                HostUrl = host.HostUrl,
                FlowsheetName = request.FlowsheetName,
                SessionName = request.SessionName,
                OperatorId = request.OperatorId,
                ScenarioId = request.ScenarioId,
                Status = SessionStatus.Created,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _sessions[sessionId] = session;

            // Update host status
            host.ActiveSessions++;

            // Log to database
            await _logger.UpsertSessionAsync(session);
            await _logger.LogEventAsync(sessionId, new EventLogEntry
            {
                Timestamp = DateTime.UtcNow,
                EventType = "session_created",
                SimTime = 0,
                OperatorId = request.OperatorId,
                Metadata = new Dictionary<string, object>
                {
                    ["host_url"] = host.HostUrl,
                    ["flowsheet"] = request.FlowsheetName
                }
            });

            _appLogger.LogInformation("Session {SessionId} created successfully on {HostUrl}", sessionId, host.HostUrl);

            return new CreateSessionResponse
            {
                SessionId = sessionId,
                HostUrl = host.HostUrl,
                Status = SessionStatus.Created,
                CreatedAt = session.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to create session on host {HostUrl}", host.HostUrl);
            host.IsHealthy = false;
            throw;
        }
    }

    public Task<ManagedSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<List<SessionListItem>> ListSessionsAsync(SessionStatus? filterByStatus = null)
    {
        var sessions = _sessions.Values
            .Where(s => filterByStatus == null || s.Status == filterByStatus)
            .Select(s => new SessionListItem
            {
                SessionId = s.SessionId,
                SessionName = s.SessionName ?? s.SessionId,
                OperatorId = s.OperatorId,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                Duration = s.EndedAt.HasValue
                    ? s.EndedAt.Value - s.CreatedAt
                    : DateTime.UtcNow - s.CreatedAt
            })
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(sessions);
    }

    public async Task<SessionControlResponse> StartSessionAsync(string sessionId, StartSessionRequest request)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{session.HostUrl}/api/v1/sessions/{sessionId}/start",
                new
                {
                    start_mode = request.StartMode,
                    time_factor = request.TimeFactor
                });

            response.EnsureSuccessStatusCode();

            session.Status = SessionStatus.Running;
            session.StartedAt = DateTime.UtcNow;

            await _logger.UpsertSessionAsync(session);
            await _logger.LogEventAsync(sessionId, new EventLogEntry
            {
                Timestamp = DateTime.UtcNow,
                EventType = "session_started",
                SimTime = 0,
                OperatorId = session.OperatorId,
                Metadata = new Dictionary<string, object>
                {
                    ["start_mode"] = request.StartMode,
                    ["time_factor"] = request.TimeFactor ?? 1.0
                }
            });

            _appLogger.LogInformation("Session {SessionId} started", sessionId);

            return new SessionControlResponse
            {
                SessionId = sessionId,
                Status = SessionStatus.Running,
                Message = "Session started successfully"
            };
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to start session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<SessionControlResponse> PauseSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        try
        {
            var response = await _httpClient.PostAsync(
                $"{session.HostUrl}/api/v1/sessions/{sessionId}/pause",
                null);

            response.EnsureSuccessStatusCode();

            session.Status = SessionStatus.Paused;

            await _logger.UpsertSessionAsync(session);
            await _logger.LogEventAsync(sessionId, new EventLogEntry
            {
                Timestamp = DateTime.UtcNow,
                EventType = "session_paused",
                SimTime = 0,
                OperatorId = session.OperatorId
            });

            _appLogger.LogInformation("Session {SessionId} paused", sessionId);

            return new SessionControlResponse
            {
                SessionId = sessionId,
                Status = SessionStatus.Paused,
                Message = "Session paused successfully"
            };
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to pause session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<SessionControlResponse> StopSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        try
        {
            var response = await _httpClient.PostAsync(
                $"{session.HostUrl}/api/v1/sessions/{sessionId}/stop",
                null);

            response.EnsureSuccessStatusCode();

            session.Status = SessionStatus.Stopped;
            session.EndedAt = DateTime.UtcNow;

            // Update host status
            if (_hostStatuses.TryGetValue(session.HostUrl, out var host))
            {
                host.ActiveSessions = Math.Max(0, host.ActiveSessions - 1);
            }

            await _logger.UpsertSessionAsync(session);
            await _logger.LogEventAsync(sessionId, new EventLogEntry
            {
                Timestamp = DateTime.UtcNow,
                EventType = "session_stopped",
                SimTime = 0,
                OperatorId = session.OperatorId
            });

            _appLogger.LogInformation("Session {SessionId} stopped", sessionId);

            return new SessionControlResponse
            {
                SessionId = sessionId,
                Status = SessionStatus.Stopped,
                Message = "Session stopped successfully"
            };
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to stop session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            return false;
        }

        try
        {
            // Stop session first if running
            if (session.Status == SessionStatus.Running || session.Status == SessionStatus.Paused)
            {
                await StopSessionAsync(sessionId);
            }

            _sessions.TryRemove(sessionId, out _);

            _appLogger.LogInformation("Session {SessionId} deleted", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<TagValueResponse> ReadTagAsync(string sessionId, string tagPath)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        try
        {
            var response = await _httpClient.GetAsync(
                $"{session.HostUrl}/api/v1/sessions/{sessionId}/tags/{tagPath}");

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            return new TagValueResponse
            {
                TagPath = tagPath,
                Value = result.GetProperty("value").GetDouble(),
                Unit = result.TryGetProperty("unit", out var unit) ? unit.GetString() ?? "" : "",
                Quality = "GOOD",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to read tag {TagPath} from session {SessionId}", tagPath, sessionId);
            throw;
        }
    }

    public async Task<TagValueResponse> WriteTagAsync(string sessionId, string tagPath, object value, string? operatorId = null)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        try
        {
            // Read old value first
            TagValueResponse? oldTag = null;
            try
            {
                oldTag = await ReadTagAsync(sessionId, tagPath);
            }
            catch
            {
                // Ignore if can't read old value
            }

            // Write new value
            var response = await _httpClient.PostAsJsonAsync(
                $"{session.HostUrl}/api/v1/sessions/{sessionId}/tags/{tagPath}",
                new { value });

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Log operator action
            await _logger.LogEventAsync(sessionId, new EventLogEntry
            {
                Timestamp = DateTime.UtcNow,
                EventType = "operator_action",
                SimTime = 0, // TODO: Get actual sim time
                OperatorId = operatorId ?? session.OperatorId,
                TagPath = tagPath,
                OldValue = oldTag?.Value as double?,
                NewValue = Convert.ToDouble(value),
                Metadata = new Dictionary<string, object>
                {
                    ["action"] = "tag_write"
                }
            });

            return new TagValueResponse
            {
                TagPath = tagPath,
                Value = value,
                Unit = result.TryGetProperty("unit", out var unit) ? unit.GetString() ?? "" : "",
                Quality = "GOOD",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Failed to write tag {TagPath} to session {SessionId}", tagPath, sessionId);
            throw;
        }
    }

    public async Task<List<EventLogEntry>> GetSessionEventsAsync(string sessionId)
    {
        return await _logger.GetSessionEventsAsync(sessionId);
    }

    public Task<SessionStatistics> GetStatisticsAsync()
    {
        var stats = new SessionStatistics
        {
            TotalSessions = _sessions.Count,
            ActiveSessions = _sessions.Values.Count(s => s.Status == SessionStatus.Running || s.Status == SessionStatus.Paused),
            AvailableHosts = _hostStatuses.Values.Count(h => h.IsHealthy),
            TotalCapacity = _hostStatuses.Values.Sum(h => h.MaxSessions),
            UtilizationPercent = 0
        };

        if (stats.TotalCapacity > 0)
        {
            stats.UtilizationPercent = (double)stats.ActiveSessions / stats.TotalCapacity * 100;
        }

        return Task.FromResult(stats);
    }

    public async Task<OrchestratorHealth> GetHealthAsync()
    {
        var stats = await GetStatisticsAsync();
        var dbHealthy = await _logger.IsHealthyAsync();

        return new OrchestratorHealth
        {
            Status = dbHealthy && _hostStatuses.Values.Any(h => h.IsHealthy) ? "healthy" : "degraded",
            Timestamp = DateTime.UtcNow,
            Statistics = stats,
            Hosts = _hostStatuses.Values.ToList()
        };
    }

    private async Task<HostStatus?> SelectAvailableHostAsync()
    {
        // Select host with lowest utilization
        var availableHosts = _hostStatuses.Values
            .Where(h => h.IsHealthy && h.ActiveSessions < h.MaxSessions)
            .OrderBy(h => h.ActiveSessions)
            .ToList();

        if (availableHosts.Count == 0)
        {
            _appLogger.LogWarning("No available hosts found");
            return null;
        }

        return availableHosts.First();
    }

    private async Task PerformHealthChecksAsync()
    {
        foreach (var host in _hostStatuses.Values)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{host.HostUrl}/health", new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
                host.IsHealthy = response.IsSuccessStatusCode;
                host.LastChecked = DateTime.UtcNow;

                if (!host.IsHealthy)
                {
                    _appLogger.LogWarning("Host {HostUrl} health check failed", host.HostUrl);
                }
            }
            catch (Exception ex)
            {
                host.IsHealthy = false;
                host.LastChecked = DateTime.UtcNow;
                _appLogger.LogError(ex, "Health check failed for host {HostUrl}", host.HostUrl);
            }
        }
    }
}
