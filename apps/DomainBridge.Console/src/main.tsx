import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App
      dashboard={{
        apiStatus: 'unavailable',
        enrolledAgents: 0,
        auditEvents: 0,
        lastAuditAction: 'none'
      }}
    />
  </StrictMode>
)
