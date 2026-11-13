import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createTheme, ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import i18n from 'i18next'
import type { ReactElement } from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import type { BoardDto, ProcessStepDto } from '../api/types'
import { initI18n } from '../i18n'
import {
  calls,
  installFetch,
  restoreFetch,
  type RecordedCall,
} from '../test/fetchStub'

import ProcessStepEditor, { type ReorderFn } from './ProcessStepEditor'

// Task 9: process-step editor — PUT body contract (full renumbered array),
// cycle-time radio invariant (at most one start, one end — or neither),
// remove guard at the last step, and the 409 conflict flow.

function step(
  phaseSeqNo: number,
  name: string,
  extra: Partial<ProcessStepDto> = {},
): ProcessStepDto {
  return {
    phaseSeqNo,
    name,
    labelColor: 'Gold',
    wipTitle: 'In Progress',
    doneTitle: 'Done',
    isStartOfCycleTime: false,
    isEndOfCycleTime: false,
    ...extra,
  }
}

// Deliberately NOT array-ordered (2,0,1): the editor must display and save
// in ascending phaseSeqNo, not source-array order.
const backlog = step(0, 'Backlog(Limit:∞)', { labelColor: 'Aqua' })
const dev = step(1, 'Development(Limit:5)', { isStartOfCycleTime: true })
const test = step(2, 'Test(Limit:3)', { labelColor: 'Teal', isEndOfCycleTime: true })

function boardWith(steps: ProcessStepDto[]): BoardDto {
  return { id: 'b-1', seqNo: 1, title: 'Alpha', processSteps: steps }
}

// The editor takes `board` as a prop (no query of its own) but invalidates
// queries on success — its own QueryClient + invalidate spy, same approach
// as CardEditDialog.test.tsx's renderDialog.
function renderEditor(
  board: BoardDto,
  onClose = () => {},
): { reorderRef: { current: ReorderFn | null }; invalidate: ReturnType<typeof vi.spyOn> } {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const reorderRef: { current: ReorderFn | null } = { current: null }
  void initI18n()
  const ui: ReactElement = (
    <ProcessStepEditor board={board} open onClose={onClose} onReorderRef={reorderRef} />
  )
  render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={createTheme()}>{ui}</ThemeProvider>
    </QueryClientProvider>,
  )
  return { reorderRef, invalidate }
}

function putCalls(): RecordedCall[] {
  return calls.filter(
    (c) => c.method === 'PUT' && c.url === '/api/boards/b-1/process-steps',
  )
}

