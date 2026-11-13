import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createTheme, ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import i18n from 'i18next'
import { Route, Routes } from 'react-router'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import type { BoardDto, CardDto, ProcessStepDto } from '../api/types'
import { initI18n } from '../i18n'
import BoardPage from '../pages/BoardPage'
import {
  calls,
  installFetch,
  restoreFetch,
  type RecordedCall,
} from '../test/fetchStub'
import { renderWithProviders } from '../test/renderWithProviders'

import CardEditDialog from './CardEditDialog'
import CardView from './CardView'

// ---------------------------------------------------------------- fixtures

const stepBacklog: ProcessStepDto = {
  phaseSeqNo: 1,
  name: 'Backlog(Limit:∞)',
  wipTitle: 'In Progress',
  doneTitle: 'Done',
  labelColor: null,
}
const stepDev: ProcessStepDto = {
  phaseSeqNo: 2,
  name: 'Development(Limit:5)',
  wipTitle: 'Dev WIP',
  doneTitle: 'Dev Done',
  labelColor: 'SkyBlue',
}
const board: BoardDto = {
  id: 'b-1',
  seqNo: 1,
  title: 'Alpha',
  description: 'First board',
  developers: 'Ann;Bob',
  processSteps: [stepBacklog, stepDev],
}

/** Every CardPatch-relevant field set, so the prefill test covers all 13. */
function fullCard(): CardDto {
  return {
    id: 'c-9',
    seqNo: 3,
    title: 'Login crash',
    tag: 'auth',
    executors: 'Ann;Bob',
    summary: '<b>sum</b>',
    testCases: 'tc1',
    designOverview: 'do1',
    cardType: 'BugFix',
    storyPoints: '8', // int64 union arrives as string
    // Naive local: parsed browser-locally, so the date input shows
    // 2026-06-15 in any browser timezone.
    releaseDate: '2026-06-15T12:00:00',
    isBlocked: true,
    isMergedIntoMaster: false,
    isMergedIntoMajorBranch: true,
    isScheduledToRelease: false,
  }
}

function callOf(method: string, url: string): RecordedCall | undefined {
  return calls.find((c) => c.method === method && c.url === url)
}

// The dialog invalidates ['cards'] on every success; the shared harness owns
// its QueryClient, so these tests mount with their own and spy on it.
function renderDialog(card: CardDto, onClose = () => {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  void initI18n()
  render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={createTheme()}>
        <CardEditDialog card={card} onClose={onClose} />
      </ThemeProvider>
    </QueryClientProvider>,
  )
  return { queryClient, invalidate }
}

function renderPage(boardId = 'b-1') {
  return renderWithProviders(
    <Routes>
      <Route path="/" element={<div data-testid="list-stub" />} />
      <Route path="/boards/:boardId" element={<BoardPage />} />
    </Routes>,
    { route: `/boards/${boardId}` },
  )
}

beforeEach(() => {
  localStorage.clear()
  void initI18n()
  void i18n.changeLanguage('en')
})

afterEach(() => {
  restoreFetch()
  localStorage.clear()
  vi.restoreAllMocks()
})

// ---------------------------------------------------------------- pinned: create

it('add-card button POSTs {boardId, processStepSeqNo, isWip} and opens the dialog on the returned card', async () => {
  const created: CardDto = { id: 'c-new', seqNo: 1, title: 'Fresh card' }
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': () => [],
    'POST /api/cards': () => created,
    'PATCH /api/cards/c-new': () => ({ ...created, title: 'Named now' }),
  })
  renderPage()
  await screen.findByText('Development(Limit:5)')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('add-card-2-wip'))

  await waitFor(() =>
    expect(callOf('POST', '/api/cards')).toBeDefined(),
  )
  expect(callOf('POST', '/api/cards')?.body).toEqual({
    boardId: 'b-1',
    processStepSeqNo: 2,
    isWip: true,
  })

  // The dialog opens on the *returned* card (its prefilled title proves it).
  expect(await screen.findByDisplayValue('Fresh card')).toBeInTheDocument()

  // Editing the created card PATCHes the new id — the dialog is bound to the
  // POST response, not to some earlier card.
  await user.clear(screen.getByTestId('field-title'))
  await user.type(screen.getByTestId('field-title'), 'Named now')
  await user.click(screen.getByTestId('card-edit-save'))
  await waitFor(() =>
    expect(callOf('PATCH', '/api/cards/c-new')).toBeDefined(),
  )
  expect(callOf('PATCH', '/api/cards/c-new')?.body).toEqual({
    title: 'Named now',
  })
})

