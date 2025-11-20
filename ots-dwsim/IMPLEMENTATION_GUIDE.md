# DWSIM OTS Implementation Guide

## Overview

This guide provides step-by-step instructions for developers implementing the DWSIM Operator Training System (OTS). The system is organized into multiple phases with clear acceptance criteria.

## Repository Structure

```
ots-dwsim/
├── src/
│   ├── dwsim-host/           # ✅ COMPLETED - REST API simulation host
│   ├── control-gateway/      # ✅ COMPLETED - OPC UA server skeleton
│   ├── orchestrator/         # ✅ COMPLETED - Session orchestration & pool management
│   ├── hmi-operator/         # ✅ COMPLETED - React operator HMI
│   ├── hmi-instructor/       # ✅ COMPLETED - React instructor UI
│   ├── replay/               # ⏳ TODO - Replay engine
│   ├── docs/
│   │   ├── api/              # ✅ COMPLETED - OpenAPI spec
│   │   └── schemas/          # ✅ COMPLETED - Scenario JSON schema
│   └── infra/
│       └── db/               # ✅ COMPLETED - TimescaleDB schema
├── samples/
│   ├── flowsheets/           # ⏳ TODO - Sample DWSIM flowsheets
│   └── scenarios/            # ✅ COMPLETED - Sample scenario JSON
├── .github/workflows/        # ✅ COMPLETED - CI/CD pipeline
├── docker-compose.yml        # ✅ COMPLETED - Development stack
└── README.md                 # ✅ COMPLETED
```

## Phase A: Foundation (Backend + Control) ✅ COMPLETED

### A.1: Repository Setup ✅
- [x] Created directory structure
- [x] Added CI/CD workflow (`.github/workflows/ci.yml`)
- [x] Created docker-compose for development

**Acceptance**: CI builds succeed

### A.2: DWSIM Simulation Host ✅
- [x] Created .NET 8 Web API project
- [x] Implemented session lifecycle API
- [x] Created tag read/write endpoints
- [x] Added Swagger/OpenAPI documentation
- [x] Integrated with DWSIM.Automation

**Location**: `src/dwsim-host/`

**Key Files**:
- `Program.cs` - ASP.NET Core setup
- `Controllers/SessionsController.cs` - REST API endpoints
- `Services/SessionManager.cs` - Core session logic
- `Services/FlowsheetRepository.cs` - Flowsheet file management
- `Models/SessionModels.cs` - DTOs

**Acceptance**:
```bash
cd src/dwsim-host
dotnet build
dotnet run
curl http://localhost:5000/health
```

### A.3: Session Lifecycle ✅
Implemented endpoints:
- `POST /api/v1/sessions` - Create session
- `POST /api/v1/sessions/{id}/start` - Start simulation
- `POST /api/v1/sessions/{id}/pause` - Pause
- `POST /api/v1/sessions/{id}/stop` - Stop

**Acceptance**: Can create session and receive valid session ID

### A.4: Tag Read/Write ✅
- `GET /api/v1/sessions/{id}/tags/{path}` - Read tag
- `POST /api/v1/sessions/{id}/tags/{path}` - Write tag

**Acceptance**: Read/write operations log correctly

### A.5: OPC UA Server Skeleton ✅
- [x] Created Control Gateway project
- [x] Implemented mapping loader (YAML)
- [x] Created OPC UA server interface
- [x] Added basic node management

**Location**: `src/control-gateway/`

**Key Files**:
- `Program.cs` - Service host
- `Services/OpcUaServer.cs` - OPC UA implementation
- `Services/MappingLoader.cs` - YAML mapping parser
- `Models/OpcUaMapping.cs` - Mapping models
- `config/opcua-mapping-template.yaml` - Tag mapping template

**Acceptance**: Service starts without errors

### A.6: Tag Mapping Loader ✅
- [x] YAML deserializer
- [x] Session context replacement
- [x] Folder structure creation

**Acceptance**: Mapping loads and validates successfully

---

## Phase B: Orchestration & Scenarios ✅ COMPLETED

### B.1: Scenario JSON Schema Validator ✅
- [x] Created JSON Schema v7 definition
- [x] Sample scenario file created
- [x] Added to CI validation

**Files**:
- `src/docs/schemas/scenario.schema.json`
- `samples/scenarios/distill_startup_fault.json`

**Future Enhancements**:
1. Create C# validator service
2. Add validation endpoint to API
3. Create scenario library management

**Acceptance**:
```bash
ajv validate -s src/docs/schemas/scenario.schema.json \
  -d samples/scenarios/distill_startup_fault.json
```

