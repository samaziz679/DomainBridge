export interface Overview {
  apiStatus: 'healthy' | 'unavailable'
  enrolledAgents: number
  auditEvents: number
  lastAuditAction: string
}

export interface AgentRecord {
  deviceId: string
  operatingSystem: string
  architecture: string
  agentVersion: string
  capturedAtUtc: string
}

export interface AuditRecord {
  eventId: string
  occurredAtUtc: string
  action: string
  subjectId: string
}

export interface ConsoleData {
  overview: Overview
  agents: AgentRecord[]
  audit: AuditRecord[]
}

type Fetcher = typeof fetch

async function readJson<T>(fetcher: Fetcher, url: string): Promise<T> {
  const response = await fetcher(url, {
    credentials: 'include',
    headers: { Accept: 'application/json' }
  })

  if (!response.ok) {
    throw new Error(`Control-plane request failed (${response.status})`)
  }

  return response.json() as Promise<T>
}

export async function loadConsoleData(apiBaseUrl: string, fetcher: Fetcher = fetch): Promise<ConsoleData> {
  const baseUrl = apiBaseUrl.replace(/\/$/, '')
  const [overview, agents, audit] = await Promise.all([
    readJson<Overview>(fetcher, `${baseUrl}/api/v1/console/overview`),
    readJson<AgentRecord[]>(fetcher, `${baseUrl}/api/v1/console/agents?limit=25`),
    readJson<AuditRecord[]>(fetcher, `${baseUrl}/api/v1/console/audit?limit=25`)
  ])

  return { overview, agents, audit }
}
