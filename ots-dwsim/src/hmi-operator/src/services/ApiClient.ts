import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1'

export interface SessionInfo {
  sessionId: string
  sessionName: string
  status: string
  createdAt: string
  startedAt?: string
  stoppedAt?: string
  simTime: string
  timeFactor: number
  flowsheetPath: string
}

export interface CreateSessionRequest {
  flowsheet: string
  sessionName?: string
  seed?: number
  environment?: Record<string, any>
  maxRealTimeSec?: number
}

export interface CreateSessionResponse {
  sessionId: string
  status: string
  createdAt: string
}

export interface TagValue {
  tag: string
  value: any
  units?: string
  simTime: string
}

export interface WriteTagRequest {
  value: any
  user?: string
  mode?: string
}

export interface WriteTagResponse {
  status: string
  appliedValue: any
}

export class ApiClient {
  static async createSession(request: CreateSessionRequest): Promise<CreateSessionResponse> {
    const response = await axios.post(`${API_BASE_URL}/sessions`, request)
    return response.data
  }

  static async getSession(sessionId: string): Promise<SessionInfo | null> {
    try {
      const response = await axios.get(`${API_BASE_URL}/sessions/${sessionId}`)
      return response.data
    } catch (error) {
      return null
    }
  }

  static async getAllSessions(): Promise<SessionInfo[]> {
    const response = await axios.get(`${API_BASE_URL}/sessions`)
    return response.data
  }

  static async startSession(sessionId: string): Promise<any> {
    const response = await axios.post(`${API_BASE_URL}/sessions/${sessionId}/start`, {
      startMode: 'run'
    })
    return response.data
  }

  static async pauseSession(sessionId: string): Promise<SessionInfo> {
    const response = await axios.post(`${API_BASE_URL}/sessions/${sessionId}/pause`)
    return response.data
  }

  static async stopSession(sessionId: string): Promise<SessionInfo> {
    const response = await axios.post(`${API_BASE_URL}/sessions/${sessionId}/stop`)
    return response.data
  }

  static async readTag(sessionId: string, tagPath: string): Promise<TagValue | null> {
    try {
      const response = await axios.get(`${API_BASE_URL}/sessions/${sessionId}/tags/${tagPath}`)
      return response.data
    } catch (error) {
      return null
    }
  }

  static async writeTag(sessionId: string, tagPath: string, request: WriteTagRequest): Promise<WriteTagResponse> {
    const response = await axios.post(`${API_BASE_URL}/sessions/${sessionId}/tags/${tagPath}`, request)
    return response.data
  }

  static async createSnapshot(sessionId: string, name: string): Promise<any> {
    const response = await axios.post(`${API_BASE_URL}/sessions/${sessionId}/snapshots`, { name })
    return response.data
  }

  static async restoreSnapshot(sessionId: string, snapshotId: string): Promise<any> {
    const response = await axios.post(`${API_BASE_URL}/sessions/${sessionId}/snapshots/${snapshotId}/restore`)
    return response.data
  }

  static async getEvents(sessionId: string, from?: Date, to?: Date): Promise<any[]> {
    const params: any = {}
    if (from) params.from = from.toISOString()
    if (to) params.to = to.toISOString()

    const response = await axios.get(`${API_BASE_URL}/sessions/${sessionId}/events`, { params })
    return response.data
  }
}
