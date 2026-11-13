import type { DragEndEvent } from '@dnd-kit/core'
import { screen, waitFor, within } from '@testing-library/react'
import i18n from 'i18next'
import { Route, Routes } from 'react-router'
import { afterEach, beforeEach, expect, it } from 'vitest'

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
import { moveRequestFor, type DragEndHandler } from './dnd'

// Task 7: moveRequestFor pure geometry (five cases per the brief's table)
// and the optimistic-move rollback path (Review Focus #1, #3).

function card(id: string, seqNo: number, title = id): CardDto {
  return { id, seqNo, title }
}

const a = card('a', 0)
const b = card('b', 1)
const c = card('c', 2)
const d = card('d', 0)
const e = card('e', 1)

it('moveRequestFor: same column, dropped before overCard → index = overCard position', () => {
  const req = moveRequestFor(
    { step: 2, isWip: true, card: c },
    { step: 2, isWip: true, overCard: a, cards: [a, b, c] },
  )
  expect(req).toEqual({ processStepSeqNo: 2, isWip: true, index: 0 })
})

it('moveRequestFor: same column, dropped after overCard → index = overCard position', () => {
  const req = moveRequestFor(
    { step: 2, isWip: true, card: a },
    { step: 2, isWip: true, overCard: c, cards: [a, b, c] },
  )
  expect(req).toEqual({ processStepSeqNo: 2, isWip: true, index: 2 })
})

it('moveRequestFor: cross column with null overCard → append = destination length', () => {
  const req = moveRequestFor(
    { step: 1, isWip: true, card: a },
    { step: 2, isWip: false, overCard: null, cards: [d, e] },
  )
  expect(req).toEqual({ processStepSeqNo: 2, isWip: false, index: 2 })
})

it('moveRequestFor: cross column onto overCard → index = overCard position', () => {
  const req = moveRequestFor(
    { step: 1, isWip: true, card: a },
    { step: 2, isWip: false, overCard: e, cards: [d, e] },
  )
  expect(req).toEqual({ processStepSeqNo: 2, isWip: false, index: 1 })
})

it('moveRequestFor: drop into empty sub-column → index 0 (Review Focus #3)', () => {
  const req = moveRequestFor(
    { step: 1, isWip: true, card: a },
    { step: 2, isWip: false, overCard: null, cards: [] },
  )
  expect(req).toEqual({ processStepSeqNo: 2, isWip: false, index: 0 })
})

it('moveRequestFor: destination order is seqNo-based regardless of array order', () => {
  // Stub-like out-of-order lists: index must count against the displayed
  // (ascending seqNo) order, matching what the sub-column renders.
  const req = moveRequestFor(
    { step: 1, isWip: true, card: a },
    { step: 2, isWip: true, overCard: d, cards: [e, d] },
  )
  expect(req).toEqual({ processStepSeqNo: 2, isWip: true, index: 0 })
})

// ---- BoardPage integration: optimistic move, rollback on ApiError --------

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
  labelColor: null,
}
const board: BoardDto = {
  id: 'b-1',
  seqNo: 1,
  title: 'Alpha',
  processSteps: [stepBacklog, stepDev],
}

// Mutable server-side table (GET serves it verbatim; the success-path PATCH
// handler mutates it so the refetch returns the moved state, like the API).
let serverTable: Record<string, CardDto[]>
const SERVER_CARDS = () => ({
  '1:true:front': [card('c-1', 0, 'Card one'), card('c-2', 1, 'Card two')],
  '1:false:front': [card('c-3', 0, 'Backlog done')],
  '2:true:front': [card('c-4', 0, 'Dev wip card')],
  '2:false:front': [] as CardDto[],
})

function cardsHandler(req: Request): CardDto[] {
  const params = new URL(req.url).searchParams
  const key = `${params.get('processStepSeqNo')}:${params.get('isWip')}:${params.get('onBoard')}`
  return serverTable[key] ?? []
}

function cardCalls(): RecordedCall[] {
  return calls.filter((c) => c.method === 'GET' && c.url.includes('/cards'))
}
function moveCalls(): RecordedCall[] {
  return calls.filter((c) => c.method === 'PATCH' && c.url.includes('/move'))
}

function dragEnd(
  activeId: string,
  activeData: object,
  overId: string | null,
  overData?: object,
): DragEndEvent {
  return {
    active: { id: activeId, data: { current: activeData } },
    over: overId === null ? null : { id: overId, data: { current: overData } },
  } as unknown as DragEndEvent
}

