import { useEffect, useState } from 'react'
import { App, type DashboardModel } from './App'
import { loadConsoleData, type ConsoleData } from './api'

const unavailableDashboard: DashboardModel = {
  apiStatus: 'unavailable',
  enrolledAgents: 0,
  auditEvents: 0,
  lastAuditAction: 'none'
}

export function LiveConsole({ apiBaseUrl }: { apiBaseUrl: string }) {
  const [data, setData] = useState<ConsoleData>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    let active = true
    loadConsoleData(apiBaseUrl)
      .then(result => active && setData(result))
      .catch(reason => active && setError(reason instanceof Error ? reason.message : 'Control-plane unavailable'))

    return () => { active = false }
  }, [apiBaseUrl])

  return (
    <App
      dashboard={data?.overview ?? unavailableDashboard}
      agents={data?.agents ?? []}
      audit={data?.audit ?? []}
      connectionError={error}
    />
  )
}
