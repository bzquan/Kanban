import {
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  TextField,
  Typography,
} from '@mui/material'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { cardsApi, intVal } from '../api/client'
import type { CardDto, CardPatch } from '../api/types'
import { CARD_TYPE_LABELS } from '../cardType'
import { format0 } from '../util/format'

// WPF parity (spec §4.5): the dialog edits exactly the 13 CardPatch fields
// in the WPF EditCardDialog's section order — title → tag → executors →
// cardType → storyPoints → releaseDate → checkboxes → summary/testCases/
// designOverview. The three HTML fields are raw textareas (WPF TextBox
// parity, no WYSIWYG).

function pad2(n: number): string {
  return String(n).padStart(2, '0')
}

/**
 * Calendar date shown by the type="date" input, in browser-local terms —
 * the same wall-clock reading the WPF DatePicker shows. Verified against
 * the live API (2026-11-13): GET /cards returns releaseDate as
 * "YYYY-MM-DDTHH:mm:ss+09:00", POST/PATCH responses as "...Z"; both parse
 * through Date to the same local calendar date on the LAN's shared
 * timezone. Dates with year ≤ 1 (the .NET default, e.g. an unset
 * DateTime serialized pre-normalization) count as empty.
 */
export function toLocalDateInput(iso: string | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime()) || d.getFullYear() <= 1) return ''
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`
}

/**
 * storyPoints text → non-negative int32 (the API's Format:int32);
 * '' → 0 (WPF's int default). null = invalid → localized error, Save blocked.
 */
export function parseStoryPoints(text: string): number | null {
  const trimmed = text.trim()
  if (trimmed === '') return 0
  if (!/^\d+$/.test(trimmed)) return null
  const n = Number(trimmed)
  return Number.isSafeInteger(n) && n >= 0 && n <= 2_147_483_647 ? n : null
}

export interface CardEditDialogProps {
  card: CardDto
  onClose: () => void
}

export default function CardEditDialog({ card, onClose }: CardEditDialogProps) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()

  // Prefill from the card; the parent mounts one dialog per opened card
  // (conditional render), so useState initializers are the prefill.
  const [title, setTitle] = useState(card.title ?? '')
  const [tag, setTag] = useState(card.tag ?? '')
  const [executors, setExecutors] = useState(card.executors ?? '')
  const [cardType, setCardType] = useState(card.cardType ?? '')
  const [storyPointsText, setStoryPointsText] = useState(
    String(intVal(card.storyPoints)),
  )
  const [releaseDate, setReleaseDate] = useState(toLocalDateInput(card.releaseDate))
  const [isBlocked, setIsBlocked] = useState(card.isBlocked === true)
  const [isMergedIntoMaster, setIsMergedIntoMaster] = useState(
    card.isMergedIntoMaster === true,
  )
  const [isMergedIntoMajorBranch, setIsMergedIntoMajorBranch] = useState(
    card.isMergedIntoMajorBranch === true,
  )
  const [isScheduledToRelease, setIsScheduledToRelease] = useState(
    card.isScheduledToRelease === true,
  )
  const [summary, setSummary] = useState(card.summary ?? '')
  const [testCases, setTestCases] = useState(card.testCases ?? '')
  const [designOverview, setDesignOverview] = useState(card.designOverview ?? '')

  const [storyPointsError, setStoryPointsError] = useState<string | null>(null)
  const [errorText, setErrorText] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const labelSx = { minWidth: 180 } as const

  // cardType select: CARD_TYPE_LABELS in the current language (same base-
  // language pick as CardView's typeLabel); unknown/empty values stay
  // selectable so a prefill never silently changes.
  const base = (i18n.language ?? 'en').toLowerCase().split('-')[0]
  const lang: 'ja' | 'zh' | 'en' = base === 'ja' || base === 'zh' ? base : 'en'
  const typeOptions: { value: string; label: string }[] = Object.entries(
    CARD_TYPE_LABELS,
  ).map(([value, entry]) => ({ value, label: entry[lang] }))
  if (cardType !== '' && !(cardType in CARD_TYPE_LABELS)) {
    typeOptions.unshift({ value: cardType, label: cardType })
  }
  if (cardType === '') {
    typeOptions.unshift({ value: '', label: '' })
  }

  const buildPatch = (): CardPatch | null => {
    const storyPoints = parseStoryPoints(storyPointsText)
    setStoryPointsError(storyPoints === null ? t('CardEdit_StoryPointsInvalid') : null)
    if (storyPoints === null) return null

    const patch: CardPatch = {}
    if (title !== (card.title ?? '')) patch.title = title
    if (tag !== (card.tag ?? '')) patch.tag = tag
    if (executors !== (card.executors ?? '')) patch.executors = executors
    if (summary !== (card.summary ?? '')) patch.summary = summary
    if (testCases !== (card.testCases ?? '')) patch.testCases = testCases
    if (designOverview !== (card.designOverview ?? '')) {
      patch.designOverview = designOverview
    }
    if (cardType !== (card.cardType ?? '')) patch.cardType = cardType
    if (storyPoints !== intVal(card.storyPoints)) patch.storyPoints = storyPoints
    // Naive local midnight: the running API parses it as server-local
    // (verified live 2026-11-13: "2026-12-01T00:00:00" GETs back as
    // "2026-12-01T00:00:00+09:00"), so the wall-clock date round-trips.
    // Clearing sends null — the API ignores it (dates cannot be unset).
    const originalDate = toLocalDateInput(card.releaseDate)
    if (releaseDate !== originalDate) {
      patch.releaseDate = releaseDate === '' ? null : `${releaseDate}T00:00:00`
    }
    if (isBlocked !== (card.isBlocked === true)) patch.isBlocked = isBlocked
    if (isMergedIntoMaster !== (card.isMergedIntoMaster === true)) {
      patch.isMergedIntoMaster = isMergedIntoMaster
    }
    if (isMergedIntoMajorBranch !== (card.isMergedIntoMajorBranch === true)) {
      patch.isMergedIntoMajorBranch = isMergedIntoMajorBranch
    }
    if (isScheduledToRelease !== (card.isScheduledToRelease === true)) {
      patch.isScheduledToRelease = isScheduledToRelease
    }
    return patch
  }

  const handleSave = async (): Promise<void> => {
    if (!card.id) return
    const patch = buildPatch()
    if (patch === null) return // validation blocked
    if (Object.keys(patch).length === 0) {
      onClose() // nothing changed — no PATCH needed
      return
    }
    setBusy(true)
    setErrorText(null)
    try {
      await cardsApi.patch(card.id, patch)
      void queryClient.invalidateQueries({ queryKey: ['cards'] })
      onClose()
    } catch {
      setErrorText(t('CardEdit_SaveError'))
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async (): Promise<void> => {
    if (!card.id) return
    // .NET-style confirm through the existing resx keys.
    if (!window.confirm(format0(t('Message_DeleteComfirmQuesion'), card.title ?? ''))) {
      return
    }
    setBusy(true)
    setErrorText(null)
    try {
      await cardsApi.remove(card.id)
      void queryClient.invalidateQueries({ queryKey: ['cards'] })
      onClose()
    } catch {
      setErrorText(t('CardEdit_DeleteError'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog open data-testid="card-edit-dialog" onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t('CardEdit_Title')}</DialogTitle>
      <DialogContent>
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: 0.5 }}>
          {/* WPF section order: title → tag → executors → cardType →
              storyPoints → releaseDate → checkboxes → summary/testCases/
              designOverview. */}
          <TextField
            size="small"
            label={t('CardEdit_WorkItem')}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'field-title' } }}
          />
          <TextField
            size="small"
            label={t('CardEdit_Tag')}
            value={tag}
            onChange={(e) => setTag(e.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'field-tag' } }}
          />
          <TextField
            size="small"
            label={t('CardEdit_ResponsiblePerson')}
            value={executors}
            onChange={(e) => setExecutors(e.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'field-executors' } }}
          />
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography variant="body2" sx={labelSx}>
              {t('CardEdit_CardType')}
            </Typography>
            <select
              data-testid="field-cardType"
              value={cardType}
              onChange={(e) => setCardType(e.target.value)}
            >
              {typeOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </Box>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography variant="body2" sx={labelSx}>
                {t('CardEdit_StoryPoints')}
              </Typography>
              <input
                data-testid="field-storyPoints"
                inputMode="numeric"
                value={storyPointsText}
                onChange={(e) => setStoryPointsText(e.target.value)}
              />
            </Box>
            {storyPointsError !== null && (
              <Typography variant="caption" color="error">
                {storyPointsError}
              </Typography>
            )}
          </Box>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography variant="body2" sx={labelSx}>
              {t('CardEdit_ReleaseDate')}
            </Typography>
            <TextField
              size="small"
              type="date"
              value={releaseDate}
              onChange={(e) => setReleaseDate(e.target.value)}
              slotProps={{ htmlInput: { 'data-testid': 'field-releaseDate' } }}
            />
          </Box>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            <FormControlLabel
              control={
                <Checkbox
                  checked={isBlocked}
                  onChange={(e) => setIsBlocked(e.target.checked)}
                />
              }
              label={t('CardEdit_Blocked')}
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={isMergedIntoMaster}
                  onChange={(e) => setIsMergedIntoMaster(e.target.checked)}
                />
              }
              label={t('CardEdit_MergedIntoMaster')}
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={isMergedIntoMajorBranch}
                  onChange={(e) => setIsMergedIntoMajorBranch(e.target.checked)}
                />
              }
              label={t('CardEdit_MergedIntoMajorBranch')}
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={isScheduledToRelease}
                  onChange={(e) => setIsScheduledToRelease(e.target.checked)}
                />
              }
              label={t('CardEdit_IsScheduledToRelease')}
            />
          </Box>
          {/* Raw HTML-text editing: WPF TextBox parity (spec §4.5), no WYSIWYG. */}
          <TextField
            size="small"
            label={t('CardEdit_Description')}
            multiline
            minRows={3}
            value={summary}
            onChange={(e) => setSummary(e.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'field-summary' } }}
          />
          <TextField
            size="small"
            label={t('CardEdit_TestCases')}
            multiline
            minRows={3}
            value={testCases}
            onChange={(e) => setTestCases(e.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'field-testCases' } }}
          />
          <TextField
            size="small"
            label={t('CardEdit_DesignOverview')}
            multiline
            minRows={3}
            value={designOverview}
            onChange={(e) => setDesignOverview(e.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'field-designOverview' } }}
          />
          {errorText !== null && (
            <Typography variant="body2" color="error" data-testid="card-edit-error">
              {errorText}
            </Typography>
          )}
        </Box>
      </DialogContent>
      <DialogActions>
        <Button data-testid="card-edit-delete" color="error" onClick={() => void handleDelete()} disabled={busy}>
          {t('ToolTip_Delete')}
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        <Button data-testid="card-edit-close" onClick={onClose} disabled={busy}>
          {t('View_Close')}
        </Button>
        <Button
          data-testid="card-edit-save"
          variant="contained"
          onClick={() => void handleSave()}
          disabled={busy}
        >
          {t('CardEdit_Save')}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
