import AddIcon from '@mui/icons-material/Add'
import FitScreenIcon from '@mui/icons-material/FitScreen'
import RefreshIcon from '@mui/icons-material/Refresh'
import ZoomInIcon from '@mui/icons-material/ZoomIn'
import ZoomOutIcon from '@mui/icons-material/ZoomOut'
import {
  Box,
  Button,
  CircularProgress,
  IconButton,
  TextField,
  Toolbar,
  Typography,
} from '@mui/material'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { CSS } from '@dnd-kit/utilities'
import {
  SortableContext,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'

import { ApiError, intVal } from '../api/client'
import type { CardDto, ProcessStepDto } from '../api/types'
import CardView from '../components/CardView'
import { format0 } from '../util/format'
import { useBoard, useCards, type BoardSide } from './boardQueries'
import { useCardDragEnd, type DragEndHandler } from './dnd'

// User settings live in localStorage (spec §4.6), mirroring the WPF client.
const ZOOM_KEY = 'kanban.zoom'
const ZOOMED_KEY = 'kanban.isZoomed'
const LAST_BOARD_KEY = 'kanban.lastBoardId'

// WPF zoom-to-fit parity-lite: manual steps of 0.1 within 0.5–1.5.
const ZOOM_MIN = 0.5
const ZOOM_MAX = 1.5
const ZOOM_STEP = 0.1
const FILTER_DEBOUNCE_MS = 300

function readStored(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null // storage unavailable (private mode)
  }
}

function persist(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    // best-effort; the in-memory state still applies
  }
}

function initialZoom(): number {
  const stored = Number.parseFloat(readStored(ZOOM_KEY) ?? '')
  return Number.isFinite(stored) && stored >= ZOOM_MIN && stored <= ZOOM_MAX
    ? stored
    : 1
}

/** Snap to tenths (0.9 - 0.1 → exactly 0.8, no float drift) and clamp. */
function stepZoom(z: number, delta: number): number {
  const snapped = Math.round((z + delta) * 10) / 10
  return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, snapped))
}

interface SubColumnViewProps {
  boardId: string
  step: ProcessStepDto
  side: 'wip' | 'done'
  onBoard: BoardSide
  filter: string
}

// One WPF sub-column: header = the step's wipTitle/doneTitle, cards fetched
// per (step, isWip, onBoard, filter). Fetch failures leave this sub-column
// empty; the toolbar and remaining chrome keep working.
// Shared between SortableContext items and useSortable so ids always match.
function sortableId(card: CardDto, seqNo: number, side: 'wip' | 'done'): string {
  return card.id ?? `${seqNo}-${side}-${intVal(card.seqNo)}`
}

// One sortable card inside a sub-column's SortableContext. The dnd-kit
// payload (kind/step/isWip/cardId) is what turns an over-id back into
// (processStepSeqNo, isWip, index) in useCardDragEnd.
function SortableCard({ card, id, step, isWip }: { card: CardDto; id: string; step: number; isWip: boolean }) {
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({
    id,
    data: { kind: 'card', step, isWip, cardId: card.id },
  })
  return (
    <Box
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      sx={{
        transform: CSS.Translate.toString(transform),
        transition,
        touchAction: 'none',
      }}
    >
      <CardView
        card={card}
        onOpen={() => {
          /* Task 8: open the card-edit dialog from here. */
        }}
      />
    </Box>
  )
}

