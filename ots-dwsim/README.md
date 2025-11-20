# DWSIM Operator Training System (OTS)

**Version:** 1.0.0-beta
**License:** GPL-3.0 (inherited from DWSIM)
**Status:** ✅ Phase A-D Complete | 🚧 Active Development

A comprehensive operator training system built on DWSIM for realistic process simulation training and assessment.

## 🚀 Quick Start

### Prerequisites

- Docker and Docker Compose
- .NET 8.0 SDK (for local development)
- Node.js 18+ (for UI development)
- DWSIM flowsheet files (.dwxmz format)

### Running with Docker Compose

1. Clone the repository and navigate to the project directory:
```bash
cd ots-dwsim
```

2. Start all services:
```bash
docker-compose up -d
```

3. Access the applications:
   - **Operator HMI**: http://localhost:3000
   - **Instructor Station**: http://localhost:3001
   - **Orchestrator API**: http://localhost:5001
   - **TimescaleDB**: localhost:5432

4. Stop all services:
```bash
docker-compose down
```

## 📋 Features

### ✅ Phase A - Foundation/Backend
- **DWSIM Host Service**: Automation API wrapper for DWSIM simulation engine
- **Session Management**: Create, start, pause, stop, and delete training sessions
- **Tag Operations**: Read and write process variables with event logging
- **Health Monitoring**: Service health checks and diagnostics

### ✅ Phase B - Orchestration & Scenarios
- **Session Pool Manager**: Multi-host orchestration with load balancing
- **TimescaleDB Integration**: Time-series database for process variables and events
- **Scenario Executor**: Precise event-driven scenario engine with:
  - 7 event types: `set`, `fault`, `controller`, `note`, `random_fault`, `alarm`, `operator_prompt`
  - Repeating events with intervals
  - Time factor support for accelerated/slowed training
- **Snapshot/Restore**: Full flowsheet state capture and restoration

### ✅ Phase C - User Interfaces
- **Operator HMI** (React + TypeScript):
  - Process Mimic with live tag values
  - Real-time trend charts (4 variables)
  - Control panel for operator inputs
  - Alarm list with severity indicators
  - Session controls (start/pause/stop)

- **Instructor Station** (React + TypeScript):
  - Session monitor with multi-session management
  - Scenario editor with JSON validation
  - Event timeline with filtering
  - Assessment viewer with KPI tracking

### ✅ Phase D - Assessment Engine
- **KPI Evaluation System**:
  - Completion Time scoring
  - Operator Actions tracking
  - Snapshot Usage analysis
  - Error Recovery metrics
- **Weighted Scoring**: Configurable KPI weights and thresholds
- **Grade Assignment**: A-F grading with pass/fail determination
- **Performance Metrics**: Comprehensive session analytics

## 🏗️ Architecture

```
┌─────────────────────┐     ┌──────────────────────┐
│  Operator HMI       │     │ Instructor Station   │
│  (React, Port 3000) │     │ (React, Port 3001)   │
└──────────┬──────────┘     └──────────┬───────────┘
           │                           │
           │         HTTP/REST         │
           └───────────┬───────────────┘
                       │
           ┌───────────▼────────────┐
           │   Orchestrator API     │
           │   (.NET 8, Port 5001)  │
           └───────────┬────────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
┌───────▼────────┐ ┌──▼───────────┐ ┌▼───────────────┐
│ DWSIM Host 1   │ │ DWSIM Host 2 │ │  TimescaleDB   │
│ (Port 5000)    │ │ (Port 5002)  │ │  (Port 5432)   │
└────────────────┘ └──────────────┘ └────────────────┘
```

### Key Components

1. **DWSIM Host Service** (`/src/dwsim-host`)
   - Wraps DWSIM automation API
   - Manages individual simulation sessions
   - Executes scenarios and maintains snapshots
   - Exposes REST API for remote control

2. **Orchestrator Service** (`/src/orchestrator`)
   - Manages session pool across multiple DWSIM hosts
   - Handles load balancing and health monitoring
   - Logs events and process variables to TimescaleDB
   - Provides unified API for UI applications
   - Executes assessment engine for trainee evaluation

3. **TimescaleDB** (`/src/infra/db`)
   - Hypertables for time-series data
   - Continuous aggregates for hourly rollups
   - Retention policies (90 days raw, 365 days events)
   - Session events and process variable history

4. **Operator HMI** (`/src/hmi-operator`)
   - React 18 + TypeScript + Material-UI
   - Real-time process visualization
   - Interactive controls for trainees
   - Auto-refresh every 2 seconds

