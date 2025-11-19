-- DWSIM OTS TimescaleDB Initialization Script
-- Version: 1.0.0
-- Description: Creates hypertables for process variable logging and event tracking

-- Enable TimescaleDB extension
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- ============================================================================
-- Process Variables Table
-- ============================================================================
-- Stores time-series data for all process variables (temperature, pressure, etc.)
CREATE TABLE IF NOT EXISTS process_variables (
    time TIMESTAMPTZ NOT NULL,
    session_id UUID NOT NULL,
    tag_path TEXT NOT NULL,
    value DOUBLE PRECISION,
    sim_time DOUBLE PRECISION NOT NULL,  -- Simulation time in seconds
    quality TEXT DEFAULT 'GOOD'          -- OPC UA quality code
);

-- Create hypertable (partitioned by time)
SELECT create_hypertable(
    'process_variables',
    'time',
    if_not_exists => TRUE,
    chunk_time_interval => INTERVAL '1 day'
);

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_pv_session_id
    ON process_variables (session_id, time DESC);

CREATE INDEX IF NOT EXISTS idx_pv_tag_path
    ON process_variables (tag_path, session_id, time DESC);

CREATE INDEX IF NOT EXISTS idx_pv_sim_time
    ON process_variables (session_id, sim_time);

-- ============================================================================
-- Session Events Table
-- ============================================================================
-- Stores discrete events: operator actions, scenario events, state changes
CREATE TABLE IF NOT EXISTS session_events (
    time TIMESTAMPTZ NOT NULL,
    session_id UUID NOT NULL,
    event_type TEXT NOT NULL,           -- session_start, operator_action, fault_injection, etc.
    sim_time DOUBLE PRECISION NOT NULL,
    operator_id TEXT,                   -- Trainee identifier
    tag_path TEXT,                      -- For operator_action events
    old_value DOUBLE PRECISION,         -- Value before change
    new_value DOUBLE PRECISION,         -- Value after change
    metadata JSONB                      -- Additional event data
);

