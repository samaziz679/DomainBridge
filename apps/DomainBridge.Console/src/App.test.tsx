import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { App } from './App'

describe('App', () => {
  it('renders a read-only operational overview', () => {
    render(
      <App
        dashboard={{
          apiStatus: 'healthy',
          enrolledAgents: 2,
          auditEvents: 5,
          lastAuditAction: 'agent.enrolled'
        }}
      />
    )

    expect(screen.getByRole('heading', { name: 'Domain migration, under control.' })).toBeTruthy()
    expect(screen.getByText('Read-only command center')).toBeTruthy()
    expect(screen.getByText('API healthy')).toBeTruthy()
    expect(screen.getByText('2')).toBeTruthy()
    expect(screen.getByText('enrolled agents')).toBeTruthy()
    expect(screen.getByText('No migration action is available in this console.')).toBeTruthy()
  })
})