function SubColumnView({ boardId, step, side, onBoard, filter }: SubColumnViewProps) {
  const { t } = useTranslation()
  const seqNo = intVal(step.phaseSeqNo)
  const isWip = side === 'wip'
  const cardsQuery = useCards(boardId, seqNo, isWip, onBoard, filter)
  // The server already sorts; stable ascending seqNo keeps that order pinned.
  const cards = [...(cardsQuery.data ?? [])].sort(
    (a, b) => intVal(a.seqNo) - intVal(b.seqNo),
  )
  // The whole sub-column is a drop target, so an *empty* sub-column still
  // accepts drops (Review Focus #3 → over null → append → index 0).
  const { setNodeRef: setDropRef } = useDroppable({
    id: `subcolumn-${seqNo}-${side}`,
    data: { kind: 'container', step: seqNo, isWip },
  })
  return (
    <Box
      ref={setDropRef}
      data-testid={`subcolumn-${seqNo}-${side}`}
      sx={{ flex: 1, minWidth: 200, p: 0.5 }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="subtitle2">
          {isWip ? step.wipTitle ?? '' : step.doneTitle ?? ''}
        </Typography>
        {/* Handler lands in Task 8; the testid is reserved already. */}
        <IconButton
          size="small"
          data-testid={`add-card-${seqNo}-${side}`}
          aria-label={t('ToolTip_AddCard')}
        >
          <AddIcon fontSize="small" />
        </IconButton>
      </Box>
      <SortableContext
        items={cards.map((card) => sortableId(card, seqNo, side))}
        strategy={verticalListSortingStrategy}
      >
        {cards.map((card) => (
          <SortableCard
            key={sortableId(card, seqNo, side)}
            id={sortableId(card, seqNo, side)}
            card={card}
            step={seqNo}
            isWip={isWip}
          />
        ))}
      </SortableContext>
    </Box>
  )
}

interface ColumnViewProps {
  boardId: string
  step: ProcessStepDto
  onBoard: BoardSide
  filter: string
}

// One WPF column: step.name verbatim header (it already embeds "(Limit:N)")
// over two sub-columns, exactly like BoardPage.xaml's processStepDataTemplate.
function ColumnView({ boardId, step, onBoard, filter }: ColumnViewProps) {
  const seqNo = intVal(step.phaseSeqNo)
  return (
    <Box
      data-testid={`column-${seqNo}`}
      sx={{ border: 1, borderColor: 'divider', borderRadius: 1, flexShrink: 0 }}
    >
      <Typography
        variant="subtitle1"
        align="center"
        sx={{ py: 0.5, bgcolor: step.labelColor ?? 'grey.200' }}
      >
        {step.name ?? ''}
      </Typography>
      <Box sx={{ display: 'flex' }}>
        <SubColumnView boardId={boardId} step={step} side="wip" onBoard={onBoard} filter={filter} />
        <SubColumnView boardId={boardId} step={step} side="done" onBoard={onBoard} filter={filter} />
      </Box>
    </Box>
  )
}

interface BoardPageProps {
  // Test seam (brief Step 6, "export the drop handler" variant): hands the
  // *real* DndContext onDragEnd handler to the test so the optimistic-move
  // and rollback paths can be driven without simulating pointer drags in
  // jsdom. Production never passes it.
  onDragEndHandleRef?: { current: DragEndHandler | null }
}

export default function BoardPage({ onDragEndHandleRef }: BoardPageProps = {}) {
  const { boardId = '' } = useParams<{ boardId: string }>()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const boardQuery = useBoard(boardId)
  const steps = boardQuery.data?.processSteps ?? []

  const [onBoard, setOnBoard] = useState<BoardSide>('front')
  const [filterInput, setFilterInput] = useState('')
  const [filter, setFilter] = useState('')
  const [zoom, setZoom] = useState(initialZoom)
  const [isZoomed, setIsZoomed] = useState(() => readStored(ZOOMED_KEY) === 'true')

  const containerRef = useRef<HTMLDivElement>(null)
  const canvasRef = useRef<HTMLDivElement>(null)

  // Drag & drop (Task 7): 5px activation keeps card clicks (Task 8 dialog)
  // from starting drags; auto-scroll uses dnd-kit's built-in AutoScroller
  // over the overflow:auto board container.
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
  )
  const onDragEnd = useCardDragEnd(boardId, onBoard, filter)
  const [activeCardId, setActiveCardId] = useState<string | null>(null)

  useEffect(() => {
    if (onDragEndHandleRef) onDragEndHandleRef.current = onDragEnd
  }, [onDragEndHandleRef, onDragEnd])

  // DragOverlay renders the live clone (dnd-kit default drop indicator: the
  // sortable list displaces items around the insertion point).
  const activeCard =
    activeCardId === null
      ? null
      : (queryClient
          .getQueriesData<CardDto[]>({ queryKey: ['cards'] })
          .flatMap(([, data]) => data ?? [])
          .find((c) => c.id === activeCardId) ?? null)

  useEffect(() => {
    persist(LAST_BOARD_KEY, boardId)
  }, [boardId])

  // Toolbar text debounces into the query's `filter` param.
  useEffect(() => {
    const id = window.setTimeout(() => setFilter(filterInput.trim()), FILTER_DEBOUNCE_MS)
    return () => window.clearTimeout(id)
  }, [filterInput])

  const applyZoom = (next: number): void => {
    setZoom(next)
    persist(ZOOM_KEY, String(next))
  }

  const refresh = (): void => {
    void queryClient.invalidateQueries({ queryKey: ['cards'] })
  }

  const resetZoom = (): void => {
    setIsZoomed(false)
    persist(ZOOMED_KEY, 'false')
    applyZoom(1)
  }

  // Fit-to-window (WPF IsZoomed): scale so the canvas width fills the
  // viewport instead of scrolling.
  const toggleFitToWindow = (): void => {
    const next = !isZoomed
    setIsZoomed(next)
    persist(ZOOMED_KEY, String(next))
    if (next && containerRef.current && canvasRef.current) {
      const container = containerRef.current.clientWidth
      const content = canvasRef.current.scrollWidth
      if (container > 0 && content > 0) applyZoom(stepZoom(container / content, 0))
    }
  }

  if (boardQuery.isError) {
    // Recoverable error state (Review Focus #2): malformed ObjectId → 404
    // from useBoard, API 400/404 straight from the client — app keeps working.
    const status = boardQuery.error instanceof ApiError ? boardQuery.error.status : undefined
    return (
      <Box data-testid="board-page" sx={{ p: 2 }}>
        <Box data-testid="board-error" sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Typography color="error">{t('BoardPage_LoadError')}</Typography>
          {/* .NET-style {0} substitution goes through the shared format0 util,
              so the label itself is localized (BoardPage_BoardWithId). */}
          <Typography color="text.secondary">
            {format0(t('BoardPage_BoardWithId'), boardId)}
          </Typography>
          {status !== undefined && (
            <Typography color="text.secondary" variant="body2">
              {`(${status})`}
            </Typography>
          )}
          <Box>
            <Button data-testid="back-to-list-button" onClick={() => navigate('/')}>
              {t('BoardPage_BackToList')}
            </Button>
          </Box>
        </Box>
      </Box>
    )
  }

  if (boardQuery.isLoading) {
    return (
      <Box data-testid="board-page" sx={{ p: 2 }}>
        <CircularProgress size={24} />
      </Box>
    )
  }

  return (
    <Box data-testid="board-page" sx={{ p: 2 }}>
      <Toolbar
        component="div"
        sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1, px: 0, minHeight: 0, mb: 1 }}
      >
        <Typography variant="h6" data-testid="board-title" sx={{ mr: 1 }}>
          {boardQuery.data?.title ?? ''}
        </Typography>
        <TextField
          size="small"
          label={t('BoardPage_Filter')}
          value={filterInput}
          onChange={(e) => setFilterInput(e.target.value)}
          slotProps={{ htmlInput: { 'data-testid': 'filter-input' } }}
        />
        <Button
          size="small"
          data-testid="refresh-button"
          startIcon={<RefreshIcon />}
          onClick={refresh}
        >
          {t('ToolTip_Refresh')}
        </Button>
        <IconButton
          size="small"
          data-testid="zoom-out-button"
          aria-label={t('ToolTip_Zoom')}
          onClick={() => applyZoom(stepZoom(zoom, -ZOOM_STEP))}
        >
          <ZoomOutIcon fontSize="small" />
        </IconButton>
        <IconButton
          size="small"
          data-testid="zoom-in-button"
          aria-label={t('ToolTip_Zoom')}
          onClick={() => applyZoom(stepZoom(zoom, ZOOM_STEP))}
        >
          <ZoomInIcon fontSize="small" />
        </IconButton>
        <Button size="small" data-testid="zoom-reset-button" onClick={resetZoom}>
          100%
        </Button>
        <Button
          size="small"
          data-testid="zoom-fit-button"
          color={isZoomed ? 'primary' : 'inherit'}
          startIcon={<FitScreenIcon />}
          onClick={toggleFitToWindow}
        >
          {t('BoardPage_FitToWindow')}
        </Button>
        <Button
          size="small"
          data-testid="board-side-toggle-button"
          aria-label={t('ToolTip_SwitchBoardFrontAndBack')}
          onClick={() => setOnBoard((s) => (s === 'front' ? 'back' : 'front'))}
        >
          {onBoard === 'front' ? t('ToolTip_BoardFront') : t('ToolTip_BoardBack')}
        </Button>
      </Toolbar>

      <DndContext
        sensors={sensors}
        onDragStart={(e: DragStartEvent) => setActiveCardId(String(e.active.id))}
        onDragEnd={(e: DragEndEvent) => {
          setActiveCardId(null)
          onDragEnd(e)
        }}
      >
      <Box
        ref={containerRef}
        data-testid="board-canvas-container"
        sx={{ overflowX: isZoomed ? 'hidden' : 'auto' }}
      >
        <Box
          ref={canvasRef}
          data-testid="board-canvas"
          sx={{ display: 'flex', gap: 1, width: 'max-content', minHeight: 200 }}
          style={{ transform: `scale(${zoom})`, transformOrigin: '0 0' }}
        >
          {steps.map((step) => (
            <ColumnView
              key={intVal(step.phaseSeqNo)}
              boardId={boardId}
              step={step}
              onBoard={onBoard}
              filter={filter}
            />
          ))}
        </Box>
      </Box>
        <DragOverlay>
          {activeCard ? (
            <Box sx={{ width: 220 }}>
              <CardView card={activeCard} />
            </Box>
          ) : null}
        </DragOverlay>
      </DndContext>
    </Box>
  )
}
