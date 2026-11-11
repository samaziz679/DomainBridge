import brandMark from './assets/domainbridge-mark.svg'
import './index.css'

export interface DashboardModel {
  apiStatus: 'healthy' | 'unavailable'
  enrolledAgents: number
  auditEvents: number
  lastAuditAction: string
}

export function App({ dashboard }: { dashboard: DashboardModel }) {
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
            <p className="eyebrow">Operations workspace</p>
            <h1>Domain migration, under control.</h1>
          </div>
          <div className="operator-chip">
            <span className="operator-dot" />
            Pilot environment
          </div>
        </header>

        <section id="overview" className="hero-card">
          <div>
            <p className="eyebrow">Read-only command center</p>
            <h2>Observe every step before you change anything.</h2>
            <p>
              DomainBridge connects inventory, agent health and audit evidence
              into a single, deliberately non-destructive view.
            </p>
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

        <section className="content-grid">
          <article className="panel audit-panel">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Latest evidence</p>
                <h2>Audit trail</h2>
              </div>
              <a href="#audit">View all <span aria-hidden="true">→</span></a>
            </div>
            <div className="audit-event">
              <div className="event-icon" aria-hidden="true">✓</div>
              <div>
                <strong>{dashboard.lastAuditAction}</strong>
                <p>Latest controlled event from the DomainBridge platform.</p>
              </div>
              <time>Just now</time>
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
