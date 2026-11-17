import { Box, Chip, LinearProgress, Tooltip, Typography } from '@mui/material'
import { useTranslation } from 'react-i18next'

import { intVal } from '../api/client'
import type { CardDto } from '../api/types'
import { CARD_TYPE_LABELS, cardTypeBackground, textColorFor } from '../cardType'
import { format0 } from '../util/format'

export interface CardViewProps {
  card: CardDto
  onOpen?: () => void
  // Board-level "a drag is in progress" flag (DndContext): hovers must not
  // fire balloons while the pointer sweeps cards mid-drag.
  disableHoverInfo?: boolean
}

function startOfLocalDay(iso: string | undefined): Date | null {
  if (!iso) return null
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return null
  return new Date(d.getFullYear(), d.getMonth(), d.getDate())
}

/**
 * Port of Kanban.Util.DateTimeUtil.WorkDays (Kanban.Domain/Util/
 * DateTimeUtil.cs:30-39): counts calendar days from from.Date through
 * to.Date inclusive, keeping only IsWorkingDay ones. IsWorkingDay
 * (DateTimeUtil.cs:24-28) rejects Saturday/Sunday only — the C# has no
 * holiday list, so weekends-only is the faithful behavior.
 * to < from: the C# foreach never iterates → 0.
 */
export function workDays(
  fromIso: string | undefined,
  toIso: string | undefined,
): number {
  const from = startOfLocalDay(fromIso)
  const to = startOfLocalDay(toIso)
  if (!from || !to || to.getTime() < from.getTime()) return 0
  let totalDays = 0
  // setDate-based stepping keeps DST days at 24h semantics (a raw 86400000ms
  // step would skip/repeat a day across DST boundaries).
  for (const day = new Date(from); day <= to; day.setDate(day.getDate() + 1)) {
    const dow = day.getDay()
    if (dow !== 0 /* Sun */ && dow !== 6 /* Sat */) totalDays++
  }
  return totalDays
}

/**
 * Progress of a scheduled-to-release card: elapsed workdays
 * createDate→stateChangedDate against the remaining workdays
 * stateChangedDate→releaseDate, i.e. elapsed/(elapsed+remaining) clamped
 * to [0,1]. The two legs mirror the two legs the WPF worked-days bar
 * measures over (Card.cs:338-375 → CardMetrics.GetWorkedDays,
 * Kanban.Domain/Model/CardMetrics.cs:29-46 — start activity → end
 * activity/"now"); the web has no activity trail in the DTO, so
 * stateChangedDate is the split point, as pinned by the brief. Missing or
 * unparseable dates → 0 (bar renders empty, never NaN).
 */
export function releaseProgressRatio(
  createDate: string | undefined,
  stateChangedDate: string | undefined,
  releaseDate: string | undefined,
): number {
  const elapsed = workDays(createDate, stateChangedDate)
  const remaining = workDays(stateChangedDate, releaseDate)
  const total = elapsed + remaining
  if (total <= 0) return 0
  return Math.min(1, Math.max(0, elapsed / total))
}

function typeLabel(cardType: string | undefined, language: string | undefined): string | null {
  if (!cardType) return null
  const entry = CARD_TYPE_LABELS[cardType]
  if (!entry) return cardType // unknown enum name: surface it raw rather than hide
  const base = (language ?? 'en').toLowerCase().split('-')[0]
  if (base === 'ja' || base === 'zh') return entry[base]
  return entry.en
}

// WPF chip colors: block → DeepPink, master → Green, major branch → Cyan
// (Card.cs:377-395 CardTitleBackgroundBrush).
const BLOCKED_CHIP_BG = '#FF1493'
const MERGED_MASTER_BG = '#008000'
const MERGED_MAJOR_BG = '#00FFFF'
// Story-points circle default branch (Card.cs:254-255, unknown effort):
// Red background / White font. EstimatedWorkEffort is not on the DTO, so
// every badge takes the WPF default look.
const STORY_POINTS_BG = '#FF0000'
const STORY_POINTS_FG = '#FFFFFF'

// WPF card ToolTip parity (BoardPage.xaml:47-48 ← Card.CardContentsInfo):
// hover surfaces the three text fields the card face hides. The ~0.5s delay
// mirrors WPF's hover threshold so sweeping the mouse across the board does
// not fire tooltips.
const HOVER_ENTER_DELAY_MS = 500

function HoverInfo({ card }: { card: CardDto }) {
  const { t } = useTranslation()
  // WPF CardContentsInfo order: description → test cases → design overview,
  // each section only when non-empty.
  const sections = [
    { key: 'summary', testId: 'hover-summary', label: t('CardEdit_Description'), html: card.summary ?? '' },
    { key: 'testCases', testId: 'hover-test-cases', label: t('CardEdit_TestCases'), html: card.testCases ?? '' },
    { key: 'designOverview', testId: 'hover-design-overview', label: t('CardEdit_DesignOverview'), html: card.designOverview ?? '' },
  ].filter((s) => s.html !== '')
  return (
    <Box sx={{ p: 1, maxWidth: 480, maxHeight: 340, overflowY: 'auto' }}>
      {sections.map((s) => (
        <Box key={s.key} sx={{ mb: 1 }}>
          <Typography variant="caption" component="div" sx={{ fontWeight: 700 }}>
            {s.label}
          </Typography>
          <Box data-testid={s.testId} dangerouslySetInnerHTML={{ __html: s.html }} />
        </Box>
      ))}
    </Box>
  )
}

