import brandMark from './assets/domainbridge-mark.svg'
import './index.css'
import type { AgentRecord, AuditRecord, RemediationRecord } from './api'

export interface DashboardModel {
  apiStatus: 'healthy' | 'unavailable'
  enrolledAgents: number
  auditEvents: number
  lastAuditAction: string
}

export function App({
  dashboard,
  agents = [],
  audit = [],
  remediations = [],
  connectionError
}: {
  dashboard: DashboardModel
  agents?: AgentRecord[]
  audit?: AuditRecord[]
  remediations?: RemediationRecord[]
  connectionError?: string
}) {
  const apiIsHealthy = dashboard.apiStatus === 'healthy'

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label="Primary navigation">
        <div className="brand">
          <img src={brandMark} alt="" className="brand-mark" />
          <div>
            <strong>DomainBridge</strong>
            <span>Migration control plane</span>
          </div>
        </div>

        <div className="workspace-label">Control room</div>

        <nav>
          <a className="nav-item active" href="#overview" aria-current="page">
            <span aria-hidden="true">◈</span> Overview
          </a>
          <a className="nav-item" href="#agents">
            <span aria-hidden="true">⌘</span> Agents
          </a>
          <a className="nav-item" href="#audit">
            <span aria-hidden="true">◷</span> Audit trail
          </a>
          <a className="nav-item" href="#maintenance">
            <span aria-hidden="true">✦</span> Maintenance
          </a>
          <a className="nav-item" href="#pilot">
            <span aria-hidden="true">◇</span> Pilot readiness
          </a>
        </nav>

        <div className="sidebar-foot">
          <span className="eyebrow">Safety mode</span>
          <strong>Read-only</strong>
          <p>Migration actions require a separately approved workflow.</p>
        </div>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p className="eyebrow">DomainBridge · operations workspace</p>
            <h1>Calm control for every endpoint.</h1>
          </div>
          <div className="topbar-meta">
            <span className="operator-chip" role="status" aria-label="Current operating mode">
              <span className="operator-dot" />
              Observation mode
            </span>
            <span className="environment-chip">Pilot</span>
          </div>
        </header>

        <section id="overview" className="hero-card">
          <div>
            <p className="eyebrow">Safe operating posture</p>
            <h2>See the estate. Understand the change. Then decide.</h2>
            <p>
              A single, evidence-led workspace for inventory, maintenance and
              domain migration readiness — with no action hidden behind a click.
            </p>
            <a className="primary-link" href="#maintenance">Review maintenance catalogue <span aria-hidden="true">→</span></a>
          </div>
          <div className="bridge-visual" aria-hidden="true">
            <span className="bridge-domain">A</span>
            <span className="bridge-line" />
            <span className="bridge-domain target">B</span>
          </div>
        </section>

        <section className="metric-grid" aria-label="Service status">
          <article className="metric-card">
            <span className="metric-label">API status</span>
            <strong className={apiIsHealthy ? 'status good' : 'status warning'}>
              <i /> API {dashboard.apiStatus}
            </strong>
            <span className="metric-detail">
              {apiIsHealthy ? 'Secure service responding' : 'Awaiting connection'}
            </span>
          </article>
          <article className="metric-card" id="agents">
            <span className="metric-label">Agent fleet</span>
            <strong>{dashboard.enrolledAgents}</strong>
            <span className="metric-detail">enrolled agents</span>
          </article>
          <article className="metric-card" id="audit">
            <span className="metric-label">Audit evidence</span>
            <strong>{dashboard.auditEvents}</strong>
            <span className="metric-detail">immutable events recorded</span>
          </article>
        </section>

        {connectionError && (
          <p className="connection-notice" role="status">
            Live control-plane data is unavailable. {connectionError}
          </p>
        )}

        <section className="readiness-strip" aria-label="Safe workflow">
          <div className="readiness-copy">
            <span className="step-index">01</span>
            <div><strong>Review the scope</strong><p>Inventory and approved maintenance policies are ready for review.</p></div>
          </div>
          <div className="readiness-divider" aria-hidden="true" />
          <div className="readiness-copy muted">
            <span className="step-index">02</span>
            <div><strong>Approve a campaign</strong><p>Available only when production controls are enabled.</p></div>
          </div>
          <span className="readiness-badge">Guardrails active</span>
        </section>

        <section className="content-grid">
          <article className="panel agents-panel" id="agents">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Technical inventory</p>
                <h2>Agents</h2>
              </div>
              <span className="panel-count">{agents.length} shown</span>
            </div>
            <div className="device-list" aria-label="Managed agents">
              {agents.length === 0 ? (
                <p className="empty-state">No technical inventory has been reported yet.</p>
              ) : agents.map(agent => (
                <div className="device-row" key={agent.deviceId}>
                  <div className="event-icon" aria-hidden="true">⌘</div>
                  <div>
                    <strong>{agent.deviceId}</strong>
                    <p>{agent.operatingSystem} · {agent.architecture} · agent {agent.agentVersion}</p>
                  </div>
                  <time>{new Date(agent.capturedAtUtc).toLocaleDateString()}</time>
                </div>
              ))}
            </div>
          </article>

          <article className="panel audit-panel" id="audit">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Latest evidence</p>
                <h2>Audit trail</h2>
              </div>
              <a href="#audit">View all <span aria-hidden="true">→</span></a>
            </div>
            <div className="audit-list">
              {audit.length === 0 ? (
                <div className="audit-event">
                  <div className="event-icon" aria-hidden="true">✓</div>
                  <div><strong>{dashboard.lastAuditAction}</strong><p>No audit evidence is available yet.</p></div>
                </div>
              ) : audit.slice(0, 3).map(event => (
                <div className="audit-event" key={event.eventId}>
                  <div className="event-icon" aria-hidden="true">✓</div>
                  <div><strong>{event.action}</strong><p>{event.subjectId}</p></div>
                  <time>{new Date(event.occurredAtUtc).toLocaleDateString()}</time>
                </div>
              ))}
            </div>
          </article>

          <article className="panel maintenance-panel" id="maintenance">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Approved operations</p>
                <h2>Maintenance catalogue</h2>
              </div>
              <span className="panel-count">Read-only</span>
            </div>
            <div className="audit-list">
              {remediations.length === 0 ? (
                <p className="empty-state">No approved maintenance actions are available.</p>
              ) : remediations.map(action => (
                <div className="audit-event" key={action.id}>
                  <div className="event-icon" aria-hidden="true">✦</div>
                  <div>
                    <strong>{action.displayName}</strong>
                    <p>{action.description}</p>
                    <span className="policy-tag">{action.requiresApproval ? 'Approval required' : 'Read-only check'}</span>
                    {action.mayRequireRestart && <span className="policy-tag">May restart</span>}
                  </div>
                </div>
              ))}
            </div>
          </article>

          <article className="panel pilot-panel" id="pilot">
            <p className="eyebrow">Pilot guardrail</p>
            <h2>No migration action is available in this console.</h2>
            <p>
              Production changes remain outside this interface until the pilot
              environment, certificate chain and audit store are approved.
            </p>
            <span className="locked-state">● Control locked</span>
          </article>
        </section>
      </section>
    </main>
  )
}
