import { describe, expect, it, vi } from 'vitest'
import { loadConsoleData } from './api'

describe('loadConsoleData', () => {
  it('maps the read-only API responses into console data', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        apiStatus: 'healthy', enrolledAgents: 2, auditEvents: 4, lastAuditAction: 'agent.inventory.reported'
      }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ deviceId: 'device-001', operatingSystem: 'Windows 11', architecture: 'X64', agentVersion: '1.0', capturedAtUtc: '2026-11-11T00:00:00Z' }]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ eventId: 'event-001', occurredAtUtc: '2026-11-11T00:00:00Z', action: 'agent.inventory.reported', subjectId: 'device-001' }]), { status: 200 }))

    const result = await loadConsoleData('https://control.example', fetcher)

    expect(result.overview.enrolledAgents).toBe(2)
    expect(result.agents[0].deviceId).toBe('device-001')
    expect(result.audit[0].action).toBe('agent.inventory.reported')
    expect(fetcher).toHaveBeenCalledTimes(3)
  })

  it('fails closed when the control-plane API is unavailable', async () => {
    const fetcher = vi.fn().mockRejectedValue(new Error('network unavailable'))

    await expect(loadConsoleData('https://control.example', fetcher)).rejects.toThrow('network unavailable')
  })
})
