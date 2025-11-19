using DWSIM.OTS.Orchestrator.Models;
using Npgsql;
using System.Text.Json;

namespace DWSIM.OTS.Orchestrator.Services;

/// <summary>
/// Service for logging events and process variables to TimescaleDB
/// </summary>
public class TimescaleDbLogger : ITimescaleDbLogger, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<TimescaleDbLogger> _logger;
    private readonly int _batchSize;
    private readonly Timer _flushTimer;

    private readonly Queue<PendingEvent> _eventQueue = new();
    private readonly Queue<PendingProcessVariable> _pvQueue = new();
    private readonly SemaphoreSlim _queueLock = new(1, 1);

    public TimescaleDbLogger(DatabaseConfig config, ILogger<TimescaleDbLogger> logger)
    {
        _connectionString = config.ConnectionString;
        _logger = logger;
        _batchSize = config.BatchSize;

        // Start background flush timer
        _flushTimer = new Timer(
            async _ => await FlushQueuesAsync(),
            null,
            TimeSpan.FromSeconds(config.FlushIntervalSeconds),
            TimeSpan.FromSeconds(config.FlushIntervalSeconds)
        );

        _logger.LogInformation("TimescaleDbLogger initialized with batch size {BatchSize}", _batchSize);
    }

    public async Task LogEventAsync(string sessionId, EventLogEntry entry)
    {
        await _queueLock.WaitAsync();
        try
        {
            _eventQueue.Enqueue(new PendingEvent
            {
                SessionId = Guid.Parse(sessionId),
                Entry = entry
            });

            if (_eventQueue.Count >= _batchSize)
            {
                await FlushEventsAsync();
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task LogEventsBatchAsync(string sessionId, IEnumerable<EventLogEntry> entries)
    {
        await _queueLock.WaitAsync();
        try
        {
            var sessionGuid = Guid.Parse(sessionId);
            foreach (var entry in entries)
            {
                _eventQueue.Enqueue(new PendingEvent
                {
                    SessionId = sessionGuid,
                    Entry = entry
                });
            }

            if (_eventQueue.Count >= _batchSize)
            {
                await FlushEventsAsync();
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task LogProcessVariableAsync(string sessionId, string tagPath, double value, double simTime)
    {
        await _queueLock.WaitAsync();
        try
        {
            _pvQueue.Enqueue(new PendingProcessVariable
            {
                SessionId = Guid.Parse(sessionId),
                TagPath = tagPath,
                Value = value,
                SimTime = simTime,
                Timestamp = DateTime.UtcNow
            });

            if (_pvQueue.Count >= _batchSize)
            {
                await FlushProcessVariablesAsync();
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task LogProcessVariablesBatchAsync(string sessionId, IEnumerable<(string tagPath, double value, double simTime)> variables)
    {
        await _queueLock.WaitAsync();
        try
        {
            var sessionGuid = Guid.Parse(sessionId);
            var timestamp = DateTime.UtcNow;

            foreach (var (tagPath, value, simTime) in variables)
            {
                _pvQueue.Enqueue(new PendingProcessVariable
                {
                    SessionId = sessionGuid,
                    TagPath = tagPath,
                    Value = value,
                    SimTime = simTime,
                    Timestamp = timestamp
                });
            }

            if (_pvQueue.Count >= _batchSize)
            {
                await FlushProcessVariablesAsync();
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task UpsertSessionAsync(ManagedSession session)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO sessions (session_id, flowsheet_name, session_name, scenario_id, operator_id, start_time, end_time, status, metadata)
            VALUES (@session_id, @flowsheet_name, @session_name, @scenario_id, @operator_id, @start_time, @end_time, @status, @metadata::jsonb)
            ON CONFLICT (session_id) DO UPDATE SET
                session_name = EXCLUDED.session_name,
                scenario_id = EXCLUDED.scenario_id,
                operator_id = EXCLUDED.operator_id,
                end_time = EXCLUDED.end_time,
                status = EXCLUDED.status,
                metadata = EXCLUDED.metadata";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("session_id", Guid.Parse(session.SessionId));
        cmd.Parameters.AddWithValue("flowsheet_name", session.FlowsheetName);
        cmd.Parameters.AddWithValue("session_name", session.SessionName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("scenario_id", session.ScenarioId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("operator_id", session.OperatorId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("start_time", session.StartedAt ?? session.CreatedAt);
        cmd.Parameters.AddWithValue("end_time", session.EndedAt ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("status", session.Status.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(session.Metadata));

        await cmd.ExecuteNonQueryAsync();
        _logger.LogDebug("Upserted session {SessionId} to database", session.SessionId);
    }

    public async Task<List<EventLogEntry>> GetSessionEventsAsync(string sessionId, DateTime? startTime = null, DateTime? endTime = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT time, event_type, sim_time, operator_id, tag_path, old_value, new_value, metadata
            FROM session_events
            WHERE session_id = @session_id
            AND (@start_time IS NULL OR time >= @start_time)
            AND (@end_time IS NULL OR time <= @end_time)
            ORDER BY time ASC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("session_id", Guid.Parse(sessionId));
        cmd.Parameters.AddWithValue("start_time", startTime ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("end_time", endTime ?? (object)DBNull.Value);

        var events = new List<EventLogEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            events.Add(new EventLogEntry
            {
                Timestamp = reader.GetDateTime(0),
                EventType = reader.GetString(1),
                SimTime = reader.GetDouble(2),
                OperatorId = reader.IsDBNull(3) ? null : reader.GetString(3),
                TagPath = reader.IsDBNull(4) ? null : reader.GetString(4),
                OldValue = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                NewValue = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                Metadata = reader.IsDBNull(7) ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(7))
            });
        }

        return events;
    }

    public async Task<List<(DateTime time, double value)>> GetProcessVariableHistoryAsync(
        string sessionId,
        string tagPath,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT time, value
            FROM process_variables
            WHERE session_id = @session_id
            AND tag_path = @tag_path
            AND (@start_time IS NULL OR time >= @start_time)
            AND (@end_time IS NULL OR time <= @end_time)
            ORDER BY time ASC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("session_id", Guid.Parse(sessionId));
        cmd.Parameters.AddWithValue("tag_path", tagPath);
        cmd.Parameters.AddWithValue("start_time", startTime ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("end_time", endTime ?? (object)DBNull.Value);

        var history = new List<(DateTime, double)>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            history.Add((reader.GetDateTime(0), reader.GetDouble(1)));
        }

        return history;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return false;
        }
    }

    private async Task FlushQueuesAsync()
    {
        await _queueLock.WaitAsync();
        try
        {
            await FlushEventsAsync();
            await FlushProcessVariablesAsync();
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private async Task FlushEventsAsync()
    {
        if (_eventQueue.Count == 0) return;

        var events = new List<PendingEvent>();
        while (_eventQueue.Count > 0 && events.Count < _batchSize)
        {
            events.Add(_eventQueue.Dequeue());
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO session_events (time, session_id, event_type, sim_time, operator_id, tag_path, old_value, new_value, metadata)
                VALUES (@time, @session_id, @event_type, @sim_time, @operator_id, @tag_path, @old_value, @new_value, @metadata::jsonb)";

            await using var batch = new NpgsqlBatch(conn);

            foreach (var evt in events)
            {
                var cmd = new NpgsqlBatchCommand(sql);
                cmd.Parameters.AddWithValue("time", evt.Entry.Timestamp);
                cmd.Parameters.AddWithValue("session_id", evt.SessionId);
                cmd.Parameters.AddWithValue("event_type", evt.Entry.EventType);
                cmd.Parameters.AddWithValue("sim_time", evt.Entry.SimTime);
                cmd.Parameters.AddWithValue("operator_id", evt.Entry.OperatorId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("tag_path", evt.Entry.TagPath ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("old_value", evt.Entry.OldValue ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("new_value", evt.Entry.NewValue ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("metadata", evt.Entry.Metadata != null ? JsonSerializer.Serialize(evt.Entry.Metadata) : (object)DBNull.Value);

                batch.BatchCommands.Add(cmd);
            }

            await batch.ExecuteNonQueryAsync();
            _logger.LogDebug("Flushed {Count} events to database", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush events to database");

            // Re-queue failed events
            foreach (var evt in events)
            {
                _eventQueue.Enqueue(evt);
            }
        }
    }

    private async Task FlushProcessVariablesAsync()
    {
        if (_pvQueue.Count == 0) return;

        var pvs = new List<PendingProcessVariable>();
        while (_pvQueue.Count > 0 && pvs.Count < _batchSize)
        {
            pvs.Add(_pvQueue.Dequeue());
        }

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO process_variables (time, session_id, tag_path, value, sim_time, quality)
                VALUES (@time, @session_id, @tag_path, @value, @sim_time, @quality)";

            await using var batch = new NpgsqlBatch(conn);

            foreach (var pv in pvs)
            {
                var cmd = new NpgsqlBatchCommand(sql);
                cmd.Parameters.AddWithValue("time", pv.Timestamp);
                cmd.Parameters.AddWithValue("session_id", pv.SessionId);
                cmd.Parameters.AddWithValue("tag_path", pv.TagPath);
                cmd.Parameters.AddWithValue("value", pv.Value);
                cmd.Parameters.AddWithValue("sim_time", pv.SimTime);
                cmd.Parameters.AddWithValue("quality", "GOOD");

                batch.BatchCommands.Add(cmd);
            }

            await batch.ExecuteNonQueryAsync();
            _logger.LogDebug("Flushed {Count} process variables to database", pvs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush process variables to database");

            // Re-queue failed pvs
            foreach (var pv in pvs)
            {
                _pvQueue.Enqueue(pv);
            }
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        FlushQueuesAsync().GetAwaiter().GetResult();
        _queueLock.Dispose();
    }

    private class PendingEvent
    {
        public required Guid SessionId { get; set; }
        public required EventLogEntry Entry { get; set; }
    }

    private class PendingProcessVariable
    {
        public required Guid SessionId { get; set; }
        public required string TagPath { get; set; }
        public required double Value { get; set; }
        public required double SimTime { get; set; }
        public required DateTime Timestamp { get; set; }
    }
}