// ---------------------------------------------------------------- pinned: prefill

it('opening the dialog prefills all 13 editable fields', () => {
  renderDialog(fullCard())

  expect(screen.getByTestId('field-title')).toHaveValue('Login crash')
  expect(screen.getByTestId('field-tag')).toHaveValue('auth')
  expect(screen.getByTestId('field-executors')).toHaveValue('Ann;Bob')
  expect(screen.getByTestId('field-cardType')).toHaveValue('BugFix')
  expect(screen.getByTestId('field-storyPoints')).toHaveValue('8')
  expect(screen.getByTestId('field-releaseDate')).toHaveValue('2026-06-15')
  expect(screen.getByTestId('field-summary')).toHaveValue('<b>sum</b>')
  expect(screen.getByTestId('field-testCases')).toHaveValue('tc1')
  expect(screen.getByTestId('field-designOverview')).toHaveValue('do1')

  expect(screen.getByRole('checkbox', { name: 'Blocked' })).toBeChecked()
  expect(screen.getByRole('checkbox', { name: 'Merged into master' })).not.toBeChecked()
  expect(screen.getByRole('checkbox', { name: 'Merged into major branch' })).toBeChecked()
  expect(screen.getByRole('checkbox', { name: 'Scheduled to release' })).not.toBeChecked()
})

// ---------------------------------------------------------------- pinned: PATCH diff

it('Save PATCHes only the changed fields (title edited → body is exactly {title})', async () => {
  installFetch({
    'PATCH /api/cards/c-9': () => ({ ...fullCard(), title: 'Renamed' }),
  })
  const { invalidate } = renderDialog(fullCard())

  const user = userEvent.setup()
  await user.clear(screen.getByTestId('field-title'))
  await user.type(screen.getByTestId('field-title'), 'Renamed')
  await user.click(screen.getByTestId('card-edit-save'))

  await waitFor(() => expect(callOf('PATCH', '/api/cards/c-9')).toBeDefined())
  // openapi-fetch sends the CardPatch body verbatim: only changed keys.
  expect(callOf('PATCH', '/api/cards/c-9')?.body).toEqual({ title: 'Renamed' })
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ['cards'] })
})

it('Save sends releaseDate as local midnight only when the date was changed', async () => {
  installFetch({
    'PATCH /api/cards/c-9': () => fullCard(),
  })
  renderDialog(fullCard())

  const user = userEvent.setup()
  // jsdom has no date-picker keyboard model; drive the onChange directly.
  fireEvent.change(screen.getByTestId('field-releaseDate'), {
    target: { value: '2026-07-04' },
  })
  await user.click(screen.getByTestId('card-edit-save'))

  await waitFor(() => expect(callOf('PATCH', '/api/cards/c-9')).toBeDefined())
  // Naive-local payload: the running API parses it in server-local time and
  // GETs it back as the same wall-clock date.
  expect(callOf('PATCH', '/api/cards/c-9')?.body).toEqual({
    releaseDate: '2026-07-04T00:00:00',
  })
})

it('clearing the release date PATCHes today (WPF TargetNullValue parity), not null', async () => {
  installFetch({
    'PATCH /api/cards/c-9': () => fullCard(),
  })
  renderDialog(fullCard())

  const user = userEvent.setup()
  // jsdom has no date-picker keyboard model; drive the onChange directly.
  fireEvent.change(screen.getByTestId('field-releaseDate'), { target: { value: '' } })
  await user.click(screen.getByTestId('card-edit-save'))

  await waitFor(() => expect(callOf('PATCH', '/api/cards/c-9')).toBeDefined())
  // The API ignores null (verified live 2026-11-13), so a clear must write
  // TODAY's local calendar date like WPF's DatePicker
  // TargetNullValue={x:Static System:DateTime.Now} — same naive-local format
  // as the date-chosen path.
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  const today = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
  expect(callOf('PATCH', '/api/cards/c-9')?.body).toEqual({
    releaseDate: `${today}T00:00:00`,
  })
})

