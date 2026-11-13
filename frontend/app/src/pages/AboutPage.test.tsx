import i18n from 'i18next'
import { screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it } from 'vitest'

import { initI18n } from '../i18n'
import { calls, installFetch, restoreFetch } from '../test/fetchStub'
import { renderWithProviders } from '../test/renderWithProviders'

import AboutPage from './AboutPage'

// Fixtures shaped like the real public/release-note/*.html fragments
// (bare <h5>/<ol> markup copied verbatim from Kanban/ReleaseNote/).
const EN_HTML = '<h5>Version 3.1.3 - 2026.06.19</h5><ol><li>Improvement：EN fixture</li></ol>'
const JA_HTML = '<h5>Version 3.1.3 - 2026.06.19</h5><ol><li>Improvement：日本語 fixture</li></ol>'

function html(body: string): Response {
  return new Response(body, {
    status: 200,
    headers: { 'content-type': 'text/html' },
  })
}

beforeEach(() => {
  localStorage.clear()
  void initI18n()
})

afterEach(() => {
  restoreFetch()
  void i18n.changeLanguage('en')
  localStorage.clear()
})

it('renders the en release note into a srcDoc iframe (never src) under en', async () => {
  void i18n.changeLanguage('en')
  installFetch({ 'GET /release-note/en.html': () => html(EN_HTML) })
  renderWithProviders(<AboutPage />)

  // AboutControl_ReleaseNote label under en.
  expect(await screen.findByText('Release Note:')).toBeInTheDocument()
  const frame = await screen.findByTestId('release-note')
  expect(frame.tagName).toBe('IFRAME')
  // srcDoc isolation contract: content is injected, no URL is loaded.
  await waitFor(() => expect(frame).toHaveAttribute('srcdoc', EN_HTML))
  expect(frame).not.toHaveAttribute('src')
  expect(calls.some((c) => c.url === '/release-note/en.html')).toBe(true)
})

it('renders the ja html when the language is ja', async () => {
  void i18n.changeLanguage('ja')
  installFetch({ 'GET /release-note/ja.html': () => html(JA_HTML) })
  renderWithProviders(<AboutPage />)

  await waitFor(() =>
    expect(screen.getByTestId('release-note')).toHaveAttribute('srcdoc', JA_HTML),
  )
  // Only the ja asset is fetched when the UI language is ja.
  expect(calls.map((c) => c.url)).toEqual(['/release-note/ja.html'])
})

it('falls back to the en html for zh', async () => {
  void i18n.changeLanguage('zh')
  installFetch({ 'GET /release-note/en.html': () => html(EN_HTML) })
  renderWithProviders(<AboutPage />)

  await waitFor(() =>
    expect(screen.getByTestId('release-note')).toHaveAttribute('srcdoc', EN_HTML),
  )
})

it('shows a localized load error when the fetch fails', async () => {
  void i18n.changeLanguage('en')
  installFetch({
    'GET /release-note/en.html': () => new Response('nope', { status: 404 }),
  })
  renderWithProviders(<AboutPage />)

  // About_LoadError (new resx key).
  expect(await screen.findByText('Failed to load release note')).toBeInTheDocument()
})
