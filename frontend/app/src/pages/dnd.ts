import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import type { DragEndEvent } from '@dnd-kit/core'

import { cardsApi, intVal } from '../api/client'
import type { CardDto, MoveCardRequest } from '../api/types'
import type { BoardSide } from './boardQueries'

// Client side of PATCH /api/cards/{id}/move. The SeqNo renumbering and the
// Activity trail are enforced server-side (CardCommandService.MoveCardAsync
// is the single implementation); this module only turns drop geometry into
// the MoveCardRequest and keeps the UI honest until the server answers.

export type DragEndHandler = (event: DragEndEvent) => void

/** Where the dragged card came from. */
export interface DragLoc {
  step: number
  isWip: boolean
  card: CardDto
}

/** Where it was dropped. `cards` is the destination sub-column's card list
 *  (same-column: still including the dragged card), as displayed. */
export interface DropLoc {
  step: number
  isWip: boolean
  overCard: CardDto | null
  cards: CardDto[]
}

/** Exact query-key contract of useCards (Task 5) — keep in sync. */
export function cardsQueryKey(
  boardId: string,
  step: number,
  isWip: boolean,
  onBoard: BoardSide,
  filter: string,
): ['cards', string, number, boolean, string, string] {
  return ['cards', boardId, step, isWip, onBoard, filter]
}

/** Display order (ascending seqNo), matching SubColumnView. */
function bySeqNo(cards: CardDto[] | undefined): CardDto[] {
  return [...(cards ?? [])].sort((x, y) => intVal(x.seqNo) - intVal(y.seqNo))
}

/**
 * MoveCardRequest from drop geometry. index = insertion position among the
 * destination sub-column's cards: null overCard appends (list length), a
 * card drop takes overCard's position. The server clamps index to
 * [0, count] and, for same-column moves, inserts after removing the dragged
 * card — which makes this single rule match MoveCardAsync exactly for
 * before/after/append/empty drops (see task-7-report for the walkthrough).
 */
export function moveRequestFor(drag: DragLoc, to: DropLoc): MoveCardRequest {
  void drag // part of the interface; the target is destination-geometry only
  const cards = bySeqNo(to.cards)
  const over = to.overCard
  let index = cards.length
  if (over !== null) {
    const found = cards.findIndex((c) => c.id !== undefined && c.id === over.id)
    // Unknown overCard (e.g. a cache update raced the drag): append rather
    // than silently prepend — dropping at the end is never worse than
    // asserting a position we cannot see.
    index = found < 0 ? cards.length : found
  }
  return { processStepSeqNo: to.step, isWip: to.isWip, index }
}

/** dnd-kit data payloads attached to sortable cards / droppable sub-columns. */
export type DragItemData =
  | { kind: 'card'; step: number; isWip: boolean; cardId: string }
  | { kind: 'container'; step: number; isWip: boolean }

export interface MoveVars {
  cardId: string
  req: MoveCardRequest
  source: { step: number; isWip: boolean }
  dest: { step: number; isWip: boolean }
}

/** Renumber 0..n-1 (server RenumberAsync base) on fresh objects. */
function renumber(cards: CardDto[]): CardDto[] {
  return cards.map((cd, i) => ({ ...cd, seqNo: i }))
}

function clampIndex(index: number, length: number): number {
  return Math.min(Math.max(index, 0), length)
}

type CardsCache = CardDto[] | undefined

