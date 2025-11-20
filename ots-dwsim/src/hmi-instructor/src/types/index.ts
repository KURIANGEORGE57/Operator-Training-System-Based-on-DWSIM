export interface Session {
  sessionId: string;
  sessionName: string;
  status: 'Created' | 'Running' | 'Paused' | 'Stopped' | 'Error';
  createdAt: string;
  startedAt?: string;
  stoppedAt?: string;
  simTime: string;
  timeFactor: number;
  flowsheetPath: string;
}

export interface TagValue {
  tag: string;
  value: number | string | boolean | null;
  units?: string;
  simTime: string;
  quality?: string;
}

export interface EventLogEntry {
  type: string;
  simTime: string;
  realTime: string;
  user?: string;
  action?: string;
  target?: string;
  value?: any;
}

export interface ScenarioRun {
  runId: string;
  scenarioId: string;
  status: 'Initializing' | 'Running' | 'Paused' | 'Completed' | 'Stopped' | 'Failed';
  elapsedSimTimeSeconds: number;
  eventsExecuted: number;
  totalEvents: number;
  progressPercent: number;
}

export interface CreateSessionRequest {
  flowsheet: string;
  sessionName?: string;
  seed?: number;
  environment?: Record<string, any>;
}

export interface WriteTagRequest {
  value: any;
  user?: string;
}

export interface OrchestratorHealth {
  status: string;
  timestamp: string;
  statistics?: {
    totalSessions: number;
    activeSessions: number;
    availableHosts: number;
    totalCapacity: number;
    utilizationPercent: number;
  };
  hosts: Array<{
    hostUrl: string;
    isHealthy: boolean;
    activeSessions: number;
    maxSessions: number;
    lastChecked: string;
  }>;
}

export interface Alarm {
  id: string;
  severity: 'critical' | 'warning' | 'info';
  message: string;
  timestamp: string;
  acknowledged: boolean;
  tag?: string;
  value?: number;
}

export interface AssessmentReport {
  assessmentId: string;
  sessionId: string;
  scenarioId?: string;
  generatedAt: string;
  operatorId: string;
  overallScore: number;
  kpiResults: KpiResult[];
  metrics: Record<string, any>;
  grade: string;
  passed: boolean;
  comments?: string;
}

export interface KpiResult {
  kpiName: string;
  description: string;
  score: number;
  maxScore: number;
  weight: number;
  passed: boolean;
  actualValue: any;
  targetValue?: any;
  threshold?: any;
  feedback?: string;
}

export interface PerformanceMetrics {
  sessionDuration: string; // ISO 8601 duration
  totalOperatorActions: number;
  tagWrites: number;
  snapshotsCreated: number;
  snapshotsRestored: number;
  averageResponseTime: number;
  eventsProcessed: number;
  eventTypeBreakdown: Record<string, number>;
}
