import AddIcon from '@mui/icons-material/Add'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
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
import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'

import { ApiError, cardsApi, intVal } from '../api/client'
import type { CardDto, ProcessStepDto } from '../api/types'
import CardEditDialog from '../components/CardEditDialog'
import CardView from '../components/CardView'
import { format0 } from '../util/format'
import { useBoard, useCards, type BoardSide } from './boardQueries'
import { useCardDragEnd, type DragEndHandler } from './dnd'
import MetricsPanel from './MetricsPanel'

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
  onAddCard: (step: number, side: 'wip' | 'done') => void
  onOpenCard: (card: CardDto) => void
  hoverDisabled: boolean
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
function SortableCard({
  card,
  id,
  step,
  isWip,
  onOpenCard,
  hoverDisabled,
}: {
  card: CardDto
  id: string
  step: number
  isWip: boolean
  onOpenCard: (card: CardDto) => void
  hoverDisabled: boolean
}) {
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
      <CardView card={card} onOpen={() => onOpenCard(card)} disableHoverInfo={hoverDisabled} />
    </Box>
  )
}

function SubColumnView({
  boardId,
  step,
  side,
  onBoard,
  filter,
  onAddCard,
  onOpenCard,
  hoverDisabled,
}: SubColumnViewProps) {
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
        <IconButton
          size="small"
          data-testid={`add-card-${seqNo}-${side}`}
          aria-label={t('ToolTip_AddCard')}
          onClick={() => onAddCard(seqNo, side)}
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
            onOpenCard={onOpenCard}
            hoverDisabled={hoverDisabled}
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
  onAddCard: (step: number, side: 'wip' | 'done') => void
  onOpenCard: (card: CardDto) => void
  hoverDisabled: boolean
}

// One WPF column: step.name verbatim header (it already embeds "(Limit:N)")
// over two sub-columns, exactly like BoardPage.xaml's processStepDataTemplate.
function ColumnView({ boardId, step, onBoard, filter, onAddCard, onOpenCard, hoverDisabled }: ColumnViewProps) {
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
        <SubColumnView
          boardId={boardId}
          step={step}
          side="wip"
          onBoard={onBoard}
          filter={filter}
          onAddCard={onAddCard}
          onOpenCard={onOpenCard}
          hoverDisabled={hoverDisabled}
        />
        <SubColumnView
          boardId={boardId}
          step={step}
          side="done"
          onBoard={onBoard}
          filter={filter}
          onAddCard={onAddCard}
          onOpenCard={onOpenCard}
          hoverDisabled={hoverDisabled}
        />
      </Box>
    </Box>
  )
}

/** The exact handlers BoardPage hands to DndContext (test seam below). */
export interface DragHandlers {
  onDragStart: (event: DragStartEvent) => void
  onDragEnd: DragEndHandler
  onDragCancel: () => void
}

interface BoardPageProps {
  // Test seam (brief Step 6, "export the drop handler" variant): hands the
  // *real* DndContext handlers to the test so the optimistic-move, rollback
  // and cancel paths can be driven exactly as DndContext would, without
  // simulating pointer drags in jsdom. Production never passes it.
  onDragHandlesRef?: { current: DragHandlers | null }
}

