import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import DragHandleIcon from '@mui/icons-material/DragHandle'
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Radio,
  TextField,
  Typography,
} from '@mui/material'
import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { ApiError, boardsApi, intVal } from '../api/client'
import type { BoardDto, ProcessStepDto } from '../api/types'

// The named step-label colors WPF offers (spec/brief list; WPF's
// DevProcess.AddProcessStep rotates a new step's LabelColor through them).
export const LABEL_COLORS = [
  'Aqua',
  'DarkOrange',
  'LimeGreen',
  'CornflowerBlue',
  'DarkSeaGreen',
  'Gold',
  'Gray',
  'Green',
  'OrangeRed',
  'Plum',
  'RoyalBlue',
  'Silver',
  'Teal',
  'Tomato',
  'Violet',
] as const

/** Move the item at `from` to position `to` (dnd-kit onDragEnd path). */
export type ReorderFn = (from: number, to: number) => void

/** Display order: ascending phaseSeqNo, whatever order the server array had. */
function bySeqNo(steps: ProcessStepDto[]): ProcessStepDto[] {
  return [...steps].sort((a, b) => intVal(a.phaseSeqNo) - intVal(b.phaseSeqNo))
}

/**
 * PUT body contract (BoardCommandService.UpdateProcessStepsAsync, pinned by
 * BoardCommandServiceTest.UpdateProcessSteps_Reorder_*): `phaseSeqNo` is the
 * column's ORIGINAL identity as GETed — the server maps it back to the right
 * column (and moves its cards) — while the ARRAY ORDER is the new display
 * order. Missing identities are deletions (guarded), identities ≥ the current
 * count are additions. The server itself compacts SeqNo to 0..n-1; the
 * client must NOT renumber, or a reorder would rotate labels over unmoved
 * cards and a middle removal would target the wrong column. Entries are
 * copied (not aliased) so a failed save never leaks mutations into the prop.
 */
function toSaveBody(steps: ProcessStepDto[]): ProcessStepDto[] {
  return steps.map((s) => ({ ...s }))
}

interface StepRowProps {
  step: ProcessStepDto
  index: number
  busy: boolean
  canRemove: boolean
  onField: (index: number, patch: ProcessStepDto) => void
  onCycle: (index: number, which: 'start' | 'end') => void
  onRemove: (index: number) => void
}

