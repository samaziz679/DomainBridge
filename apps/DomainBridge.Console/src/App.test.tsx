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

    expect(screen.getByRole('heading', { name: 'Domain migration, under control.' })).toBeTruthy()
    expect(screen.getByText('Read-only command center')).toBeTruthy()
    expect(screen.getByText('API healthy')).toBeTruthy()
    expect(screen.getByText('2')).toBeTruthy()
    expect(screen.getByText('enrolled agents')).toBeTruthy()
    expect(screen.getByText('No migration action is available in this console.')).toBeTruthy()
    expect(screen.getAllByText('device-001')).toHaveLength(2)
    expect(screen.getByText('agent.inventory.reported')).toBeTruthy()
  })

  it('shows approved maintenance actions as read-only policy information', () => {
    render(
      <App
        dashboard={{ apiStatus: 'healthy', enrolledAgents: 0, auditEvents: 0, lastAuditAction: 'none' }}
        remediations={[{
          id: 'windows-update-reset',
          displayName: 'Réinitialiser les composants Windows Update',
          description: 'Répare les composants de mise à jour Windows selon une procédure approuvée.',
          requiresApproval: true,
          mayRequireRestart: true
        }]}
      />
    )

    expect(screen.getByRole('heading', { name: 'Maintenance catalogue' })).toBeTruthy()
    expect(screen.getByText('Réinitialiser les composants Windows Update')).toBeTruthy()
    expect(screen.getByText('Approval required')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /run|execute/i })).toBeNull()
  })
})
