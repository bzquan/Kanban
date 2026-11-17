import { AppBar, Box, CssBaseline, Toolbar, Typography } from '@mui/material'
import { createTheme, ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Outlet, Route, Routes } from 'react-router'

import AboutPage from './pages/AboutPage'
import BoardListPage from './pages/BoardListPage'
import BoardPage from './pages/BoardPage'
import SettingsPage from './pages/SettingsPage'

const theme = createTheme()

// Layout route: a viewport-locked flex column — the AppBar row stays fixed
// and the page renders into a scrollable <main> below it. The board page
// relies on this: its horizontal scrollbar sits at the bottom of the
// viewport, not at the bottom of a page grown by tall columns (agreed 11-17).
function Shell() {
  const { t } = useTranslation()
  return (
    <Box data-testid="app-shell" sx={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
      <AppBar position="static" data-testid="app-bar">
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            {/* Main_Title is the WPF window title key (Kanban / 看板 / 看板) */}
            {t('Main_Title')}
          </Typography>
        </Toolbar>
      </AppBar>
      {/* display:flex/column is load-bearing: board-page's flex-grow/flex-basis
          only work when main is the flex container (omitting it collapsed the
          canvas to height 0 — blank board, repro 2026-11-17). */}
      <Box
        component="main"
        data-testid="app-main"
        sx={{ flexGrow: 1, minHeight: 0, display: 'flex', flexDirection: 'column', overflowY: 'auto' }}
      >
        <Outlet />
      </Box>
    </Box>
  )
}

export default function App() {
  // One QueryClient per App mount; retries off — tests and later tasks own refetching.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
      }),
  )

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        {/* Resets the default body margin — without it the 100vh shell plus
            the 8px margin would put the page's own scrollbar back. */}
        <CssBaseline />
        <Routes>
          <Route element={<Shell />}>
            <Route path="/" element={<BoardListPage />} />
            <Route path="/boards/:boardId" element={<BoardPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/about" element={<AboutPage />} />
          </Route>
        </Routes>
      </ThemeProvider>
    </QueryClientProvider>
  )
}