// ---------------------------------------------------------------- pinned: delete

it('delete confirms then calls DELETE /api/cards/{id}; cancelling deletes nothing', async () => {
  installFetch({
    'DELETE /api/cards/c-9': () => new Response(null, { status: 204 }),
  })
  const { invalidate } = renderDialog(fullCard())

  const user = userEvent.setup()
  const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false)
  await user.click(screen.getByTestId('card-edit-delete'))
  expect(confirmSpy).toHaveBeenCalledTimes(1)
  // The .NET confirm text goes through the existing resx keys: <{0}> carries the title.
  expect(confirmSpy).toHaveBeenCalledWith('Do you really delete <Login crash>?')
  expect(callOf('DELETE', '/api/cards/c-9')).toBeUndefined()
  // Cancelled: the dialog stays open.
  expect(screen.getByTestId('field-title')).toBeInTheDocument()

  confirmSpy.mockReturnValue(true)
  await user.click(screen.getByTestId('card-edit-delete'))
  await waitFor(() => expect(callOf('DELETE', '/api/cards/c-9')).toBeDefined())
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ['cards'] })
})

// ---------------------------------------------------------------- pinned: storyPoints validation

it('invalid storyPoints text shows the localized validation error and blocks Save', async () => {
  installFetch({
    'PATCH /api/cards/c-9': () => fullCard(),
  })
  renderDialog(fullCard())

  const user = userEvent.setup()
  await user.clear(screen.getByTestId('field-storyPoints'))
  await user.type(screen.getByTestId('field-storyPoints'), 'abc')
  await user.click(screen.getByTestId('card-edit-save'))

  // Localized error (CardEdit_StoryPointsInvalid via t()), not a raw key, not English-hardcoded.
  expect(await screen.findByText('Invalid story points')).toBeInTheDocument()
  expect(screen.queryByText('CardEdit_StoryPointsInvalid')).not.toBeInTheDocument()
  // Save is blocked: no PATCH request left the dialog.
  await new Promise((r) => setTimeout(r, 50))
  expect(callOf('PATCH', '/api/cards/c-9')).toBeUndefined()
  expect(screen.getByTestId('field-title')).toBeInTheDocument()

  // Fixing the text clears the error and Save works.
  await user.clear(screen.getByTestId('field-storyPoints'))
  await user.type(screen.getByTestId('field-storyPoints'), '13')
  await user.click(screen.getByTestId('card-edit-save'))
  await waitFor(() => expect(callOf('PATCH', '/api/cards/c-9')).toBeDefined())
  expect(callOf('PATCH', '/api/cards/c-9')?.body).toEqual({ storyPoints: 13 })
})

// ---------------------------------------------------------------- wiring: card click / keyboard

it('clicking a card on the board opens the edit dialog prefilled with that card', async () => {
  const card: CardDto = { id: 'c-1', seqNo: 1, title: 'Card one', storyPoints: 5 }
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': (req) => {
      const params = new URL(req.url).searchParams
      const key = `${params.get('processStepSeqNo')}:${params.get('isWip')}`
      return key === '1:true' ? [card] : []
    },
  })
  renderPage()
  await screen.findByText('Card one')

  const user = userEvent.setup()
  await user.click(screen.getByText('Card one'))

  expect(await screen.findByTestId('field-title')).toHaveValue('Card one')
  expect(screen.getByTestId('field-storyPoints')).toHaveValue('5')
})

// ------------------------------------------------- carry 1: keyboard-openable card

