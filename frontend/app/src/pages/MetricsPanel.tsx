// Task 10: the metrics panel — 5 ECharts charts (CFD, cycle-time
// distribution, throughput, bugs & blocked, team velocity) behind localized
// tabs, with the WPF summary lines rendered as text under the chart.
//
// Chart-type choice persists in localStorage['kanban.lastMetricChartType']
// and is restored on mount (unknown/absent → first chart). Data comes from
// metricsApi through TanStack Query, one query per chart type
// (['metrics', type, boardId]) — switching tabs fetches lazily and caches.
import { Box, CircularProgress, Tab, Tabs, Typography } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import type { EChartsOption } from 'echarts'
import { useCallback, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { intVal, metricsApi } from '../api/client'
import type {
  BugsBlockedDto,
  CfdDto,
  CycleTimeDto,
  ThroughputDto,
  VelocityDto,
} from '../api/types'
import {
  bugsBlockedOption,
  cfdOption,
  cycleTimeOption,
  throughputOption,
  useECharts,
  velocityOption,
} from '../charts/options'
import { format0 } from '../util/format'

const LAST_CHART_TYPE_KEY = 'kanban.lastMetricChartType'

export const METRIC_CHART_TYPES = [
  'cfd',
  'cycleTime',
  'throughput',
  'bugsBlocked',
  'velocity',
] as const
export type MetricChartType = (typeof METRIC_CHART_TYPES)[number]

// en/ja/zh tab labels live in the resx trio (WPF MetricChartType_* texts).
const TAB_LABEL_KEYS: Record<MetricChartType, string> = {
  cfd: 'MetricChartType_CFD',
  cycleTime: 'MetricChartType_CycleTimeDistribution',
  throughput: 'MetricChartType_Throughput',
  bugsBlocked: 'MetricChartType_BugsAndBlocked',
  velocity: 'MetricChartType_TeamVelocity',
}

/** Restores the persisted choice; absent/bogus value → first chart type. */
function readStoredChartType(): MetricChartType {
  try {
    const stored = localStorage.getItem(LAST_CHART_TYPE_KEY)
    if (stored && (METRIC_CHART_TYPES as readonly string[]).includes(stored)) {
      return stored as MetricChartType
    }
  } catch {
    // storage unavailable (private mode) — fall back below
  }
  return METRIC_CHART_TYPES[0]
}

const EMPTY_OPTION: EChartsOption = {}

/** One number for the summary lines: one decimal, trailing .0 dropped (WPF {0:0.#}). */
function fmtNum(v: number | string | undefined): string {
  const n = intVal(v)
  return String(Math.round(n * 10) / 10)
}

/** ISO date → locale short date; unparseable input is shown verbatim. */
function fmtDate(iso: string | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString()
}

// Discriminated union so the option/summary switch narrows each DTO without
// casting (query.data cannot know the type from chartType on its own).
type MetricData =
  | { type: 'cfd'; dto: CfdDto }
  | { type: 'cycleTime'; dto: CycleTimeDto }
  | { type: 'throughput'; dto: ThroughputDto }
  | { type: 'bugsBlocked'; dto: BugsBlockedDto }
  | { type: 'velocity'; dto: VelocityDto }

async function fetchMetric(type: MetricChartType, boardId: string): Promise<MetricData> {
  switch (type) {
    case 'cfd':
      return { type, dto: await metricsApi.cfd(boardId) }
    case 'cycleTime':
      return { type, dto: await metricsApi.cycleTime(boardId) }
    case 'throughput':
      return { type, dto: await metricsApi.throughput(boardId) }
    case 'bugsBlocked':
      return { type, dto: await metricsApi.bugsBlocked(boardId) }
    case 'velocity':
      return { type, dto: await metricsApi.velocity(boardId) }
  }
}

interface MetricsPanelProps {
  boardId: string
}

export default function MetricsPanel({ boardId }: MetricsPanelProps) {
  const { t } = useTranslation()
  const [chartType, setChartType] = useState<MetricChartType>(readStoredChartType)
  const chartRef = useRef<HTMLDivElement>(null)

  const query = useQuery({
    queryKey: ['metrics', chartType, boardId],
    queryFn: () => fetchMetric(chartType, boardId),
  })

  const selectChart = useCallback((next: MetricChartType): void => {
    setChartType(next)
    try {
      localStorage.setItem(LAST_CHART_TYPE_KEY, next)
    } catch {
      // best-effort; the in-memory switch still applies
    }
  }, [])

  // One memo builds the ECharts option AND the localized summary lines, so
  // both come from the same DTO snapshot. Series names are t() values — the
  // builders themselves stay i18n-free (see src/charts/options.ts).
  const { option, summaryLines } = useMemo<{ option: EChartsOption; summaryLines: string[] }>(
    () => {
      const data = query.data
      if (!data) return { option: EMPTY_OPTION, summaryLines: [] }
      switch (data.type) {
        case 'cfd':
          return {
            option: cfdOption(data.dto),
            summaryLines: [
              `${t('Metrics_MaxWIP')} ${intVal(data.dto.summary?.maxWip)}, ` +
                `${t('Metrics_AveWIP')} ${fmtNum(data.dto.summary?.averageWip)}`,
              `${t('Metrics_OccuredDate')} ${fmtDate(data.dto.summary?.maxWipDate)}`,
            ],
          }
        case 'cycleTime':
          return {
            option: cycleTimeOption(data.dto, {
              average: t('Metrics_AveCycleTime'),
              distribution: t('Metrics_DistributionLabel'),
            }),
            summaryLines: [
              `${t('Metrics_AveCycleTime')}${fmtNum(
                data.dto.weeklyAverageCycleTimeDays,
              )}${t('Metrics_FormatLabelDay')}`,
            ],
          }
        case 'throughput': {
          const est = intVal(data.dto.summary?.estimatedCycleTimeWeeks)
          return {
            option: throughputOption(data.dto, {
              items: t('Metrics_ItemsLabel'),
              ahead: t('Metrics_EstimationMuch'),
              onTime: t('Metrics_EstimationProper'),
              delayed: t('Metrics_EstimationSmall'),
            }),
            summaryLines: [
              `${t('Metrics_AveThroughput')}${fmtNum(
                data.dto.summary?.realAverageThroughputPerWeek,
              )}${t('Metrics_PerWeek')}`,
              // No completions to estimate from → WPF shows "∞ weeks".
              est > 0
                ? `${t('Metrics_EstimatedCycleTime')}${fmtNum(est)}${t('Metrics_Weeks')}`
                : `${t('Metrics_EstimatedCycleTime')}${t('Metrics_UnknownWeeks')}`,
            ],
          }
        }
        case 'bugsBlocked':
          return {
            option: bugsBlockedOption(data.dto, {
              bugFix: t('Metrics_ItemsOfBugfix'),
              blocked: t('Metrics_ItemsOfBlocked'),
            }),
            summaryLines: [],
          }
        case 'velocity':
          return {
            option: velocityOption(data.dto, { storyPoints: t('Metrics_StoryPointsLabel') }),
            summaryLines: [
              format0(t('Metrics_Last6WeeksAveVelocity'), fmtNum(data.dto.averageVelocity4Every2Weeks)),
            ],
          }
      }
    },
    [query.data, t],
  )

  useECharts(chartRef, option)

  return (
    <Box data-testid="metrics-panel">
      <Tabs
        value={chartType}
        onChange={(_event, next: MetricChartType) => selectChart(next)}
        variant="scrollable"
        scrollButtons="auto"
        allowScrollButtonsMobile
        sx={{ mb: 1 }}
      >
        {METRIC_CHART_TYPES.map((type) => (
          <Tab
            key={type}
            value={type}
            data-testid={`metrics-tab-${type}`}
            label={t(TAB_LABEL_KEYS[type])}
          />
        ))}
      </Tabs>
      {query.isLoading && (
        <Box sx={{ p: 1 }}>
          <CircularProgress size={24} />
        </Box>
      )}
      {query.isError && (
        <Typography color="error" data-testid="metrics-error">
          {t('Metrics_LoadError')}
        </Typography>
      )}
      <Box ref={chartRef} data-testid="metrics-chart" sx={{ width: '100%', height: 360 }} />
      <Box data-testid="metrics-summary" sx={{ mt: 1 }}>
        {summaryLines.map((line) => (
          <Typography key={line} variant="body2" color="text.secondary">
            {line}
          </Typography>
        ))}
      </Box>
    </Box>
  )
}