// One vertical sortable row: drag handle + the five WPF processStepListBox
// fields (name, labelColor, WIP title, Done title, cycle-time radios) plus
// Remove. The name text is free text — "(Limit:N)" is user-typed, WPF
// parity, never parsed or validated here.
function StepRow({
  step,
  index,
  busy,
  canRemove,
  onField,
  onCycle,
  onRemove,
}: StepRowProps) {
  const { t } = useTranslation()
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: index })

  // The current color stays selectable even when it is not one of the named
  // colors (a prefill must never silently change — CardEditDialog cardType
  // precedent); null/'' gets its own empty option.
  const color = step.labelColor ?? ''
  const options: string[] =
    color !== '' && !(LABEL_COLORS as readonly string[]).includes(color)
      ? [color, ...LABEL_COLORS]
      : [...LABEL_COLORS]
  if (color === '') options.unshift('')

  return (
    <Box
      ref={setNodeRef}
      data-testid={`step-row-${index}`}
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1,
        p: 0.5,
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        opacity: isDragging ? 0.5 : 1,
        transform: CSS.Translate.toString(transform),
        transition,
      }}
    >
      <IconButton
        size="small"
        data-testid={`step-drag-${index}`}
        aria-label={t('ProcessStep_DragHandle')}
        sx={{ touchAction: 'none', cursor: 'grab' }}
        {...attributes}
        {...listeners}
      >
        <DragHandleIcon fontSize="small" />
      </IconButton>
      <TextField
        size="small"
        disabled={busy}
        value={step.name ?? ''}
        onChange={(e) => onField(index, { name: e.target.value })}
        slotProps={{ htmlInput: { 'data-testid': `step-name-${index}` } }}
        sx={{ width: 200 }}
      />
      <Box
        data-testid={`step-swatch-${index}`}
        data-color={color}
        sx={{
          width: 16,
          height: 16,
          flexShrink: 0,
          border: 1,
          borderColor: 'divider',
          bgcolor: color !== '' ? color : 'grey.200',
        }}
      />
      <select
        data-testid={`step-color-${index}`}
        value={color}
        disabled={busy}
        onChange={(e) => onField(index, { labelColor: e.target.value })}
      >
        {options.map((c) => (
          <option key={c} value={c}>
            {c}
          </option>
        ))}
      </select>
      {/* Literal WIP/Done captions, WPF processStepListBoxDataTemplate parity. */}
      <Typography variant="body2">WIP</Typography>
      <TextField
        size="small"
        disabled={busy}
        value={step.wipTitle ?? ''}
        onChange={(e) => onField(index, { wipTitle: e.target.value })}
        slotProps={{ htmlInput: { 'data-testid': `step-wip-${index}` } }}
        sx={{ width: 120 }}
      />
      <Typography variant="body2">Done</Typography>
      <TextField
        size="small"
        disabled={busy}
        value={step.doneTitle ?? ''}
        onChange={(e) => onField(index, { doneTitle: e.target.value })}
        slotProps={{ htmlInput: { 'data-testid': `step-done-${index}` } }}
        sx={{ width: 120 }}
      />
      {/* WPF GroupName semantics: one radio group for start, one for end,
          selection across rows is mutually exclusive (or neither, e.g. a
          pure backlog board). */}
      <FormControlLabel
        control={
          <Radio
            size="small"
            disabled={busy}
            checked={step.isStartOfCycleTime === true}
            onChange={() => onCycle(index, 'start')}
          />
        }
        label={t('Process_MetricsBegin')}
      />
      <FormControlLabel
        control={
          <Radio
            size="small"
            disabled={busy}
            checked={step.isEndOfCycleTime === true}
            onChange={() => onCycle(index, 'end')}
          />
        }
        label={t('Process_MetricsEnd')}
      />
      <IconButton
        size="small"
        data-testid={`step-remove-${index}`}
        disabled={!canRemove || busy}
        title={canRemove ? t('ToolTip_Delete') : t('ProcessStep_CannotRemoveLastStep')}
        onClick={() => onRemove(index)}
      >
        <DeleteIcon fontSize="small" />
      </IconButton>
    </Box>
  )
}

export interface ProcessStepEditorProps {
  board: BoardDto
  open: boolean
  onClose: () => void
  // Test seam (BoardPage's onDragHandlesRef variant): hands the *real*
  // reorder used by onDragEnd to the test — jsdom cannot simulate drags.
  // Production never passes it.
  onReorderRef?: { current: ReorderFn | null }
}

