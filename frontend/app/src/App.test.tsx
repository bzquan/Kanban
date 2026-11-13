import { screen } from '@testing-library/react'

import App from './App'
import { LANG_STORAGE_KEY } from './i18n'
import { renderWithProviders } from './test/renderWithProviders'

// Pin the language before the first initI18n() so the title assertion does not
// depend on jsdom's navigator sniffing.
localStorage.setItem(LANG_STORAGE_KEY, 'en')

it('renders app shell with title and board-list route', async () => {
  renderWithProviders(<App />, { route: '/' })
  expect(await screen.findByTestId('app-bar')).toBeInTheDocument()
  // i18n initialized via renderWithProviders: AppBar shows the translated title.
  expect(await screen.findByRole('heading', { level: 1, name: 'Kanban' })).toBeInTheDocument()
})

it('renders board-list placeholder at /', () => {
  renderWithProviders(<App />, { route: '/' })
  expect(screen.getByTestId('board-list-page')).toBeInTheDocument()
})

it('renders board placeholder at /boards/:boardId', () => {
  renderWithProviders(<App />, { route: '/boards/b-42' })
  expect(screen.getByTestId('board-page')).toBeInTheDocument()
  expect(screen.getByText(/b-42/)).toBeInTheDocument()
})
