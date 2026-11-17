import i18n from 'i18next'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, it } from 'vitest'

import App from './App'
import { LANG_STORAGE_KEY } from './i18n'
import { cssRulesFor } from './test/cssRules'
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

it('locks the shell to the viewport so page content scrolls inside main', () => {
  renderWithProviders(<App />, { route: '/' })
  // 100vh flex column: AppBar row fixed, main owns the remaining height and
  // its own vertical scroll → the board's horizontal bar sits at the bottom
  // of the screen without scrolling the page (agreed 11-17).
  // cssRulesFor: jsdom's getComputedStyle drops declarations behind emotion's
  // -webkit- duplicates, so the stylesheet text is the reliable contract.
  const shell = screen.getByTestId('app-shell')
  const shellCss = cssRulesFor(shell)
  expect(shellCss).toMatch(/display:flex/)
  expect(shellCss).toMatch(/flex-direction:column/)
  expect(shellCss).toMatch(/height:100vh/)
  // display:flex/column is what makes board-page's flex-grow/flex-basis work —
  // with main left a block, the canvas container collapsed to height 0 and the
  // board rendered blank (whiteout repro 2026-11-17).
  const mainCss = cssRulesFor(screen.getByTestId('app-main'))
  expect(mainCss).toMatch(/display:flex/)
  expect(mainCss).toMatch(/flex-direction:column/)
  expect(mainCss).toMatch(/flex-grow:1/)
  expect(mainCss).toMatch(/min-height:0/)
  expect(mainCss).toMatch(/overflow-y:auto/)
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

it('renders the settings page at /settings', async () => {
  void i18n.changeLanguage('en')
  renderWithProviders(<App />, { route: '/settings' })
  expect(await screen.findByTestId('settings-page')).toBeInTheDocument()
})

it('switching language on the settings page re-renders the AppBar title localized', async () => {
  void i18n.changeLanguage('en')
  renderWithProviders(<App />, { route: '/settings' })
  expect(await screen.findByTestId('settings-page')).toBeInTheDocument()
  expect(screen.getByRole('heading', { level: 1, name: 'Kanban' })).toBeInTheDocument()

  const user = userEvent.setup()
  // MUI v9 Select opens from the inner combobox element, whose accessible name
  // is the aria-label (Setting_Language) — pinned under en before the switch.
  await user.click(screen.getByRole('combobox', { name: 'Language:' }))
  await user.click(await screen.findByRole('option', { name: '中文' }))

  // Shell AppBar (Main_Title), the page label and the combobox name all follow.
  expect(await screen.findByRole('heading', { level: 1, name: '看板' })).toBeInTheDocument()
  expect(await screen.findByText('语言:')).toBeInTheDocument()
  expect(screen.getByRole('combobox', { name: '语言:' })).toBeInTheDocument()
})

it('About page renders at /about with the release-note iframe', async () => {
  void i18n.changeLanguage('en')
  installFetch({
    'GET /release-note/en.html': () =>
      new Response('<h5>Version 1.0</h5>', {
        status: 200,
        headers: { 'content-type': 'text/html' },
      }),
  })
  renderWithProviders(<App />, { route: '/about' })
  expect(await screen.findByTestId('app-bar')).toBeInTheDocument()
  await screen.findByTestId('release-note')
})

afterEach(() => {
  restoreFetch()
  // These tests switch the shared i18n singleton; put it back so test order
  // cannot leak a language into other files.
  void i18n.changeLanguage('en')
  localStorage.clear()
})
