import { useQuery } from '@tanstack/react-query'

import { ApiError, boardsApi, cardsApi, type CardsQuery } from '../api/client'
import type { BoardDto } from '../api/types'

/** Which side of the board is shown (WPF BoardPage's front/back switch). */
export type BoardSide = 'front' | 'back'

/**
 * Board metadata. There is no GET /api/boards/{id} endpoint: fetch the list
 * and pick by id; a miss (e.g. malformed ObjectId) surfaces as a 404 so the
 * page can render its recoverable error state.
 */
export function useBoard(boardId: string) {
  return useQuery({
    queryKey: ['board', boardId],
    queryFn: async (): Promise<BoardDto> => {
      const boards = await boardsApi.list()
      const board = boards.find((b) => b.id === boardId)
      if (!board) throw new ApiError(404)
      return board
    },
  })
}

/**
 * Cards of one sub-column. The server filters strictly per
 * (processStepSeqNo, isWip) — one query per sub-column — and returns them
 * seqNo-ordered. An empty filter text is omitted (never sent as filter='').
 */
export function useCards(
  boardId: string,
  step: number,
  isWip: boolean,
  onBoard: BoardSide,
  filter: string,
) {
  const query: CardsQuery = { processStepSeqNo: step, isWip, onBoard }
  if (filter !== '') query.filter = filter
  return useQuery({
    queryKey: ['cards', boardId, step, isWip, onBoard, filter],
    queryFn: () => cardsApi.list(boardId, query),
  })
}