const CARD_DATA = (step: number, isWip: boolean, cardId: string) => ({
  kind: 'card',
  step,
  isWip,
  cardId,
})
const CONTAINER_DATA = (step: number, isWip: boolean) => ({
  kind: 'container',
  step,
  isWip,
})

function titlesIn(testId: string): string[] {
  return within(screen.getByTestId(testId))
    .getAllByTestId('card')
    .map((el) => el.textContent)
}

function renderPage(handleRef: { current: DragEndHandler | null }) {
  return renderWithProviders(
    <Routes>
      <Route path="/boards/:boardId" element={<BoardPage onDragEndHandleRef={handleRef} />} />
    </Routes>,
    { route: '/boards/b-1' },
  )
}

beforeEach(() => {
  localStorage.clear()
  serverTable = SERVER_CARDS()
  void initI18n()
  void i18n.changeLanguage('en')
})

afterEach(() => {
  restoreFetch()
  localStorage.clear()
})

it('failed move: optimistic change reverts and the cards queries re-fetch (Review Focus #1)', async () => {
  // Deferred 500: hold the PATCH so the optimistic state is observable,
  // then fail it and watch the rollback + refetch.
  let failPatch: (() => void) | null = null
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler,
    'PATCH /api/cards/c-1/move': () =>
      new Promise<Response>((resolve) => {
        failPatch = () => resolve(new Response(null, { status: 500 }))
      }),
  })
  const handleRef = { current: null as DragEndHandler | null }
  renderPage(handleRef)
  await screen.findByText('Card one')
  expect(titlesIn('subcolumn-1-done')).toEqual(['Backlog done'])

  // Cross-column: c-1 (step1/wip) onto c-3 (step1/done) → insert at 0.
  handleRef.current?.(dragEnd('c-1', CARD_DATA(1, true, 'c-1'), 'c-3', CARD_DATA(1, false, 'c-3')))

  // Optimistic: card lands on c-3's slot (before it, per the index=0
  // request) while the PATCH is still in flight.
  await waitFor(() => expect(titlesIn('subcolumn-1-done')).toEqual(['Card one', 'Backlog done']))
  expect(within(screen.getByTestId('subcolumn-1-wip')).queryAllByTestId('card')).toHaveLength(1)
  await waitFor(() => expect(failPatch).not.toBeNull())

  // The drop geometry became the exact MoveCardRequest body.
  await waitFor(() => expect(moveCalls()).toHaveLength(1))
  expect(moveCalls()[0].url).toContain('/api/cards/c-1/move')
  expect(moveCalls()[0].body).toEqual({ processStepSeqNo: 1, isWip: false, index: 0 })

  // Now let the PATCH fail: 500 → rollback + invalidate(['cards']) —
  // re-issued GETs and no ghost card left behind.
  failPatch!()
  await waitFor(() => expect(cardCalls().length).toBeGreaterThan(4))
  await waitFor(() => expect(titlesIn('subcolumn-1-wip')).toEqual(['Card one', 'Card two']))
  expect(titlesIn('subcolumn-1-done')).toEqual(['Backlog done'])
  expect(moveCalls()).toHaveLength(1) // not retried
})

it('successful move into an empty sub-column persists through the refetch (Review Focus #3)', async () => {
  installFetch({
    'GET /api/boards': () => [board],
    'GET /api/boards/b-1/cards': cardsHandler,
    'PATCH /api/cards/c-4/move': () => {
      // Mirror the server: remove from source, insert into dest, renumber
      // (fresh arrays, so cached responses keep their pre-move content).
      serverTable['2:true:front'] = []
      serverTable['2:false:front'] = [card('c-4', 0, 'Dev wip card')]
      return new Response(null, { status: 204 })
    },
  })
  const handleRef = { current: null as DragEndHandler | null }
  renderPage(handleRef)
  await screen.findByText('Dev wip card')

  // Drop c-4 onto the empty Dev Done container → append → index 0.
  handleRef.current?.(dragEnd('c-4', CARD_DATA(2, true, 'c-4'), 'subcolumn-2-done', CONTAINER_DATA(2, false)))
  await waitFor(() => expect(moveCalls()).toHaveLength(1))
  expect(moveCalls()[0].body).toEqual({ processStepSeqNo: 2, isWip: false, index: 0 })

  // After the invalidate(['cards']) refetch, the moved card is still there
  // (server truth), and the source sub-column is empty — no ghost.
  await waitFor(() => expect(cardCalls().length).toBeGreaterThan(4))
  await waitFor(() => expect(titlesIn('subcolumn-2-done')).toEqual(['Dev wip card']))
  expect(within(screen.getByTestId('subcolumn-2-wip')).queryAllByTestId('card')).toHaveLength(0)
})