function startRadios(): HTMLElement[] {
  return screen.getAllByRole('radio', { name: 'Begin of metrics' })
}
function endRadios(): HTMLElement[] {
  return screen.getAllByRole('radio', { name: 'End of metrics' })
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

// ------------------------------------------------------------- pinned: save

it('Save PUTs the FULL array in the reordered order, carrying original phaseSeqNo identities', async () => {
  installFetch({
    'PUT /api/boards/b-1/process-steps': () => boardWith([]),
  })
  const { reorderRef } = renderEditor(boardWith([test, backlog, dev]))

  // Display order is phaseSeqNo-ascending despite the shuffled array.
  await waitFor(() =>
    expect(screen.getByTestId('step-name-0')).toHaveValue('Backlog(Limit:∞)'),
  )
  expect(screen.getByTestId('step-name-1')).toHaveValue('Development(Limit:5)')
  expect(screen.getByTestId('step-name-2')).toHaveValue('Test(Limit:3)')

  // Move Backlog (index 0) to the end (dnd-kit reorder — jsdom cannot
  // simulate pointer drags, so the real onDragEnd path is driven through the
  // same export-the-handler seam BoardPage uses).
  reorderRef.current!(0, 2)

  const user = userEvent.setup()
  await user.click(screen.getByTestId('step-save-button'))

  await waitFor(() => expect(putCalls()).toHaveLength(1))
  // Server contract (BoardCommandService.UpdateProcessStepsAsync + its
  // UpdateProcessSteps_Reorder test): phaseSeqNo is the column's ORIGINAL
  // identity — the server maps it back and moves the column's cards — while
  // the array order is the new display order. The server compacts SeqNo to
  // 0..n-1 itself; client-side renumbering would rotate labels over cards
  // that never moved, so the identities MUST travel with the rows.
  expect(putCalls()[0].body).toEqual([
    step(1, 'Development(Limit:5)', { isStartOfCycleTime: true }),
    step(2, 'Test(Limit:3)', { labelColor: 'Teal', isEndOfCycleTime: true }),
    step(0, 'Backlog(Limit:∞)', { labelColor: 'Aqua' }),
  ])
})

it('all fields editable: name, labelColor swatch select, wipTitle, doneTitle reach the PUT body', async () => {
  installFetch({
    'PUT /api/boards/b-1/process-steps': () => boardWith([]),
  })
  renderEditor(boardWith([backlog, dev, test]))
  await screen.findByTestId('step-name-0')

  const user = userEvent.setup()
  await user.clear(screen.getByTestId('step-name-0'))
  await user.type(screen.getByTestId('step-name-0'), 'Analysis(Limit:2)')
  await user.selectOptions(screen.getByTestId('step-color-0'), 'Tomato')
  await user.clear(screen.getByTestId('step-wip-0'))
  await user.type(screen.getByTestId('step-wip-0'), 'Wipping')
  await user.clear(screen.getByTestId('step-done-0'))
  await user.type(screen.getByTestId('step-done-0'), 'Finished')
  await user.click(screen.getByTestId('step-save-button'))

  await waitFor(() => expect(putCalls()).toHaveLength(1))
  const body = putCalls()[0].body as ProcessStepDto[]
  expect(body[0]).toEqual(
    step(0, 'Analysis(Limit:2)', {
      labelColor: 'Tomato',
      wipTitle: 'Wipping',
      doneTitle: 'Finished',
    }),
  )
  expect(body[1]).toEqual(
    step(1, 'Development(Limit:5)', { isStartOfCycleTime: true }),
  )
  // The swatch shows the picked named color.
  expect(screen.getByTestId('step-swatch-0')).toHaveAttribute('data-color', 'Tomato')
})

// --------------------------------------------------- pinned: radio invariant

it('selecting a second start-of-cycle radio moves the first (radio invariant)', async () => {
  installFetch({})
  renderEditor(boardWith([backlog, dev, test]))
  await screen.findByTestId('step-name-0')

  const user = userEvent.setup()
  expect(startRadios()[1]).toBeChecked() // Development starts the cycle
  await user.click(startRadios()[2])

  expect(startRadios()[1]).not.toBeChecked()
  expect(startRadios()[2]).toBeChecked()
  // The end marker is a separate group — untouched.
  expect(endRadios()[2]).toBeChecked()
  expect(endRadios()[0]).not.toBeChecked()

  await user.click(endRadios()[0])
  expect(endRadios()[2]).not.toBeChecked()
  expect(endRadios()[0]).toBeChecked()
})

it('a single step with no markers shows neither start nor end checked', async () => {
  installFetch({})
  renderEditor(boardWith([step(0, 'Solo')]))
  await screen.findByTestId('step-name-0')
  expect(startRadios()[0]).not.toBeChecked()
  expect(endRadios()[0]).not.toBeChecked()
})

// ------------------------------------------------------------ pinned: guard

it('Remove is disabled at one remaining step, with the localized reason', async () => {
  installFetch({})
  renderEditor(boardWith([step(0, 'Solo')]))
  const remove = await screen.findByTestId('step-remove-0')
  expect(remove).toBeDisabled()
  expect(remove).toHaveAttribute(
    'title',
    'The last remaining process step cannot be removed',
  )
  // A disabled button swallows pointer input entirely (userEvent refuses it);
  // the row is still there and the guard holds.
  expect(screen.getByTestId('step-name-0')).toBeInTheDocument()
})

it('add appends a WPF-default step; remove drops a row', async () => {
  installFetch({
    'PUT /api/boards/b-1/process-steps': () => boardWith([]),
  })
  renderEditor(boardWith([backlog, dev, test]))
  await screen.findByTestId('step-name-0')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('step-add-button'))
  expect(screen.getByTestId('step-name-3')).toHaveValue('NewWork(Limit:1)')
  expect(screen.getByTestId('step-wip-3')).toHaveValue('In Progress')
  expect(screen.getByTestId('step-done-3')).toHaveValue('Done')
  expect(screen.getByTestId('step-color-3')).toHaveValue('CornflowerBlue')

  await user.click(screen.getByTestId('step-remove-3'))
  await waitFor(() =>
    expect(screen.queryByTestId('step-name-3')).not.toBeInTheDocument(),
  )
  expect(screen.getByTestId('step-remove-2')).toBeEnabled()

  // Additions get identities above every original (the server reads
  // phaseSeqNo >= current count as an addition), never a renumbered slot
  // that could overwrite a surviving column — proven by saving and reading
  // the exact PUT body.
  await user.click(screen.getByTestId('step-add-button'))
  await user.click(screen.getByTestId('step-save-button'))
  await waitFor(() => expect(putCalls()).toHaveLength(1))
  const body = putCalls()[0].body as ProcessStepDto[]
  expect(body).toHaveLength(4)
  expect(body[3]).toEqual({
    // Monotonic allocator: 3 was consumed by the first add+remove, this
    // re-add gets 4 — any value ≥ oldCount(3) reads as an addition, and
    // never reusing an original identity is the point.
    phaseSeqNo: 4,
    name: 'NewWork(Limit:1)',
    labelColor: 'CornflowerBlue',
    wipTitle: 'In Progress',
    doneTitle: 'Done',
    isStartOfCycleTime: false,
    isEndOfCycleTime: false,
  })
})