### B.2: Scenario Executor ✅ COMPLETED

**Completed Tasks**:
- [x] Created `ScenarioExecutor` service (440 lines)
- [x] Implemented event scheduler with precise timing
- [x] Added event handlers for all event types:
  - `set` - Set tag value
  - `fault` - Inject fault
  - `controller` - Modify controller parameters
  - `note` - Instructor note
  - `random_fault` - Random fault injection
  - `alarm` - Alarm trigger
  - `operator_prompt` - Operator prompt
- [x] Implemented repeating events with interval and count
- [x] Added scenario run management (start, stop, status, list)
- [x] Integrated with SessionManager for tag read/write
- [x] Added REST API endpoints to SessionsController

**Files Created**:
- `src/dwsim-host/Models/ScenarioModels.cs` (170 lines)
- `src/dwsim-host/Services/IScenarioExecutor.cs` (30 lines)
- `src/dwsim-host/Services/ScenarioExecutor.cs` (440 lines)

**API Endpoints**:
- `POST /api/v1/sessions/{id}/scenario/run` - Run scenario
- `POST /api/v1/sessions/scenarios/{runId}/stop` - Stop scenario
- `GET /api/v1/sessions/scenarios/{runId}/status` - Get scenario status
- `GET /api/v1/sessions/scenarios` - List all scenario runs

**Acceptance**: ✅ Passed
- Load sample scenario ✓
- Execute events at correct sim_time with time_factor support ✓
- Log all events ✓
- Support deterministic execution with seed ✓
- Handle repeating events ✓

### B.3: Snapshot & Restore ✅ COMPLETED

**Completed Tasks**:
- [x] Implemented DWSIM flowsheet XML serialization
- [x] Created SnapshotManager with file storage
- [x] Implemented snapshot creation and restoration
- [x] Added snapshot metadata tracking
- [x] Integrated with SessionManager

**Files Created**:
- `src/dwsim-host/Services/ISnapshotManager.cs` (30 lines)
- `src/dwsim-host/Services/SnapshotManager.cs` (150 lines)

**Features**:
- Snapshots saved as .dwxmz files (DWSIM native format)
- Metadata tracking (snapshot ID, name, created date, file size)
- Automatic snapshot directory creation
- Session-specific snapshot tracking
- Full flowsheet state preservation

**API Endpoints**:
- `POST /api/v1/sessions/{id}/snapshot` - Create snapshot
- `POST /api/v1/sessions/{id}/restore` - Restore snapshot

**Acceptance**: ✅ Passed
- Create snapshot at any simulation time ✓
- Modify simulation ✓
- Restore snapshot to exact state ✓
- State consistency verified through DWSIM XML serialization ✓

---

## Phase C: User Interfaces ✅ COMPLETED

### C.1: Operator HMI (React) ✅ COMPLETED

**Completed Tasks**:
- [x] Created React 18 + TypeScript project
- [x] Installed Material-UI v5, Recharts, Axios
- [x] Implemented all core components:
  - `ProcessMimic.tsx` - Live SVG flowsheet with 6 process variables
  - `TrendChart.tsx` - Real-time Recharts line chart (4 variables, 30-point history)
  - `AlarmList.tsx` - Material-UI table with severity indicators
  - `ControlPanel.tsx` - 4 operator input controls with validation
- [x] Created API client with all Orchestrator endpoints
- [x] Implemented auto-refresh (2-second polling)
- [x] Added session controls (start/pause/stop) in app bar
- [x] Configured Material-UI dark theme
- [x] Created production Dockerfile with Nginx

**Location**: `src/hmi-operator/`

**Key Files**:
- `src/components/ProcessMimic.tsx` (125 lines)
- `src/components/TrendChart.tsx` (105 lines)
- `src/components/AlarmList.tsx` (85 lines)
- `src/components/ControlPanel.tsx` (120 lines)
- `src/services/ApiClient.ts` (155 lines)
- `src/App.tsx` (180 lines)

**Acceptance**: ✅ Passed
- Displays 6 process variables ✓
- Updates every 2 seconds ✓
- Operator can write setpoints with validation ✓
- All actions logged via ApiClient ✓

### C.2: Instructor UI (React) ✅ COMPLETED

**Completed Tasks**:
- [x] Created React 18 + TypeScript project
- [x] Implemented all core components:
  - `SessionMonitor.tsx` - Multi-session management table with create dialog
  - `ScenarioEditor.tsx` - JSON editor with validation
  - `EventTimeline.tsx` - Chronological event list with filtering
  - `AssessmentViewer.tsx` - KPI results and grading display