5. **Instructor Station** (`/src/hmi-instructor`)
   - React 18 + TypeScript + Material-UI
   - Session management and monitoring
   - Scenario authoring with JSON editor
   - Event timeline and assessment reports
   - Performance metrics dashboard

## 🔧 Development

### Backend Services

```bash
# DWSIM Host
cd src/dwsim-host
dotnet restore
dotnet run

# Orchestrator
cd src/orchestrator
dotnet restore
dotnet run
```

### Frontend Applications

```bash
# Operator HMI
cd src/hmi-operator
npm install
npm start  # Runs on port 3000

# Instructor Station
cd src/hmi-instructor
npm install
npm start  # Runs on port 3001
```

### Database Setup

```bash
# Start TimescaleDB
docker run -d \
  --name timescaledb \
  -p 5432:5432 \
  -e POSTGRES_PASSWORD=postgres \
  -v ./src/infra/db/init.sql:/docker-entrypoint-initdb.d/init.sql \
  timescale/timescaledb:latest-pg15
```

## 📖 API Documentation

### Orchestrator API Endpoints

#### Session Management
- `POST /api/v1/sessions` - Create a new session
- `GET /api/v1/sessions` - List all sessions
- `GET /api/v1/sessions/{id}` - Get session details
- `POST /api/v1/sessions/{id}/start` - Start a session
- `POST /api/v1/sessions/{id}/pause` - Pause a session
- `POST /api/v1/sessions/{id}/stop` - Stop a session
- `DELETE /api/v1/sessions/{id}` - Delete a session

#### Tag Operations
- `GET /api/v1/sessions/{id}/tags/{tagPath}` - Read tag value
- `POST /api/v1/sessions/{id}/tags/{tagPath}` - Write tag value

#### Scenarios
- `POST /api/v1/sessions/{id}/scenario/run` - Execute a scenario
- `POST /api/v1/sessions/scenarios/{runId}/stop` - Stop scenario
- `GET /api/v1/sessions/scenarios/{runId}/status` - Get scenario status

#### Snapshots
- `POST /api/v1/sessions/{id}/snapshot` - Create snapshot
- `POST /api/v1/sessions/{id}/restore` - Restore from snapshot

#### Events & Monitoring
- `GET /api/v1/sessions/{id}/events` - Get event log
- `GET /api/v1/health` - Health check
- `GET /api/v1/statistics` - Pool statistics

#### Assessments
- `POST /api/v1/assessments` - Generate assessment for a session
- `GET /api/v1/assessments/{id}` - Get assessment report
- `GET /api/v1/assessments` - List all assessments
- `GET /api/v1/sessions/{id}/metrics` - Get performance metrics

## 📝 Scenario JSON Schema

```json
{
  "scenario_id": "training_scenario_001",
  "title": "Basic Process Control",
  "description": "Introduction to process control operations",
  "author": "instructor",
  "seed": 12345,
  "initial_state": {
    "Streams.Feed.Temperature": 298.15,
    "Streams.Feed.Flow": 2.0
  },
  "events": [
    {
      "time_s": 60,
      "type": "set",
      "target": "Streams.Feed.Temperature",
      "payload": 310.15
    },
    {
      "time_s": 120,
      "type": "fault",
      "target": "Equipment.Pump01.Efficiency",
      "payload": 0.5
    },
    {
      "time_s": 300,
      "type": "note",
      "payload": "Observe temperature change response"
    }
  ],
  "metadata": {
    "recommended_time_factor": 1.0,
    "expected_duration_seconds": 300,
    "difficulty": "beginner"
  }
}
```

### Supported Event Types

1. **`set`** - Set a tag to a specific value
2. **`fault`** - Inject equipment fault (efficiency, capacity)
3. **`controller`** - Modify controller parameters (setpoint, gains)
4. **`note`** - Display informational message
5. **`random_fault`** - Random fault from predefined list
6. **`alarm`** - Trigger alarm condition
7. **`operator_prompt`** - Prompt operator for action

### Repeating Events

```json
{
  "time_s": 60,
  "type": "set",
  "target": "Streams.Feed.Flow",
  "payload": 2.5,
  "repeat": {
    "interval_s": 120,
    "count": 5
  }
}
```

## 🎯 Assessment KPIs

The assessment engine evaluates trainees on 4 default KPIs:

