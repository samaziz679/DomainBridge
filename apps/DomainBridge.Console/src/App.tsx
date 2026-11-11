export interface DashboardModel {
  apiStatus: 'healthy' | 'unavailable'
  enrolledAgents: number
  auditEvents: number
  lastAuditAction: string
}

export function App({ dashboard }: { dashboard: DashboardModel }) {
  return (
    <main>
      <header>
        <h1>DomainBridge</h1>
        <p>Read-only migration operations overview</p>
      </header>

      <section aria-label="Service status">
        <h2>Service status</h2>
        <p>API {dashboard.apiStatus}</p>
      </section>

      <section aria-label="Agent status">
        <h2>Agent status</h2>
        <p>{dashboard.enrolledAgents} enrolled agents</p>
      </section>

      <section aria-label="Audit status">
        <h2>Audit status</h2>
        <p>{dashboard.auditEvents} audit events</p>
        <p>Latest action: {dashboard.lastAuditAction}</p>
      </section>

      <aside>
        No migration action is available in this console.
      </aside>
    </main>
  )
}
