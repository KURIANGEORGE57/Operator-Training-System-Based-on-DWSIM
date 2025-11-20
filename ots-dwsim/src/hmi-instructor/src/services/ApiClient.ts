import axios, { AxiosInstance } from 'axios';
import {
  Session,
  TagValue,
  EventLogEntry,
  ScenarioRun,
  CreateSessionRequest,
  WriteTagRequest,
  OrchestratorHealth,
} from '../types';

class ApiClient {
  private client: AxiosInstance;
  private baseURL: string;

  constructor() {
    // Use environment variable or default to orchestrator URL
    this.baseURL = process.env.REACT_APP_API_URL || 'http://localhost:5001';

    this.client = axios.create({
      baseURL: this.baseURL,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 10000,
    });

    // Add response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      (error) => {
        console.error('API Error:', error.response?.data || error.message);
        return Promise.reject(error);
      }
    );
  }

  // Health and statistics
  async getHealth(): Promise<OrchestratorHealth> {
    const response = await this.client.get('/api/v1/health');
    return response.data;
  }

  async getStatistics() {
    const response = await this.client.get('/api/v1/statistics');
    return response.data;
  }

  // Session management
  async createSession(request: CreateSessionRequest): Promise<Session> {
    const response = await this.client.post('/api/v1/sessions', {
      flowsheetName: request.flowsheet,
      sessionName: request.sessionName,
      operatorId: 'operator-001', // TODO: Get from auth context
      metadata: request.environment,
    });
    return response.data;
  }

  async getSession(sessionId: string): Promise<Session> {
    const response = await this.client.get(`/api/v1/sessions/${sessionId}`);
    return response.data;
  }

  async listSessions(status?: string): Promise<Session[]> {
    const params = status ? { status } : {};
    const response = await this.client.get('/api/v1/sessions', { params });
    return response.data;
  }

  async startSession(sessionId: string, timeFactor: number = 1.0) {
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/start`, {
      startMode: 'run',
      timeFactor,
    });
    return response.data;
  }

  async pauseSession(sessionId: string) {
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/pause`);
    return response.data;
  }

  async stopSession(sessionId: string) {
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/stop`);
    return response.data;
  }

  async deleteSession(sessionId: string) {
    await this.client.delete(`/api/v1/sessions/${sessionId}`);
  }

  // Tag operations
  async readTag(sessionId: string, tagPath: string): Promise<TagValue> {
    const response = await this.client.get(`/api/v1/sessions/${sessionId}/tags/${tagPath}`);
    return response.data;
  }

  async writeTag(sessionId: string, tagPath: string, value: any, user?: string): Promise<TagValue> {
    const request: WriteTagRequest = { value, user };
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/tags/${tagPath}`, request);
    return response.data;
  }

  // Event log
  async getSessionEvents(sessionId: string, from?: Date, to?: Date): Promise<EventLogEntry[]> {
    const params: any = {};
    if (from) params.from = from.toISOString();
    if (to) params.to = to.toISOString();

    const response = await this.client.get(`/api/v1/sessions/${sessionId}/events`, { params });
    return response.data;
  }

  // Scenario operations
  async runScenario(sessionId: string, scenarioPath: string, timeFactor: number = 1.0): Promise<ScenarioRun> {
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/scenario/run`, {
      scenarioPath,
      timeFactor,
    });
    return response.data;
  }

  async stopScenario(runId: string) {
    await this.client.post(`/api/v1/sessions/scenarios/${runId}/stop`);
  }

  async getScenarioStatus(runId: string): Promise<ScenarioRun> {
    const response = await this.client.get(`/api/v1/sessions/scenarios/${runId}/status`);
    return response.data;
  }

  async listScenarioRuns(): Promise<ScenarioRun[]> {
    const response = await this.client.get('/api/v1/sessions/scenarios');
    return response.data;
  }

  // Snapshot operations
  async createSnapshot(sessionId: string, name: string) {
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/snapshot`, { name });
    return response.data;
  }

  async restoreSnapshot(sessionId: string, snapshotId: string) {
    const response = await this.client.post(`/api/v1/sessions/${sessionId}/restore`, { snapshotId });
    return response.data;
  }
}

export default new ApiClient();