export default function CardView({ card, onOpen, disableHoverInfo }: CardViewProps) {
  const { t, i18n } = useTranslation()
  const background = cardTypeBackground(card.cardType)
  const textColor = textColorFor(background)
  const storyPoints = intVal(card.storyPoints)
  const label = typeLabel(card.cardType, i18n.language)
  const progress = releaseProgressRatio(
    card.createDate,
    card.stateChangedDate,
    card.releaseDate,
  )
  const hasChips = card.isBlocked === true || card.isMergedIntoMaster === true || card.isMergedIntoMajorBranch === true
  // WPF showed the ToolTip even for a bare title; the web card face already
  // carries the title, so an all-empty HoverInfo would be an empty balloon.
  const hasHoverInfo = Boolean(card.summary || card.testCases || card.designOverview)

  return (
    <Tooltip
      title={hasHoverInfo ? <HoverInfo card={card} /> : ''}
      enterDelay={HOVER_ENTER_DELAY_MS}
      disableHoverListener={disableHoverInfo}
    >
    <Box
      data-testid="card"
      role={onOpen ? 'button' : undefined}
      tabIndex={onOpen ? 0 : undefined}
      onClick={onOpen}
      onKeyDown={
        onOpen
          ? (e) => {
              // Keyboard parity with the button semantics: Enter/Space open
              // the editor (Task 6 carry-over; the mouse path is onClick).
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                onOpen()
              }
            }
          : undefined
      }
      sx={{
        p: 0.75,
        mb: 0.5,
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        bgcolor: background,
        color: textColor,
        cursor: onOpen ? 'pointer' : 'default',
      }}
    >
      <Typography variant="body2" component="div" sx={{ fontWeight: 600 }}>
        {card.title ?? ''}
      </Typography>

      {card.summary ? (
        // Spec §4.2: the board renders summary HTML directly, sanitization
        // none — the accepted LAN trust-model trade-off (do not "fix").
        <details data-testid="card-summary">
          <summary
            onClick={(e) => {
              // The card root is click-to-edit; the summary is a control of
              // its own (native details toggle), keep the click off the root.
              e.stopPropagation()
            }}
            onKeyDown={(e) => {
              // Same for the root's Enter/Space open handler — without this
              // its preventDefault() would kill the native toggle *and*
              // open the edit dialog over the freshly expanded details.
              if (e.key === 'Enter' || e.key === ' ') e.stopPropagation()
            }}
          >
            {t('CheckBoxSwitch_ShowDetail')}
          </summary>
          <Box
            component="div"
            sx={{ fontSize: 12 }}
            dangerouslySetInnerHTML={{ __html: card.summary }}
          />
        </details>
      ) : null}

      {label !== null && (
        <Typography variant="caption" component="div" data-testid="card-type-label">
          {label}
        </Typography>
      )}

      {card.tag ? (
        <Typography variant="caption" component="div" data-testid="card-tag">
          {format0(t('Card_TagFormat'), card.tag)}
        </Typography>
      ) : null}

      {card.executors ? (
        // WPF renders executors on LightYellow with bold blue names
        // (BoardPage.xaml:67-71).
        <Box
          data-testid="card-executors"
          sx={{ bgcolor: '#FFFACD', color: '#000', px: 0.25, my: 0.25 }}
        >
          <Typography variant="caption" component="span">
            {t('Card_ResponsiblePerson')}
          </Typography>
          <Typography
            variant="caption"
            component="span"
            sx={{ fontWeight: 700, color: '#00F' }}
          >
            {card.executors}
          </Typography>
        </Box>
      ) : null}

      {hasChips && (
        <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', my: 0.25 }}>
          {card.isBlocked === true && (
            <Chip
              data-testid="blocked-chip"
              size="small"
              label={t('CardEdit_Blocked')}
              sx={{ bgcolor: BLOCKED_CHIP_BG, color: textColorFor(BLOCKED_CHIP_BG), fontWeight: 700 }}
            />
          )}
          {card.isMergedIntoMaster === true && (
            <Chip
              data-testid="merged-master-chip"
              size="small"
              label={t('CardEdit_MergedIntoMaster')}
              sx={{ bgcolor: MERGED_MASTER_BG, color: textColorFor(MERGED_MASTER_BG) }}
            />
          )}
          {card.isMergedIntoMajorBranch === true && (
            <Chip
              data-testid="merged-major-chip"
              size="small"
              label={t('CardEdit_MergedIntoMajorBranch')}
              sx={{ bgcolor: MERGED_MAJOR_BG, color: textColorFor(MERGED_MAJOR_BG) }}
            />
          )}
        </Box>
      )}

      {storyPoints > 0 && (
        <Tooltip title={t('ToolTip_StoryPoints')}>
          <Box
            data-testid="story-points-badge"
            // The badge carries its own tooltip; keeping the hover events from
            // bubbling stops the card-level contents tooltip opening as well.
            onMouseOver={(e) => e.stopPropagation()}
            onMouseLeave={(e) => e.stopPropagation()}
            sx={{
              display: 'inline-block',
              minWidth: 24,
              px: 0.5,
              mt: 0.25,
              borderRadius: '50%',
              textAlign: 'center',
              bgcolor: STORY_POINTS_BG,
              color: STORY_POINTS_FG,
              fontWeight: 700,
            }}
          >
            {storyPoints}
          </Box>
        </Tooltip>
      )}

      {card.isScheduledToRelease === true && (
        <Tooltip title={t('ToolTip_WorkedDays')}>
          <Box
            data-testid="progress-bar"
            // Same as the story-points badge: own tooltip only, no card
            // contents balloon from the bubbled hover.
            onMouseOver={(e) => e.stopPropagation()}
            onMouseLeave={(e) => e.stopPropagation()}
            sx={{ mt: 0.5 }}
          >
            <LinearProgress
              variant="determinate"
              value={Math.round(progress * 100)}
              aria-label={t('ToolTip_WorkedDays')}
            />
          </Box>
        </Tooltip>
      )}
    </Box>
    </Tooltip>
  )
}
