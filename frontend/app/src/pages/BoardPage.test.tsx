import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from 'i18next'
import { Route, Routes } from 'react-router'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

// Task 10: BoardPage can host MetricsPanel; jsdom has no canvas, so the
// render hook is stubbed (MetricsPanel's own tests cover option building).
vi.mock('../charts/options', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../charts/options')>()
  return { ...actual, useECharts: () => undefined }
})

import type { BoardDto, CardDto, ProcessStepDto } from '../api/types'
import { initI18n } from '../i18n'
import {
  calls,
  installFetch,
  restoreFetch,
  type RecordedCall,
} from '../test/fetchStub'
import { renderWithProviders } from '../test/renderWithProviders'

import BoardPage from './BoardPage'

// Task 5 fixtures: one board with 2 process steps (WPF-style names that
// already embed "(Limit:N)"), plus an empty backlog-only board.
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
const emptyBoard: BoardDto = {
  id: 'b-2',
  seqNo: 2,
  title: 'Empty',
  processSteps: [stepBacklog],
}

function card(id: string, seqNo: number, title: string): CardDto {
  return { id, seqNo, title }
}

const FRONT_CARDS: Record<string, CardDto[]> = {
  // Deliberately out of seqNo order: the sub-column must display them
  // ascending by intVal(seqNo).
  '1:true:front': [card('c-2', 2, 'Card two'), card('c-1', 1, 'Card one')],
  '1:false:front': [card('c-3', 1, 'Backlog done')],
  '2:true:front': [card('c-4', 1, 'Dev wip card')],
  '2:false:front': [],
}

/** Serves the table entry for (processStepSeqNo, isWip, onBoard); empty otherwise. */
function cardsHandler(table: Record<string, CardDto[]>) {
  return (req: Request): CardDto[] => {
    const params = new URL(req.url).searchParams
    const key = `${params.get('processStepSeqNo')}:${params.get('isWip')}:${params.get('onBoard')}`
    return table[key] ?? []
  }
}

function cardCalls(): RecordedCall[] {
  return calls.filter(
    (c) => c.method === 'GET' && c.url.includes('/cards'),
  )
}

function paramsOf(call: RecordedCall): URLSearchParams {
  return new URL(call.url, 'http://localhost/').searchParams
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
})

it('renders one column per step with verbatim name and wip/done sub-columns, cards in seqNo order', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
  })
  renderPage()

  // Column headers: step.name verbatim (already embeds "(Limit:N)").
  expect(await screen.findByText('Backlog(Limit:∞)')).toBeInTheDocument()
  expect(screen.getByText('Development(Limit:5)')).toBeInTheDocument()
  // Sub-column headers: the step's own wipTitle / doneTitle.
  expect(screen.getByText('In Progress')).toBeInTheDocument()
  expect(screen.getByText('Done')).toBeInTheDocument()
  expect(screen.getByText('Dev WIP')).toBeInTheDocument()
  expect(screen.getByText('Dev Done')).toBeInTheDocument()

  expect(screen.getByTestId('board-title')).toHaveTextContent('Alpha')
  expect(screen.getByTestId('column-1')).toBeInTheDocument()
  expect(screen.getByTestId('column-2')).toBeInTheDocument()
  expect(screen.getAllByTestId(/^column-/)).toHaveLength(2)
  for (const id of ['subcolumn-1-wip', 'subcolumn-1-done', 'subcolumn-2-wip', 'subcolumn-2-done']) {
    expect(screen.getByTestId(id)).toBeInTheDocument()
  }

  // Stub returns seq 2 before seq 1; the sub-column shows them ascending.
  // Cards arrive via their own queries, after the columns are already there.
  await screen.findByText('Card one')
  await screen.findByText('Dev wip card')
  await screen.findByText('Backlog done')
  const wipCards = within(screen.getByTestId('subcolumn-1-wip')).getAllByTestId('card')
  expect(wipCards.map((el) => el.textContent)).toEqual(['Card one', 'Card two'])
  const doneCards = within(screen.getByTestId('subcolumn-1-done')).getAllByTestId('card')
  expect(doneCards.map((el) => el.textContent)).toEqual(['Backlog done'])

  // One cards GET per (step × wip/done), front side, no filter param.
  const cc = cardCalls()
  await waitFor(() => expect(cardCalls()).toHaveLength(4))
  const combos = cc
    .map((c) => {
      const p = paramsOf(c)
      return `${p.get('processStepSeqNo')}:${p.get('isWip')}:${p.get('onBoard')}`
    })
    .sort()
  expect(combos).toEqual([
    '1:false:front',
    '1:true:front',
    '2:false:front',
    '2:true:front',
  ])
  expect(cc.every((c) => paramsOf(c).get('filter') === null)).toBe(true)

  // Add-card buttons reserved for Task 8.
  for (const id of ['add-card-1-wip', 'add-card-1-done', 'add-card-2-wip', 'add-card-2-done']) {
    expect(screen.getByTestId(id)).toBeInTheDocument()
  }

  expect(localStorage.getItem('kanban.lastBoardId')).toBe('b-1')
})