-- Create hypertable
SELECT create_hypertable(
    'session_events',
    'time',
    if_not_exists => TRUE,
    chunk_time_interval => INTERVAL '1 day'
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_events_session_id
    ON session_events (session_id, time DESC);

CREATE INDEX IF NOT EXISTS idx_events_type
    ON session_events (event_type, session_id, time DESC);

CREATE INDEX IF NOT EXISTS idx_events_operator
    ON session_events (operator_id, time DESC)
    WHERE operator_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_events_metadata
    ON session_events USING GIN (metadata);

-- ============================================================================
-- Session Metadata Table
-- ============================================================================
-- Stores session-level information (PostgreSQL table, not hypertable)
CREATE TABLE IF NOT EXISTS sessions (
    session_id UUID PRIMARY KEY,
    flowsheet_name TEXT NOT NULL,
    session_name TEXT,
    scenario_id TEXT,
    operator_id TEXT,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ,
    status TEXT NOT NULL,               -- running, paused, stopped, completed
    created_at TIMESTAMPTZ DEFAULT NOW(),
    metadata JSONB
);

CREATE INDEX IF NOT EXISTS idx_sessions_operator
    ON sessions (operator_id, start_time DESC);

CREATE INDEX IF NOT EXISTS idx_sessions_scenario
    ON sessions (scenario_id, start_time DESC);

CREATE INDEX IF NOT EXISTS idx_sessions_status
    ON sessions (status);

-- ============================================================================
-- Scenarios Table
-- ============================================================================
-- Stores scenario definitions
CREATE TABLE IF NOT EXISTS scenarios (
    scenario_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    flowsheet_name TEXT NOT NULL,
    scenario_json JSONB NOT NULL,       -- Full scenario definition
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    created_by TEXT,
    version INTEGER DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_scenarios_flowsheet
    ON scenarios (flowsheet_name);

-- ============================================================================
-- Continuous Aggregates for Performance Metrics
-- ============================================================================
-- Hourly rollup of process variables (reduces query time for dashboards)
CREATE MATERIALIZED VIEW IF NOT EXISTS process_variables_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', time) AS bucket,
    session_id,
    tag_path,
    AVG(value) AS avg_value,
    MIN(value) AS min_value,
    MAX(value) AS max_value,
    STDDEV(value) AS stddev_value,
    COUNT(*) AS sample_count
FROM process_variables
GROUP BY bucket, session_id, tag_path
WITH NO DATA;

-- Refresh policy: update aggregates every hour
SELECT add_continuous_aggregate_policy('process_variables_hourly',
    start_offset => INTERVAL '3 hours',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE
);

-- ============================================================================
-- Data Retention Policies
-- ============================================================================
-- Keep raw process_variables data for 90 days
SELECT add_retention_policy('process_variables',
    INTERVAL '90 days',
    if_not_exists => TRUE
);

-- Keep session_events for 365 days
SELECT add_retention_policy('session_events',
    INTERVAL '365 days',
    if_not_exists => TRUE
);

-- Keep hourly aggregates for 2 years
SELECT add_retention_policy('process_variables_hourly',
    INTERVAL '730 days',
    if_not_exists => TRUE
);

-- ============================================================================
-- Helper Views
-- ============================================================================
-- Active sessions view
CREATE OR REPLACE VIEW active_sessions AS
SELECT
    s.session_id,
    s.session_name,
    s.operator_id,
    s.scenario_id,
    s.start_time,
    s.status,
    COUNT(DISTINCT e.event_type) AS event_type_count,
    COUNT(e.*) AS total_events,
    EXTRACT(EPOCH FROM (NOW() - s.start_time)) AS duration_seconds
FROM sessions s
LEFT JOIN session_events e ON s.session_id = e.session_id
WHERE s.status IN ('running', 'paused')
GROUP BY s.session_id, s.session_name, s.operator_id, s.scenario_id, s.start_time, s.status;

-- Session summary view
CREATE OR REPLACE VIEW session_summary AS
SELECT
    s.session_id,
    s.session_name,
    s.operator_id,
    s.scenario_id,
    s.flowsheet_name,
    s.start_time,
    s.end_time,
    s.status,
    EXTRACT(EPOCH FROM (COALESCE(s.end_time, NOW()) - s.start_time)) AS duration_seconds,
    COUNT(DISTINCT e.event_type) FILTER (WHERE e.event_type = 'operator_action') AS operator_action_count,
    COUNT(*) FILTER (WHERE e.event_type = 'fault_injection') AS fault_count
FROM sessions s
LEFT JOIN session_events e ON s.session_id = e.session_id
GROUP BY s.session_id, s.session_name, s.operator_id, s.scenario_id,
         s.flowsheet_name, s.start_time, s.end_time, s.status;

-- ============================================================================
-- Permissions (for production)
-- ============================================================================
-- CREATE ROLE ots_app WITH LOGIN PASSWORD 'change_me_in_production';
-- GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO ots_app;
-- GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO ots_app;

-- ============================================================================
-- Sample Data (for testing)
-- ============================================================================
-- Uncomment to insert sample data for development

-- INSERT INTO scenarios (scenario_id, name, description, flowsheet_name, scenario_json)
-- VALUES (
--     'test-scenario-001',
--     'Distillation Column Startup',
--     'Normal startup procedure with no faults',
--     'distillation_column.dwxmz',
--     '{
--         "version": "1.0",
--         "metadata": {
--             "title": "Distillation Column Startup",
--             "duration_minutes": 30
--         },
--         "events": []
--     }'::jsonb
-- );

-- ============================================================================
-- Database Info
-- ============================================================================
COMMENT ON TABLE process_variables IS 'Time-series storage for all process variable measurements';
COMMENT ON TABLE session_events IS 'Event log for operator actions, scenario events, and state changes';
COMMENT ON TABLE sessions IS 'Session metadata and lifecycle tracking';
COMMENT ON TABLE scenarios IS 'Scenario definitions and versioning';

-- Success message
DO $$
BEGIN
    RAISE NOTICE '✅ DWSIM OTS Database initialized successfully';
    RAISE NOTICE '   - Hypertables created: process_variables, session_events';
    RAISE NOTICE '   - Tables created: sessions, scenarios';
    RAISE NOTICE '   - Continuous aggregates: process_variables_hourly';
    RAISE NOTICE '   - Retention policies applied';
END $$;