| KPI | Weight | Description | Target | Threshold |
|-----|--------|-------------|--------|-----------|
| **Completion Time** | 1.5 | Time to complete scenario | 30 min | 45 min |
| **Operator Actions** | 1.0 | Number of tag writes | 10 | 20 |
| **Snapshot Usage** | 0.5 | Effective use of snapshots | - | - |
| **Error Recovery** | 0.8 | Number of snapshot restores | 0 | 3 |

### Grading Scale

- **A**: 90-100% (Excellent)
- **B**: 80-89% (Good)
- **C**: 70-79% (Satisfactory)
- **D**: 60-69% (Needs Improvement)
- **F**: <60% (Failed)

**Passing Score**: 70%

## 🔐 Configuration

### Environment Variables

#### Orchestrator
- `ConnectionStrings__TimescaleDb` - TimescaleDB connection string
- `DwsimHosts__0__Url` - DWSIM Host URL
- `DwsimHosts__0__MaxSessions` - Max concurrent sessions
- `Logging__LogLevel__Default` - Log level

#### DWSIM Host
- `FlowsheetsPath` - Path to flowsheet files
- `SnapshotsPath` - Path to snapshot storage
- `Logging__LogLevel__Default` - Log level

#### UI Applications
- `REACT_APP_API_URL` - Orchestrator API URL (default: http://localhost:5001)

## 📊 Database Schema

### Tables

1. **`process_variables`** (hypertable on `time`)
   - Time-series data for all process tags
   - Partitioned by time
   - Retention: 90 days

2. **`session_events`** (hypertable on `time`)
   - All session events (operator actions, faults, etc.)
   - Retention: 365 days

3. **`sessions`**
   - Session metadata and lifecycle
   - No automatic retention

4. **`scenarios`**
   - Scenario definitions and runs
   - No automatic retention

### Continuous Aggregates

- **`process_variables_hourly`** - Hourly rollups (min, max, avg)
  - Retention: 2 years
  - Materialized every hour

## 🧪 Testing

### Manual Testing Flow

1. **Start Services**:
   ```bash
   docker-compose up -d
   ```

2. **Create Session** (Instructor Station):
   - Navigate to "Session Monitor" tab
   - Click "Create Session"
   - Enter session name
   - Session starts with default flowsheet

3. **Run Scenario**:
   - Go to "Scenario Editor" tab
   - Edit or use sample scenario
   - Click "Run Scenario"

4. **Operate as Trainee** (Operator HMI):
   - View Process Mimic
   - Adjust controls in Control Panel
   - Monitor alarms
   - Observe trends

5. **Generate Assessment** (Instructor Station):
   - Go to "Assessments" tab
   - Click "Generate Assessment"
   - Select session
   - Review KPI results and grade

6. **View Timeline**:
   - Go to "Event Timeline" tab
   - Select session
   - Review all events chronologically

## 🐛 Troubleshooting

### Common Issues

1. **DWSIM Host fails to start**
   - Ensure flowsheet files exist in `/flowsheets`
   - Check DWSIM libraries are properly loaded
   - Verify .NET 8 Runtime is installed

2. **Orchestrator can't connect to TimescaleDB**
   - Check connection string in `appsettings.json`
   - Verify TimescaleDB container is running
   - Test connection: `docker exec -it timescaledb psql -U postgres`

3. **UI shows "Failed to load sessions"**
   - Check Orchestrator is running on port 5001
   - Verify proxy setting in UI package.json
   - Check browser console for CORS errors

4. **Scenario doesn't execute events**
   - Verify scenario JSON is valid
   - Check event times are sequential
   - Ensure session is in "Running" state
   - Review logs: `docker logs dwsim-host`

### Logs

```bash
# View all logs
docker-compose logs -f

# Specific service
docker logs -f dwsim-host
docker logs -f orchestrator
docker logs -f timescaledb

# .NET app logs (when running locally)
tail -f src/orchestrator/bin/Debug/net8.0/logs/*.log
```

## 📦 Deployment

See [DEPLOYMENT.md](./DEPLOYMENT.md) for production deployment guide.

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project builds upon DWSIM which is licensed under the GNU General Public License (GPL) Version 3.

Copyright 2008-2025 Daniel Wagner and contributors

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

## 🙏 Acknowledgments

- **DWSIM Team** - Daniel Wagner and contributors for the excellent open-source process simulator
- Built with .NET 8, React 18, Material-UI, TimescaleDB, and Docker

## 📞 Support

- 📧 Issues: https://github.com/KURIANGEORGE57/Operator-Training-System-Based-on-DWSIM/issues
- 💬 Discussions: Use GitHub Discussions for questions
- 📚 Documentation: See `docs/` directory for detailed guides

---

**Last Updated:** 2025-01-20
