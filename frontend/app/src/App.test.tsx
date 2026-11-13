import { screen } from '@testing-library/react'

import App from './App'
import { renderWithProviders } from './test/renderWithProviders'

it('renders app shell with title and board-list route', async () => {
  renderWithProviders(<App />, { route: '/' })
  expect(await screen.findByTestId('app-bar')).toBeInTheDocument()
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