it('front/back toggle re-queries the cards with onBoard=back', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
  })
  renderPage()
  await screen.findByText('Backlog(Limit:∞)')

  const user = userEvent.setup()
  const toggle = screen.getByTestId('board-side-toggle-button')
  expect(toggle).toHaveTextContent('Front board') // ToolTip_BoardFront
  await user.click(toggle)

  await waitFor(() =>
    expect(
      cardCalls().some((c) => paramsOf(c).get('onBoard') === 'back'),
    ).toBe(true),
  )
  expect(toggle).toHaveTextContent('Back board') // ToolTip_BoardBack
})

it('zoom out twice renders scale(0.8) and persists kanban.zoom; reload restores it', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
  })
  const user = userEvent.setup()
  const first = renderPage()
  await screen.findByText('Backlog(Limit:∞)')

  await user.click(screen.getByTestId('zoom-out-button'))
  await user.click(screen.getByTestId('zoom-out-button'))
  expect(screen.getByTestId('board-canvas')).toHaveStyle({ transform: 'scale(0.8)' })
  expect(localStorage.getItem('kanban.zoom')).toBe('0.8')

  // Reload (fresh mount) restores the stored zoom instead of 1.
  first.unmount()
  renderPage()
  await screen.findByText('Backlog(Limit:∞)')
  expect(screen.getByTestId('board-canvas')).toHaveStyle({ transform: 'scale(0.8)' })

  // Zoom back in and reset land on exact tenths (no float drift).
  await user.click(screen.getByTestId('zoom-in-button'))
  expect(screen.getByTestId('board-canvas')).toHaveStyle({ transform: 'scale(0.9)' })
  await user.click(screen.getByTestId('zoom-reset-button'))
  expect(screen.getByTestId('board-canvas')).toHaveStyle({ transform: 'scale(1)' })
  expect(localStorage.getItem('kanban.zoom')).toBe('1')

  // Fit-to-window toggle persists kanban.isZoomed both ways.
  await user.click(screen.getByTestId('zoom-fit-button'))
  expect(localStorage.getItem('kanban.isZoomed')).toBe('true')
  await user.click(screen.getByTestId('zoom-fit-button'))
  expect(localStorage.getItem('kanban.isZoomed')).toBe('false')
})

it('filter text is debounced 300ms into the cards query as the filter param', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
  })
  renderPage()
  await screen.findByText('Backlog(Limit:∞)')
  expect(cardCalls().every((c) => paramsOf(c).get('filter') === null)).toBe(true)

  const user = userEvent.setup()
  await user.type(screen.getByTestId('filter-input'), 'bug fix')

  await waitFor(
    () =>
      expect(
        cardCalls().some((c) => paramsOf(c).get('filter') === 'bug fix'),
      ).toBe(true),
    { timeout: 2000 },
  )
})

