import i18n from 'i18next'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useParams } from 'react-router'
import { afterEach, beforeEach, expect, it } from 'vitest'

import type { BoardDto } from '../api/types'
import { initI18n } from '../i18n'
import { calls, installFetch, restoreFetch } from '../test/fetchStub'
import { renderWithProviders } from '../test/renderWithProviders'

import BoardListPage from './BoardListPage'

const alpha: BoardDto = {
  id: 'b-1',
  seqNo: 1,
  title: 'Alpha',
  description: 'First board',
  developers: 'Ann;Bob',
  processSteps: [
    { phaseSeqNo: 0, name: 'Backlog(Limit:∞)', wipTitle: 'In Progress', doneTitle: 'Done' },
    { phaseSeqNo: 1, name: 'Development(Limit:5)', wipTitle: 'In Progress', doneTitle: 'Done', isStartOfCycleTime: true },
    { phaseSeqNo: 2, name: 'Test(Limit:3)', wipTitle: 'In Progress', doneTitle: 'Done', isEndOfCycleTime: true },
  ],
}
const beta: BoardDto = {
  id: 'b-2',
  seqNo: 2,
  title: 'Beta',
  description: 'Second board',
  developers: 'Cid',
  processSteps: [],
}

// Stand-in for BoardPage (Task 5) so navigation can be observed with the id.
function BoardPageStub() {
  const { boardId } = useParams()
  return (
    <div data-testid="board-page">
      {boardId}
    </div>
  )
}

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/" element={<BoardListPage />} />
      <Route path="/boards/:boardId" element={<BoardPageStub />} />
    </Routes>,
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

it('lists boards from GET /api/boards with title, description, developers and #steps', async () => {
  installFetch({ 'GET /api/boards': () => [alpha, beta] })
  renderPage()

  // en values from the resx bundle: Board_NameFormat "Name：{0} ",
  // Board_DetailFormat "Detail：{0}", BoardList_StepsFormat "Steps：{0}".
  expect(await screen.findByText('Name：Alpha')).toBeInTheDocument()
  expect(screen.getByText('Detail：First board')).toBeInTheDocument()
  expect(screen.getByText(/Ann;Bob/)).toBeInTheDocument()
  expect(screen.getByText('Steps：3')).toBeInTheDocument()

  expect(screen.getByText('Name：Beta')).toBeInTheDocument()
  expect(screen.getByText('Steps：0')).toBeInTheDocument()

  // The list came from one recorded GET, at the API path.
  const gets = calls.filter((c) => c.method === 'GET')
  expect(gets).toEqual([{ method: 'GET', url: '/api/boards', body: undefined }])
})

it('clicking a row navigates to /boards/{id} and stores kanban.lastBoardId', async () => {
  installFetch({ 'GET /api/boards': () => [alpha, beta] })
  renderPage()

  const user = userEvent.setup()
  await user.click(await screen.findByText('Name：Alpha'))

  expect(await screen.findByTestId('board-page')).toHaveTextContent('b-1')
  expect(localStorage.getItem('kanban.lastBoardId')).toBe('b-1')
})

it('create button POSTs {uiLanguage:"ja"} under ja and navigates to the new board', async () => {
  void i18n.changeLanguage('ja')
  installFetch({
    'GET /api/boards': () => [],
    'POST /api/boards': () => ({ ...beta, id: 'b-new', title: '新規プロジェクト' }),
  })
  renderPage()

  const user = userEvent.setup()
  // ToolTip_AddBoard under ja.
  await user.click(await screen.findByRole('button', { name: 'ボードの追加' }))

  expect(await screen.findByTestId('board-page')).toHaveTextContent('b-new')
  const post = calls.find((c) => c.method === 'POST')
  expect(post).toBeDefined()
  expect(post?.url).toBe('/api/boards')
  expect(post?.body).toEqual({ uiLanguage: 'ja' })

  // createMutation invalidates ['boards']: a refetch GET is recorded after the POST.
  await waitFor(() => expect(calls.filter((c) => c.method === 'GET')).toHaveLength(2))
  expect(calls[1].method).toBe('POST')
  expect(calls[2]).toEqual({ method: 'GET', url: '/api/boards', body: undefined })
})

it('delete flow confirms, records DELETE /api/boards/{id} then refetches the list', async () => {
  let boards: BoardDto[] = [alpha, beta]
  installFetch({
    'GET /api/boards': () => boards,
    'DELETE /api/boards/b-1': () => {
      boards = [beta]
      return {}
    },
  })
  renderPage()

  const user = userEvent.setup()
  const row = await screen.findByTestId('board-row-b-1')
  await user.click(screen.getByTestId('board-row-delete-b-1'))

  // MUI confirm dialog: Message_DeleteComfirmTitle + Message_DeleteComfirmQuesion with {0}.
  expect(await screen.findByText('Comfirm to delete')).toBeInTheDocument()
  expect(screen.getByText('Do you really delete <Alpha>?')).toBeInTheDocument()

  await user.click(screen.getByTestId('confirm-delete-button'))

  await waitFor(() =>
    expect(
      calls.some((c) => c.method === 'DELETE' && c.url === '/api/boards/b-1'),
    ).toBe(true),
  )
  // The list query is invalidated: a second GET refetched the (now shorter) list.
  await waitFor(() => expect(row).not.toBeInTheDocument())
  expect(calls.filter((c) => c.method === 'GET')).toHaveLength(2)
  expect(screen.getByTestId('board-row-b-2')).toBeInTheDocument()
})

it('shows the localized empty state when the API returns no boards', async () => {
  installFetch({ 'GET /api/boards': () => [] })
  renderPage()

  const empty = await screen.findByTestId('board-list-empty')
  expect(empty).toHaveTextContent('No boards yet') // BoardList_Empty under en
})

it('row menu opens the process-step editor seeded with that board\'s steps', async () => {
  installFetch({
    'GET /api/boards': () => [alpha, beta],
    'PUT /api/boards/b-1/process-steps': () => alpha,
  })
  renderPage()

  const user = userEvent.setup()
  // board.editSteps under en.
  await user.click(await screen.findByTestId('board-row-steps-b-1'))

  expect(await screen.findByTestId('process-step-editor')).toBeInTheDocument()
  expect(screen.getByTestId('step-name-0')).toHaveValue('Backlog(Limit:∞)')
  expect(screen.getByTestId('step-name-1')).toHaveValue('Development(Limit:5)')

  await user.click(screen.getByTestId('step-save-button'))
  await waitFor(() =>
    expect(
      calls.some(
        (c) => c.method === 'PUT' && c.url === '/api/boards/b-1/process-steps',
      ),
    ).toBe(true),
  )
})

it('row menu entry is localized under ja (プロセスステップ設定)', async () => {
  void i18n.changeLanguage('ja')
  installFetch({ 'GET /api/boards': () => [alpha] })
  renderPage()

  const user = userEvent.setup()
  // IconButton exposes its aria-label as the accessible name.
  await user.click(await screen.findByRole('button', { name: 'プロセスステップ設定' }))
  expect(await screen.findByTestId('process-step-editor')).toBeInTheDocument()
})
