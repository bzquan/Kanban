import { render } from '@testing-library/react'
import { createTheme, ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import type { ReactElement } from 'react'

import { initI18n } from '../i18n'

const theme = createTheme()

interface RenderWithProvidersOptions {
  route?: string
}

// Test render harness: providers the whole app needs, minus the BrowserRouter
// (MemoryRouter here lets each test pick its own initial URL).
export function renderWithProviders(
  ui: ReactElement,
  { route = '/' }: RenderWithProvidersOptions = {},
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  // Idempotent and synchronous here (initImmediate: false in src/i18n), so t()
  // resolves in the render below — component tests get real translations.
  void initI18n()
  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  )
}