// ------------------------------------------------------------ pinned: 409

it('409 from the server renders the localized conflict text and refetches the steps', async () => {
  // Mutable "server": the guard trips while guardTrips, and the GET serves
  // serverSteps — the truth the editor must reload into its rows.
  let guardTrips = true
  const serverSteps = [step(0, 'Server-side(Limit:1)')]
  installFetch({
    'GET /api/boards': () => [{ ...boardWith([]), processSteps: serverSteps }],
    'PUT /api/boards/b-1/process-steps': () =>
      guardTrips
        ? new Response(JSON.stringify({ guard: 'MinSteps' }), {
            status: 409,
            headers: { 'content-type': 'application/json' },
          })
        : boardWith(serverSteps),
  })
  renderEditor(boardWith([backlog, dev, test]))
  await screen.findByTestId('step-name-0')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('step-save-button'))

  // Localized conflict text (ProcessStep_Conflict under en), not a crash:
  // the dialog stays mounted and usable.
  expect(await screen.findByTestId('process-step-error')).toHaveTextContent(
    'process steps were changed by another user',
  )
  // Steps refetched and the dirty local edits replaced by server truth.
  await waitFor(() =>
    expect(screen.getByTestId('step-name-0')).toHaveValue('Server-side(Limit:1)'),
  )
  expect(screen.queryByTestId('step-name-1')).not.toBeInTheDocument()
  const putIndex = calls.findIndex((c) => c.method === 'PUT')
  expect(putIndex).toBeGreaterThanOrEqual(0)
  expect(calls.slice(putIndex).some((c) => c.method === 'GET')).toBe(true)

  // Server releases the guard → saving again succeeds.
  guardTrips = false
  await user.click(screen.getByTestId('step-save-button'))
  await waitFor(() => expect(putCalls()).toHaveLength(2))
  expect(screen.queryByTestId('process-step-error')).not.toBeInTheDocument()
})

// ---------------------------------------------- save pending + invalidation

it('Save is disabled while the PUT is in flight; success invalidates both keys and closes', async () => {
  let resolvePut: ((r: Response | object) => void) | null = null
  installFetch({
    'PUT /api/boards/b-1/process-steps': () =>
      new Promise<object>((resolve) => {
        resolvePut = resolve
      }),
  })
  const onClose = vi.fn()
  const { invalidate } = renderEditor(boardWith([backlog, dev, test]), onClose)
  await screen.findByTestId('step-name-0')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('step-save-button'))
  await waitFor(() => expect(putCalls()).toHaveLength(1))
  expect(screen.getByTestId('step-save-button')).toBeDisabled()
  expect(screen.getByTestId('step-close-button')).toBeDisabled()

  resolvePut!(boardWith([backlog, dev, test]))
  await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1))
  const keys = invalidate.mock.calls.map(
    (c: unknown[]) => (c[0] as { queryKey: unknown }).queryKey,
  )
  expect(keys).toContainEqual(['board'])
  expect(keys).toContainEqual(['boards'])
})

it('editor renders inside the dialog with the localized title and close closes', async () => {
  installFetch({})
  const onClose = vi.fn()
  renderEditor(boardWith([backlog, dev, test]), onClose)
  const dialog = await screen.findByTestId('process-step-editor')
  expect(within(dialog).getByText('Process of this project')).toBeInTheDocument()

  const user = userEvent.setup()
  await user.click(screen.getByTestId('step-close-button'))
  expect(onClose).toHaveBeenCalledTimes(1)
})
