import { Box, Chip, LinearProgress, Tooltip, Typography } from '@mui/material'
import { useTranslation } from 'react-i18next'

import { intVal } from '../api/client'
import type { CardDto } from '../api/types'
import { CARD_TYPE_LABELS, cardTypeBackground, textColorFor } from '../cardType'
import { format0 } from '../util/format'

export interface CardViewProps {
  card: CardDto
  onOpen?: () => void
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

export default function CardView({ card, onOpen }: CardViewProps) {
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

  return (
    <Box
      data-testid="card"
      onClick={onOpen}
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
          <Box data-testid="progress-bar" sx={{ mt: 0.5 }}>
            <LinearProgress
              variant="determinate"
              value={Math.round(progress * 100)}
              aria-label={t('ToolTip_WorkedDays')}
            />
          </Box>
        </Tooltip>
      )}
    </Box>
  )
}