- [x] Created API client with assessment endpoints
- [x] Implemented auto-refresh (3-second polling)
- [x] Added tabbed interface for all features
- [x] Configured Material-UI dark theme
- [x] Created production Dockerfile with Nginx

**Location**: `src/hmi-instructor/`

**Key Files**:
- `src/components/SessionMonitor.tsx` (155 lines)
- `src/components/ScenarioEditor.tsx` (110 lines)
- `src/components/EventTimeline.tsx` (145 lines)
- `src/components/AssessmentViewer.tsx` (490 lines)
- `src/services/ApiClient.ts` (175 lines)
- `src/types/index.ts` (118 lines)

**Acceptance**: ✅ Passed
- Create and manage sessions ✓
- Edit scenarios with JSON validation ✓
- View event timeline ✓
- Generate and view assessments ✓

---

## Phase D: Assessment Engine ✅ COMPLETED

### D.1: Time-Series Logging ✅ COMPLETED

**Completed Tasks**:
- [x] Created TimescaleDB schema with hypertables
- [x] Implemented `TimescaleDbLogger` service (380 lines)
- [x] Added batched event and process variable logging
- [x] Implemented automatic flush timer (5-second intervals)
- [x] Created retention policies (90 days raw, 365 days events)
- [x] Added continuous aggregates for hourly rollups
- [x] Integrated with Orchestrator service

**Files Created**:
- `src/infra/db/init.sql` (262 lines) - Complete database schema
- `src/orchestrator/Services/TimescaleDbLogger.cs` (380 lines)
- `src/orchestrator/Services/ITimescaleDbLogger.cs` (30 lines)

**Schema Highlights**:
- `process_variables` hypertable - Time-series process data
- `session_events` hypertable - Event log with retention
- `sessions` table - Session metadata
- `scenarios` table - Scenario definitions
- Continuous aggregate: `process_variables_hourly`

**Acceptance**: ✅ Passed
- Sessions logged to TimescaleDB ✓
- Process variables batched efficiently ✓
- Query historical data via API ✓
- Retention policies active ✓

### D.2: Replay Engine ⏳ TODO

**Status**: Not implemented (future enhancement)

**Planned Features**:
- Load session event log from TimescaleDB
- Create deterministic replay session
- Compare outputs for verification
- Generate deviation reports

### D.3: Assessment Engine ✅ COMPLETED

**Completed Tasks**:
- [x] Created assessment models (AssessmentReport, KpiResult, PerformanceMetrics)
- [x] Implemented `AssessmentEngine` service (320 lines)
- [x] Added 4 default KPI evaluators:
  - **Completion Time** (weight 1.5, target 30min, threshold 45min)
  - **Operator Actions** (weight 1.0, target 10, threshold 20)
  - **Snapshot Usage** (weight 0.5)
  - **Error Recovery** (weight 0.8, target 0, threshold 3)
- [x] Implemented weighted scoring calculation
- [x] Added grade assignment (A/B/C/D/F) with 70% passing score
- [x] Created performance metrics calculator
- [x] Added assessment API endpoints to Orchestrator
- [x] Integrated with Instructor UI (AssessmentViewer component)

**Files Created**:
- `src/orchestrator/Models/AssessmentModels.cs` (130 lines)
- `src/orchestrator/Services/IAssessmentEngine.cs` (30 lines)
- `src/orchestrator/Services/AssessmentEngine.cs` (320 lines)
- `src/hmi-instructor/src/components/AssessmentViewer.tsx` (490 lines)

**API Endpoints**:
- `POST /api/v1/assessments` - Generate assessment
- `GET /api/v1/assessments/{id}` - Get assessment report
- `GET /api/v1/assessments` - List all assessments
- `GET /api/v1/sessions/{id}/metrics` - Get performance metrics

**Acceptance**: ✅ Passed
- Generate assessment for completed session ✓
- Calculate weighted KPI scores ✓
- Assign grade (A-F) based on score ✓
- View assessment in Instructor UI ✓
- Performance metrics calculated correctly ✓

---

## Phase E: Packaging & Deployment ⏳ TODO

### E.1: Docker Images

**Tasks**:
1. Create production Dockerfiles:

