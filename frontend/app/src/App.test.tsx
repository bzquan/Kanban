import { screen } from '@testing-library/react'
import { afterEach, it } from 'vitest'

import App from './App'
import { LANG_STORAGE_KEY } from './i18n'
import { installFetch, restoreFetch } from './test/fetchStub'
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

it('renders the board page at /boards/:boardId', async () => {
  // The board page fetches meta via GET /api/boards (there is no
  // GET /api/boards/{id}); stub the board, and its empty processSteps mean
  // no cards requests are needed.
  installFetch({
    'GET /api/boards': () => [{ id: 'b-42', title: 'Board 42', processSteps: [] }],
  })
  renderWithProviders(<App />, { route: '/boards/b-42' })
  expect(await screen.findByTestId('board-page')).toBeInTheDocument()
  expect(await screen.findByTestId('board-title')).toHaveTextContent('Board 42')
})

afterEach(() => {
  restoreFetch()
})