export default function ProcessStepEditor({
  board,
  open,
  onClose,
  onReorderRef,
}: ProcessStepEditorProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  // Conditional mount per opened board (BoardListPage), so the useState
  // initializer is the prefill.
  const [steps, setSteps] = useState<ProcessStepDto[]>(() =>
    bySeqNo(board.processSteps ?? []),
  )
  const [errorText, setErrorText] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Identity allocator for rows added this session: strictly above every
  // original phaseSeqNo, so the server reads them as additions (its rule:
  // phaseSeqNo >= current count) and they can never collide with — or
  // overwrite — a surviving original column. Monotonic per open.
  const nextFreshSeq = useRef(
    Math.max(0, ...(board.processSteps ?? []).map((s) => intVal(s.phaseSeqNo) + 1)),
  )

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
  )

  const reorder = useCallback<ReorderFn>((from, to) => {
    setSteps((list) => arrayMove(list, from, to))
  }, [])
  useEffect(() => {
    if (onReorderRef) onReorderRef.current = reorder
  }, [onReorderRef, reorder])

  const handleDragEnd = useCallback(
    (event: DragEndEvent): void => {
      const { active, over } = event
      if (!over || active.id === over.id) return
      reorder(Number(active.id), Number(over.id))
    },
    [reorder],
  )

  const patchStep = useCallback((index: number, patch: ProcessStepDto): void => {
    setSteps((list) => list.map((s, i) => (i === index ? { ...s, ...patch } : s)))
  }, [])

  // Radio invariant (WPF GroupName parity): checking a row's start clears it
  // on every other row — at most one start and one end, or neither.
  const setCycle = useCallback((index: number, which: 'start' | 'end'): void => {
    setSteps((list) =>
      list.map((s, i) =>
        which === 'start'
          ? { ...s, isStartOfCycleTime: i === index }
          : { ...s, isEndOfCycleTime: i === index },
      ),
    )
  }, [])

  // WPF DevProcess.AddProcessStep defaults, localized through the same resx
  // keys WPF uses; color rotates through LABEL_COLORS by display position.
  const handleAdd = (): void => {
    setErrorText(null)
    const fresh = nextFreshSeq.current
    nextFreshSeq.current += 1
    setSteps((list) => {
      return [
        ...list,
        {
          phaseSeqNo: fresh,
          name: t('Process_NewProcessStepTitle'),
          labelColor: LABEL_COLORS[list.length % LABEL_COLORS.length],
          wipTitle: t('Process_WIPTitle'),
          doneTitle: t('Process_DoneTitle'),
          isStartOfCycleTime: false,
          isEndOfCycleTime: false,
        },
      ]
    })
  }

  const handleRemove = (index: number): void => {
    if (steps.length <= 1) return // guard: the button is disabled too
    setErrorText(null)
    setSteps((list) => list.filter((_, i) => i !== index))
  }

  const handleSave = async (): Promise<void> => {
    const id = board.id ?? ''
    if (id === '' || busy) return
    setBusy(true)
    setErrorText(null)
    try {
      await boardsApi.updateSteps(id, toSaveBody(steps))
      void queryClient.invalidateQueries({ queryKey: ['board'] })
      void queryClient.invalidateQueries({ queryKey: ['boards'] })
      onClose()
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        // Server guard won (e.g. <3 steps, column still holds cards, a
        // concurrent edit): show the conflict and reload the server's truth.
        setErrorText(t('ProcessStep_Conflict'))
        try {
          const fresh = (await boardsApi.list()).find((b) => b.id === id)
          const fetched = bySeqNo(fresh?.processSteps ?? [])
          setSteps(fetched)
          // Reseed the allocator: the refetched state may carry identities at
          // or above the counter seeded at mount, and a later Add must never
          // emit a duplicate identity (server would 400).
          nextFreshSeq.current = Math.max(
            nextFreshSeq.current,
            ...fetched.map((s) => intVal(s.phaseSeqNo) + 1),
          )
        } catch {
          // Re-read failed too — the conflict notice stands; the dialog
          // stays open and usable.
        }
      } else {
        setErrorText(t('ProcessStep_SaveError'))
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog
      open={open}
      data-testid="process-step-editor"
      onClose={busy ? undefined : onClose}
      maxWidth="lg"
    >
      <DialogTitle>{t('Main_Process')}</DialogTitle>
      <DialogContent>
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={handleDragEnd}
        >
          <SortableContext
            items={steps.map((_, i) => i)}
            strategy={verticalListSortingStrategy}
          >
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, pt: 0.5 }}>
              {steps.map((step, i) => (
                <StepRow
                  key={`step-row-${i}`}
                  step={step}
                  index={i}
                  busy={busy}
                  canRemove={steps.length > 1}
                  onField={patchStep}
                  onCycle={setCycle}
                  onRemove={handleRemove}
                />
              ))}
            </Box>
          </SortableContext>
        </DndContext>
        <Box sx={{ mt: 1 }}>
          <Button
            size="small"
            startIcon={<AddIcon />}
            data-testid="step-add-button"
            disabled={busy}
            onClick={handleAdd}
          >
            {t('ToolTip_AddProcessStep')}
          </Button>
        </Box>
        {errorText !== null && (
          <Typography variant="body2" color="error" data-testid="process-step-error">
            {errorText}
          </Typography>
        )}
      </DialogContent>
      <DialogActions>
        <Button data-testid="step-close-button" onClick={onClose} disabled={busy}>
          {t('View_Close')}
        </Button>
        <Button
          data-testid="step-save-button"
          variant="contained"
          disabled={busy}
          onClick={() => void handleSave()}
        >
          {t('CardEdit_Save')}
        </Button>
      </DialogActions>
    </Dialog>
  )
}