import { AppBar, Toolbar, Typography } from '@mui/material'
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

// Layout route: the AppBar sits above whatever page the router renders into the Outlet.
function Shell() {
  const { t } = useTranslation()
  return (
    <>
      <AppBar position="static" data-testid="app-bar">
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            {/* Main_Title is the WPF window title key (Kanban / 看板 / 看板) */}
            {t('Main_Title')}
          </Typography>
        </Toolbar>
      </AppBar>
      <main>
        <Outlet />
      </main>
    </>
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
