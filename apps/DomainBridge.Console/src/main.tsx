import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { LiveConsole } from './LiveConsole'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <LiveConsole apiBaseUrl={import.meta.env.VITE_DOMAINBRIDGE_API_URL ?? window.location.origin} />
  </StrictMode>
)