export default function BoardPage({ onDragHandlesRef }: BoardPageProps = {}) {
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
  // Task 8: the card currently open in the edit dialog (null = closed).
  const [editing, setEditing] = useState<CardDto | null>(null)
  // Final review Finding 1: a failed card create must tell the user the card
  // was NOT created server-side; null = nothing to show.
  const [addCardError, setAddCardError] = useState<string | null>(null)
  // Task 10: Kanban/Chart segmented toggle. Local-only state — leaving the
  // chart unmounts MetricsPanel (its queries are disposable), and the view
  // always starts on the kanban like the WPF board page.
  const [view, setView] = useState<'kanban' | 'chart'>('kanban')

  // Add-card flow: POST the minimal CardCreateRequest, then open the edit
  // dialog on the *returned* card so the user fills in the full record.
  const handleAddCard = useCallback(
    async (step: number, side: 'wip' | 'done'): Promise<void> => {
      try {
        const created = await cardsApi.create({
          boardId,
          processStepSeqNo: step,
          isWip: side === 'wip',
        })
        setAddCardError(null)
        void queryClient.invalidateQueries({ queryKey: ['cards'] })
        setEditing(created)
      } catch {
        // Board stays usable, but the failure is surfaced: the user must know
        // the card was not created server-side (Finding 1).
        setAddCardError(t('BoardPage_AddCardError'))
      }
    },
    [boardId, queryClient, t],
  )

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

  const handleDragStart = useCallback((event: DragStartEvent): void => {
    setActiveCardId(String(event.active.id))
  }, [])
  const handleDragEnd = useCallback(
    (event: DragEndEvent): void => {
      setActiveCardId(null)
      onDragEnd(event)
    },
    [onDragEnd],
  )
  // Regression guard: PointerSensor cancels the drag on Escape (or pointer
  // loss) and fires onDragCancel *without* onDragEnd — without this the
  // DragOverlay clone stays stuck on screen until the next drag starts.
  const handleDragCancel = useCallback((): void => {
    setActiveCardId(null)
  }, [])

  useEffect(() => {
    if (onDragHandlesRef) {
      onDragHandlesRef.current = {
        onDragStart: handleDragStart,
        onDragEnd: handleDragEnd,
        onDragCancel: handleDragCancel,
      }
    }
  }, [onDragHandlesRef, onDragEnd, handleDragStart, handleDragEnd, handleDragCancel])

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
  // viewport instead of scrolling. Fit is NOT the manual-zoom path: the
  // 0.1-step/0.5-floor would leave a wide board overflowing (and overflowX
  // hides the bar, so the overflow is unreachable), and a stale scrollLeft
  // hid the left side after scrolling right first (bug 11-17).
  const FIT_ZOOM_MIN = 0.1
  const toggleFitToWindow = (): void => {
    const next = !isZoomed
    setIsZoomed(next)
    persist(ZOOMED_KEY, String(next))
    if (next && containerRef.current && canvasRef.current) {
      const container = containerRef.current.clientWidth
      const content = canvasRef.current.scrollWidth
      if (container > 0 && content > 0) {
        // Round down so the scaled canvas never overflows the container.
        const ratio = Math.floor((container / content) * 100) / 100
        applyZoom(Math.min(ZOOM_MAX, Math.max(FIT_ZOOM_MIN, ratio)))
      }
      containerRef.current.scrollLeft = 0
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
    // flex-basis 0 + grow: the page height comes from the shell, never from
    // tall canvas content — so the canvas container (below) owns the scroll.
    <Box data-testid="board-page" sx={{ p: 2, display: 'flex', flexDirection: 'column', flexGrow: 1, flexBasis: 0, minHeight: 0 }}>
      <Toolbar
        component="div"
        sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1, px: 0, minHeight: 0, mb: 1, flexShrink: 0 }}
      >
        {/* Icon-only compact back button (agreed 11-17); aria-label carries
            the localized BoardPage_BackToList name. */}
        <IconButton
          size="small"
          data-testid="back-to-list-button"
          aria-label={t('BoardPage_BackToList')}
          onClick={() => navigate('/')}
        >
          <ArrowBackIcon fontSize="small" />
        </IconButton>
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
        {/* Task 10: Kanban/Chart segmented toggle (WPF has no chart view —
            this is the web client's metrics entry point). */}
        <Button
          size="small"
          data-testid="view-kanban-button"
          color={view === 'kanban' ? 'primary' : 'inherit'}
          onClick={() => setView('kanban')}
        >
          {t('BoardPage_ViewKanban')}
        </Button>
        <Button
          size="small"
          data-testid="view-chart-button"
          color={view === 'chart' ? 'primary' : 'inherit'}
          onClick={() => setView('chart')}
        >
          {t('BoardPage_ViewChart')}
        </Button>
      </Toolbar>

      {addCardError !== null && (
        <Typography color="error" data-testid="add-card-error" sx={{ mb: 1, flexShrink: 0 }}>
          {addCardError}
        </Typography>
      )}

      {view === 'chart' && (
        <Box sx={{ flexShrink: 0 }}>
          <MetricsPanel boardId={boardId} />
        </Box>
      )}
      {view === 'kanban' && (
      <DndContext
        sensors={sensors}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
        onDragCancel={handleDragCancel}
      >
      <Box
        ref={containerRef}
        data-testid="board-canvas-container"
        // Takes the viewport height below the toolbar (minHeight 0 lets the
        // flex item shrink so the canvas scrolls inside it); vertical scrolling
        // happens here, keeping the horizontal bar at the bottom of the screen.
        // 'scroll' keeps the bar always grabbable; fit-to-window scales the
        // canvas instead (agreed 11-17).
        sx={{
          flexGrow: 1,
          flexBasis: 0,
          minHeight: 0,
          overflowY: 'auto',
          overflowX: isZoomed ? 'hidden' : 'scroll',
        }}
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
              onAddCard={(s, side) => void handleAddCard(s, side)}
              onOpenCard={setEditing}
              hoverDisabled={activeCardId !== null}
            />
          ))}
        </Box>
      </Box>
        <DragOverlay>
          {activeCard ? (
            <Box sx={{ width: 220 }}>
              {/* The drag ghost never needs the contents balloon. */}
              <CardView card={activeCard} disableHoverInfo />
            </Box>
          ) : null}
        </DragOverlay>
      </DndContext>
      )}
      {editing !== null && (
        <CardEditDialog card={editing} onClose={() => setEditing(null)} />
      )}
    </Box>
  )
}