it('refresh button invalidates the cards queries', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
  })
  renderPage()
  await screen.findByText('Backlog(Limit:∞)')
  expect(cardCalls()).toHaveLength(4)

  const user = userEvent.setup()
  await user.click(screen.getByTestId('refresh-button'))

  await waitFor(() => expect(cardCalls()).toHaveLength(8))
})

it('malformed boardId renders board-error with a back-to-list link and fetches no cards', async () => {
  installFetch({
    'GET /api/boards': () => [board], // 'xyz' is not in the list
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
  })
  renderPage('xyz')

  expect(await screen.findByTestId('board-error')).toBeInTheDocument()
  // Label goes through format0 + the BoardPage_BoardWithId resx key, not a
  // hand-built concatenation: "Board: {0}" under en.
  expect(screen.getByText('Board: xyz')).toBeInTheDocument()
  expect(screen.queryByTestId('board-canvas')).not.toBeInTheDocument()
  expect(cardCalls()).toHaveLength(0)

  const user = userEvent.setup()
  await user.click(screen.getByTestId('back-to-list-button'))
  expect(await screen.findByTestId('list-stub')).toBeInTheDocument()
})

it('empty board (backlog-only, no cards) renders empty columns without error', async () => {
  installFetch({
    'GET /api/boards': () => [emptyBoard],
    'GET /api/boards/b-2/cards': () => [],
  })
  renderPage('b-2')

  expect(await screen.findByTestId('column-1')).toBeInTheDocument()
  expect(screen.getByTestId('subcolumn-1-wip')).toBeInTheDocument()
  expect(screen.getByTestId('subcolumn-1-done')).toBeInTheDocument()
  expect(screen.queryAllByTestId('card')).toHaveLength(0)
  expect(screen.queryByTestId('board-error')).not.toBeInTheDocument()
})

it('Kanban/Chart toolbar toggle swaps the canvas for the metrics panel and back', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler(FRONT_CARDS),
    'GET /api/boards/b-1/metrics/cfd': () => ({
      days: ['2026-11-01'],
      series: [{ stepName: 'Backlog', cumulativeFlowCounts: [1] }],
    }),
  })
  renderPage()
  await screen.findByText('Backlog(Limit:∞)')
  expect(screen.queryByTestId('metrics-panel')).not.toBeInTheDocument()

  const user = userEvent.setup()
  await user.click(screen.getByTestId('view-chart-button'))
  expect(await screen.findByTestId('metrics-panel')).toBeInTheDocument()
  // Unmount-on-toggle: the kanban canvas is gone while the chart shows.
  expect(screen.queryByTestId('board-canvas')).not.toBeInTheDocument()

  await user.click(screen.getByTestId('view-kanban-button'))
  expect(screen.getByTestId('board-canvas')).toBeInTheDocument()
  expect(screen.queryByTestId('metrics-panel')).not.toBeInTheDocument()
})

// --------------------------------- final review Finding 1: visible create failure

it('add-card POST failure surfaces the localized error and opens no dialog', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': () => [],
    'POST /api/cards': () => new Response('boom', { status: 500 }),
  })
  renderPage()
  await screen.findByText('Development(Limit:5)')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('add-card-1-wip'))

  // The user must know the card was NOT created server-side: a localized
  // error line, not silence.
  const line = await screen.findByTestId('add-card-error')
  expect(line).toHaveTextContent('Failed to create the work item') // BoardPage_AddCardError under en
  expect(screen.queryByTestId('card-edit-dialog')).not.toBeInTheDocument()
})

it('card fetch failures keep the toolbar and column chrome intact', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': () => new Response('boom', { status: 500 }),
  })
  renderPage()

  expect(await screen.findByTestId('column-1')).toBeInTheDocument()
  expect(screen.getByTestId('column-2')).toBeInTheDocument()
  expect(screen.getByTestId('filter-input')).toBeInTheDocument()
  expect(screen.getByTestId('refresh-button')).toBeInTheDocument()
  expect(screen.queryByTestId('board-error')).not.toBeInTheDocument()
})
