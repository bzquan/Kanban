import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'

import App from './App'
import { initI18n } from './i18n'

async function boot(): Promise<void> {
  // Await translations before the first render so no raw keys flash in the AppBar.
  await initI18n()
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </StrictMode>,
  )
}

void boot()