/** Optimistic cache edit mirroring MoveCardAsync (remove→insert→renumber). */
function applyMove(
  queryClient: ReturnType<typeof useQueryClient>,
  vars: MoveVars,
  srcKey: ReturnType<typeof cardsQueryKey>,
  dstKey: ReturnType<typeof cardsQueryKey>,
): void {
  const index = intVal(vars.req.index)
  const sameColumn =
    vars.source.step === vars.dest.step && vars.source.isWip === vars.dest.isWip

  if (sameColumn) {
    // Server: RemoveAt(from) then Insert(clamp(index)) into the SHORTER list.
    const list = bySeqNo(queryClient.getQueryData<CardsCache>(srcKey))
    const from = list.findIndex((c) => c.id === vars.cardId)
    if (from < 0) return
    const [moved] = list.splice(from, 1)
    list.splice(clampIndex(index, list.length), 0, moved)
    queryClient.setQueryData(srcKey, renumber(list))
    return
  }

  const source = bySeqNo(queryClient.getQueryData<CardsCache>(srcKey))
  const kept = source.filter((c) => c.id !== vars.cardId)
  queryClient.setQueryData(srcKey, renumber(kept))

  const dest = bySeqNo(queryClient.getQueryData<CardsCache>(dstKey))
  const moved = source.find((c) => c.id === vars.cardId)
  if (!moved) return
  dest.splice(clampIndex(index, dest.length), 0, moved)
  queryClient.setQueryData(dstKey, renumber(dest))
}

/**
 * Optimistic move mutation (Review Focus #1): snapshot → cancel → edit,
 * restore snapshots on error, and always invalidate ['cards'] once settled
 * so the screen re-reads the server's truth (never keeps a ghost card).
 */
export function useCardMove(boardId: string, onBoard: BoardSide, filter: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (vars: MoveVars) => cardsApi.move(vars.cardId, vars.req),
    onMutate: async (vars) => {
      const srcKey = cardsQueryKey(boardId, vars.source.step, vars.source.isWip, onBoard, filter)
      const dstKey = cardsQueryKey(boardId, vars.dest.step, vars.dest.isWip, onBoard, filter)
      await Promise.all([
        queryClient.cancelQueries({ queryKey: srcKey }),
        queryClient.cancelQueries({ queryKey: dstKey }),
      ])
      const snapshots: [CardsCache, CardsCache] = [
        queryClient.getQueryData(srcKey),
        queryClient.getQueryData(dstKey),
      ]
      applyMove(queryClient, vars, srcKey, dstKey)
      return { srcKey, dstKey, snapshots }
    },
    onError: (_error, _vars, ctx) => {
      if (!ctx) return
      queryClient.setQueryData(ctx.srcKey, ctx.snapshots[0])
      queryClient.setQueryData(ctx.dstKey, ctx.snapshots[1])
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['cards'] })
    },
  })
}

/**
 * The DndContext onDragEnd handler: resolves active/over dnd-kit payloads
 * against the per-sub-column caches and fires the optimistic move mutation.
 * Drops outside any sub-column and self-drops are no-ops.
 */
export function useCardDragEnd(
  boardId: string,
  onBoard: BoardSide,
  filter: string,
): DragEndHandler {
  const queryClient = useQueryClient()
  const { mutate } = useCardMove(boardId, onBoard, filter)
  // Stable identity (mutate is stable per TanStack v5) so BoardPage can
  // memoize its DndContext handlers without exhaustive-deps churn.
  return useCallback(
    (event: DragEndEvent) => {
    const { active, over } = event
    const activeData = active.data.current as DragItemData | undefined
    const overData = over?.data.current as DragItemData | undefined
    if (!activeData || activeData.kind !== 'card' || !overData) return

    const source = { step: activeData.step, isWip: activeData.isWip }
    const dest = { step: overData.step, isWip: overData.isWip }
    const srcCards = bySeqNo(
      queryClient.getQueryData<CardsCache>(
        cardsQueryKey(boardId, source.step, source.isWip, onBoard, filter),
      ),
    )
    const card = srcCards.find((c) => c.id === activeData.cardId)
    if (!card || card.id == null) return

    const dstCards = bySeqNo(
      queryClient.getQueryData<CardsCache>(
        cardsQueryKey(boardId, dest.step, dest.isWip, onBoard, filter),
      ),
    )
    const overCard =
      overData.kind === 'card'
        ? dstCards.find((c) => c.id === overData.cardId) ?? null
        : null
    if (overData.kind === 'card' && overData.cardId === card.id) return

      mutate({
        cardId: card.id,
        req: moveRequestFor(
          { step: source.step, isWip: source.isWip, card },
          { step: dest.step, isWip: dest.isWip, overCard, cards: dstCards },
        ),
        source,
        dest,
      })
    },
    [queryClient, mutate, boardId, onBoard, filter],
  )
}
