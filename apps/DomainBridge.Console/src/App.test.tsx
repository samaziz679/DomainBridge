import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { App } from './App'

describe('App', () => {
  afterEach(cleanup)

  it('renders a read-only operational overview', () => {
    render(
      <App
        dashboard={{
          apiStatus: 'healthy',
          enrolledAgents: 2,
          auditEvents: 5,
          lastAuditAction: 'agent.enrolled'
        }}
        agents={[{
          deviceId: 'device-001',
          operatingSystem: 'Windows 11',
          architecture: 'X64',
          agentVersion: '1.0',
          capturedAtUtc: '2026-11-11T00:00:00Z'
        }]}
        audit={[{
          eventId: 'event-001',
          occurredAtUtc: '2026-11-11T00:00:00Z',
          action: 'agent.inventory.reported',
          subjectId: 'device-001'
        }]}
      />
    )

    expect(screen.getByRole('heading', { name: 'Calm control for every endpoint.' })).toBeTruthy()
    expect(screen.getByText('Safe operating posture')).toBeTruthy()
    expect(screen.getByText('API healthy')).toBeTruthy()
    expect(screen.getByText('2')).toBeTruthy()
    expect(screen.getByText('enrolled agents')).toBeTruthy()
    expect(screen.getByText('No migration action is available in this console.')).toBeTruthy()
    expect(screen.getAllByText('device-001')).toHaveLength(2)
    expect(screen.getByText('agent.inventory.reported')).toBeTruthy()
  })

  it('guides an operator to the next safe step without exposing execution controls', () => {
    render(
      <App
        dashboard={{ apiStatus: 'healthy', enrolledAgents: 7, auditEvents: 12, lastAuditAction: 'agent.enrolled' }}
      />
    )

    expect(screen.getByText('Control room')).toBeTruthy()
    expect(screen.getByRole('status', { name: 'Current operating mode' }).textContent).toContain('Observation mode')
    expect(screen.getByRole('link', { name: 'Review pilot readiness' })).toBeTruthy()
    expect(screen.queryByText(/maintenance/i)).toBeNull()
    expect(screen.queryByRole('button', { name: /approve|execute|run/i })).toBeNull()
  })
})