**`src/dwsim-host/Dockerfile`**:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "ots-dwsim/src/dwsim-host/DWSIM.OTS.SimulationHost.csproj"
RUN dotnet publish "ots-dwsim/src/dwsim-host/DWSIM.OTS.SimulationHost.csproj" \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "DWSIM.OTS.SimulationHost.dll"]
```

2. Build and test images:
```bash
docker-compose up --build
docker-compose ps
```

**Acceptance**: All services start and health checks pass

### E.2: Documentation & Installer

**Tasks**:
1. Create deployment guide
2. Create Kubernetes manifests (optional)
3. Create Windows installer (optional)
4. Write operator manual
5. Write instructor manual

**Files to create**:
- `docs/deployment/README.md`
- `docs/deployment/kubernetes/`
- `docs/manuals/operator-manual.pdf`
- `docs/manuals/instructor-manual.pdf`

---

## Testing Strategy

### Unit Tests
```bash
dotnet test tests/unit --logger "console;verbosity=detailed"
```

### Integration Tests
```bash
# Start dependencies
docker-compose up -d postgres timescaledb

# Run tests
dotnet test tests/integration
```

### End-to-End Test
```bash
# 1. Start all services
docker-compose up -d

# 2. Create session
SESSION_ID=$(curl -X POST http://localhost:5000/api/v1/sessions \
  -H "Content-Type: application/json" \
  -d '{"flowsheet":"sample.dwx","session_name":"e2e_test"}' \
  | jq -r '.session_id')

# 3. Start session
curl -X POST http://localhost:5000/api/v1/sessions/$SESSION_ID/start \
  -H "Content-Type: application/json" \
  -d '{"start_mode":"run"}'

# 4. Read tag
curl http://localhost:5000/api/v1/sessions/$SESSION_ID/tags/Streams.Feed.Temperature

# 5. Run scenario
curl -X POST http://localhost:5000/api/v1/sessions/$SESSION_ID/scenario/run \
  -H "Content-Type: application/json" \
  -d '{"scenario_id":"samples/scenarios/distill_startup_fault.json"}'

# 6. Verify events
curl http://localhost:5000/api/v1/sessions/$SESSION_ID/events

# 7. Stop session
curl -X POST http://localhost:5000/api/v1/sessions/$SESSION_ID/stop
```

---

## Development Workflow

### Daily Development
```bash
# Terminal 1: Start backend
cd src/dwsim-host
dotnet watch run

# Terminal 2: Start control gateway
cd src/control-gateway
dotnet run

# Terminal 3: Start operator HMI
cd src/hmi-operator
npm start

# Terminal 4: Start instructor UI
cd src/hmi-instructor
npm start
```

### Before Commit
```bash
# Format code
dotnet format

# Run tests
dotnet test

# Validate scenarios
ajv validate -s src/docs/schemas/scenario.schema.json -d "samples/scenarios/*.json"

# Lint YAML
yamllint src/control-gateway/config/*.yaml
```

---

## Troubleshooting

### DWSIM Flowsheet Load Errors
- Ensure flowsheet is in `samples/flowsheets/`
- Check file extension (.dwxmz or .dwxml)
- Verify flowsheet opens in DWSIM GUI

### OPC UA Connection Issues
- Check port 4840 is not blocked
- Verify `opcua-mapping-template.yaml` syntax
- Check logs: `tail -f logs/control-gateway/*.log`

### Session Start Failures
- Check DWSIM assemblies are referenced correctly
- Verify flowsheet has no solver errors
- Review logs in `logs/dwsim-host/`

---

## Next Steps

1. **Immediate Priority**:
   - Implement scenario executor (Phase B.2)
   - Create basic operator HMI (Phase C.1)
   - Add sample DWSIM flowsheet

2. **Week 1-2**:
   - Complete Phase B (Scenarios)
   - Start Phase C (UIs)
   - Add integration tests

3. **Week 3-4**:
   - Complete Phase D (Logging & Replay)
   - Add comprehensive tests
   - Performance optimization

4. **Week 5-6**:
   - Phase E (Docker & Docs)
   - User acceptance testing
   - Production deployment

---

## Contributing

### Code Style
- C#: Follow Microsoft C# coding conventions
- TypeScript/React: Use ESLint + Prettier
- Commit messages: Conventional Commits format

### Pull Request Process
1. Create feature branch: `git checkout -b feature/scenario-executor`
2. Implement with tests
3. Update documentation
4. Submit PR with description
5. Wait for CI to pass
6. Request review

### Contact
- Issues: GitHub Issues
- Discussions: GitHub Discussions
- DWSIM Community: http://dwsim.inforside.com.br

---

**Document Version**: 1.0
**Last Updated**: 2025-11-17
**Status**: Living document - updated as implementation progresses
