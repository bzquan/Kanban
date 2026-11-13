import createClient from 'openapi-fetch'

import type { paths } from '../../../openapi/schema'
import type {
  BoardDto,
  BugsBlockedDto,
  CardCreateRequest,
  CardDto,
  CardPatch,
  CfdDto,
  CycleTimeDto,
  MoveCardRequest,
  ThroughputDto,
  VelocityDto,
} from './types'

/** Failure from the Kanban API (any non-2xx response). */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number) {
    super(`API request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
  }
}

// The generated paths already carry the '/api' prefix (e.g. '/api/boards'),
// so baseUrl must stay empty: '/api' here would double it to '/api/api/boards'.
// Dev-time requests reach the backend through the Vite proxy (vite.config.ts).
// fetch is late-bound so the test stub (src/test/fetchStub.ts), which is
// installed after this module loads, is picked up: openapi-fetch otherwise
// captures globalThis.fetch when createClient() runs.
export const api = createClient<paths>({
  baseUrl: '',
  fetch: (request: Request) => globalThis.fetch(request),
})

/** Cards list filter, sent verbatim as query params on GET /api/boards/{boardId}/cards. */
export interface CardsQuery {
  processStepSeqNo?: number
  isWip?: boolean
  filter?: string
  onBoard?: 'front' | 'back' | 'all'
}

/** Coerce the int64 `number | string` unions the API returns to a number; NaN/undefined → 0. */
export function intVal(v: number | string | undefined): number {
  const n = Number(v)
  return Number.isFinite(n) ? n : 0
}

function unwrap<T>(data: T | undefined, response: Response): T {
  if (!response.ok || data === undefined) {
    throw new ApiError(response.status)
  }
  return data
}

function ensureOk(response: Response): void {
  if (!response.ok) {
    throw new ApiError(response.status)
  }
}

export const boardsApi = {
  async list(): Promise<BoardDto[]> {
    const { data, response } = await api.GET('/api/boards')
    return unwrap(data, response)
  },

  async create(uiLanguage: string): Promise<BoardDto> {
    const { data, response } = await api.POST('/api/boards', {
      body: { uiLanguage },
    })
    return unwrap(data, response)
  },

  async remove(id: string): Promise<void> {
    const { response } = await api.DELETE('/api/boards/{id}', {
      params: { path: { id } },
    })
    ensureOk(response)
  },
}

export const cardsApi = {
  async list(boardId: string, q: CardsQuery): Promise<CardDto[]> {
    const { data, response } = await api.GET('/api/boards/{boardId}/cards', {
      params: {
        path: { boardId },
        query: {
          processStepSeqNo: q.processStepSeqNo,
          isWip: q.isWip,
          filter: q.filter,
          onBoard: q.onBoard,
        },
      },
    })
    return unwrap(data, response)
  },

  async create(req: CardCreateRequest): Promise<CardDto> {
    const { data, response } = await api.POST('/api/cards', { body: req })
    return unwrap(data, response)
  },

  async patch(id: string, p: CardPatch): Promise<CardDto> {
    const { data, response } = await api.PATCH('/api/cards/{id}', {
      params: { path: { id } },
      body: p,
    })
    return unwrap(data, response)
  },

  async move(id: string, req: MoveCardRequest): Promise<void> {
    const { response } = await api.PATCH('/api/cards/{id}/move', {
      params: { path: { id } },
      body: req,
    })
    ensureOk(response)
  },

  async remove(id: string): Promise<void> {
    const { response } = await api.DELETE('/api/cards/{id}', {
      params: { path: { id } },
    })
    ensureOk(response)
  },
}

export const metricsApi = {
  async cfd(boardId: string): Promise<CfdDto> {
    const { data, response } = await api.GET(
      '/api/boards/{boardId}/metrics/cfd',
      { params: { path: { boardId } } },
    )
    return unwrap(data, response)
  },

  async cycleTime(boardId: string): Promise<CycleTimeDto> {
    const { data, response } = await api.GET(
      '/api/boards/{boardId}/metrics/cycle-time',
      { params: { path: { boardId } } },
    )
    return unwrap(data, response)
  },

  async throughput(boardId: string): Promise<ThroughputDto> {
    const { data, response } = await api.GET(
      '/api/boards/{boardId}/metrics/throughput',
      { params: { path: { boardId } } },
    )
    return unwrap(data, response)
  },

  async bugsBlocked(boardId: string): Promise<BugsBlockedDto> {
    const { data, response } = await api.GET(
      '/api/boards/{boardId}/metrics/bugs-blocked',
      { params: { path: { boardId } } },
    )
    return unwrap(data, response)
  },

  async velocity(boardId: string): Promise<VelocityDto> {
    const { data, response } = await api.GET(
      '/api/boards/{boardId}/metrics/velocity',
      { params: { path: { boardId } } },
    )
    return unwrap(data, response)
  },
}