it('CardView is keyboard-openable: role=button, tabIndex 0, Enter and Space fire onOpen', () => {
  const onOpen = vi.fn()
  renderWithProviders(<CardView card={{ id: 'c-1', seqNo: 1, title: 'Kick' }} onOpen={onOpen} />)
  const card = screen.getByTestId('card')
  expect(card).toHaveAttribute('role', 'button')
  expect(card).toHaveAttribute('tabindex', '0')
  fireEvent.keyDown(card, { key: 'Enter' })
  expect(onOpen).toHaveBeenCalledTimes(1)
  fireEvent.keyDown(card, { key: ' ' })
  expect(onOpen).toHaveBeenCalledTimes(2)
})

// ------------------------------------------------- summary <details> (spec §4.2)

it('CardView renders summary HTML raw in an expandable details region', () => {
  renderWithProviders(
    <CardView card={{ id: 'c-1', seqNo: 1, title: 'Kick', summary: '<b>bold summary</b>' }} />,
  )
  const details = screen.getByTestId('card-summary')
  expect(details.tagName).toBe('DETAILS')
  const bold = details.querySelector('b')
  expect(bold).not.toBeNull() // raw HTML, not escaped text
  expect(bold).toHaveTextContent('bold summary')
})

it('CardView renders no details region when summary is absent', () => {
  renderWithProviders(<CardView card={{ id: 'c-1', seqNo: 1, title: 'Kick' }} />)
  expect(screen.queryByTestId('card-summary')).not.toBeInTheDocument()
})

// ------------------------------------------------- review fix: summary is reachable

it('summary click expands the details region without firing onOpen (no bubble to card root)', () => {
  const onOpen = vi.fn()
  renderWithProviders(
    <CardView
      card={{ id: 'c-1', seqNo: 1, title: 'Kick', summary: '<b>bold summary</b>' }}
      onOpen={onOpen}
    />,
  )
  const details = screen.getByTestId('card-summary')
  const summary = details.querySelector('summary')
  expect(summary).not.toBeNull()
  expect(details).not.toHaveAttribute('open')

  // Native UA toggle (jsdom implements it) must survive…
  expect(fireEvent.click(summary!)).toBe(true) // not preventDefault()ed
  expect(details).toHaveAttribute('open')
  // …and the card root's click-to-edit must NOT see this click.
  expect(onOpen).not.toHaveBeenCalled()
})

it('summary keyboard Enter/Space never leaks to the card open handler and stays un-prevented', () => {
  const onOpen = vi.fn()
  renderWithProviders(
    <CardView
      card={{ id: 'c-1', seqNo: 1, title: 'Kick', summary: '<b>bold summary</b>' }}
      onOpen={onOpen}
    />,
  )
  const details = screen.getByTestId('card-summary')
  const summary = details.querySelector('summary')
  expect(summary).not.toBeNull()

  // jsdom does not synthesize keyboard activation, so the native toggle
  // itself is pinned by "not canceled" (the root handler used to swallow
  // Enter/Space with preventDefault, killing it) plus no dialog opening.
  expect(fireEvent.keyDown(summary!, { key: 'Enter' })).toBe(true)
  expect(fireEvent.keyDown(summary!, { key: ' ' })).toBe(true)
  expect(onOpen).not.toHaveBeenCalled()
})

it('clicking the card body (title) still opens the dialog with the summary present', () => {
  const onOpen = vi.fn()
  renderWithProviders(
    <CardView
      card={{ id: 'c-1', seqNo: 1, title: 'Kick', summary: '<b>bold summary</b>' }}
      onOpen={onOpen}
    />,
  )
  fireEvent.click(screen.getByText('Kick'))
  expect(onOpen).toHaveBeenCalledTimes(1)
})

// ------------------------------------------------- create failure keeps board usable

it('create failure on add-card leaves the board usable and opens no dialog', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': () => [],
    'POST /api/cards': () => new Response('boom', { status: 500 }),
  })
  renderPage()
  await screen.findByText('Development(Limit:5)')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('add-card-1-done'))

  await waitFor(() => expect(callOf('POST', '/api/cards')).toBeDefined())
  expect(screen.queryByTestId('field-title')).not.toBeInTheDocument()
  expect(screen.getByTestId('board-canvas')).toBeInTheDocument()
})