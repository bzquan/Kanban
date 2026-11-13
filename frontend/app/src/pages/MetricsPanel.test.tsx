// Task 10 Step 5: MetricsPanel tests. useECharts is stubbed via a module
// mock that records the option each render builds (the pure builders stay
// real); the five metrics endpoints are served canned DTO fixtures through
// the shared fetch stub.
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from 'i18next'
import type { EChartsOption } from 'echarts'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import type {
  BugsBlockedDto,
  CfdDto,
  CycleTimeDto,
  ThroughputDto,
  VelocityDto,
} from '../api/types'
import { calls, installFetch, restoreFetch } from '../test/fetchStub'
import { initI18n } from '../i18n'
import { renderWithProviders } from '../test/renderWithProviders'
import en from '../locales/en.json'
import ja from '../locales/ja.json'
import zh from '../locales/zh.json'

import MetricsPanel from './MetricsPanel'

// Mock only the hook — the builders in the same module stay untouched.
const captured: EChartsOption[] = []
vi.mock('../charts/options', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../charts/options')>()
  return {
    ...actual,
    useECharts: (_ref: unknown, option: EChartsOption): void => {
      captured.push(option)
    },
  }
})

const CFD: CfdDto = {
  days: ['2026-11-01', '2026-11-02', '2026-11-03'],
  series: [
    { stepName: 'Backlog', cumulativeFlowCounts: [2, '3', 1] },
    { stepName: 'Development', cumulativeFlowCounts: [3, 2, 4] },
  ],
  summary: {
    totalDays: 3,
    maxWip: 7,
    averageWip: 4.5,
    maxWipDate: '2026-11-02T00:00:00',
    workingDays4MaxWip: 2,
  },
}
const CYCLE_TIME: CycleTimeDto = {
  weekEnds: ['2026-11-07', '2026-11-14'],
  weeks: [
    {
      buckets: [
        { stepName: 'Backlog', percentage: 30 },
        { stepName: 'Development', percentage: 70 },
      ],
    },
    {
      buckets: [
        { stepName: 'Backlog', percentage: 50 },
        { stepName: 'Development', percentage: 50 },
      ],
    },
  ],
  averageCycleTimeDays: [3, 4],
  weeklyAverageCycleTimeDays: 3.5,
}
const THROUGHPUT: ThroughputDto = {
  weekEnds: ['2026-11-07', '2026-11-14'],
  itemCounts: [5, 7],
  performance: [
    { aheadOfScheduleCount: 1, onTimeCount: 3, delayedCount: 1 },
    { aheadOfScheduleCount: 2, onTimeCount: 4, delayedCount: 1 },
  ],
  summary: { realAverageThroughputPerWeek: 6, estimatedCycleTimeWeeks: 2.5 },
}
const BUGS_BLOCKED: BugsBlockedDto = {
  days: ['2026-11-01', '2026-11-02'],
  bugFixCounts: [1, 2],
  blockedCounts: [0, 3],
}
const VELOCITY: VelocityDto = {
  weekEnds: ['2026-11-07', '2026-11-14'],
  storyPoints: ['8', 10],
  hasUnknownStoryPoints: [false, true],
  averageVelocity4Every2Weeks: 9,
}

function installMetricHandlers(): void {
  installFetch({
    'GET /api/boards/b-1/metrics/cfd': () => CFD,
    'GET /api/boards/b-1/metrics/cycle-time': () => CYCLE_TIME,
    'GET /api/boards/b-1/metrics/throughput': () => THROUGHPUT,
    'GET /api/boards/b-1/metrics/bugs-blocked': () => BUGS_BLOCKED,
    'GET /api/boards/b-1/metrics/velocity': () => VELOCITY,
  })
}

function fetchedChartPaths(): string[] {
  return calls
    .filter((c) => c.method === 'GET' && c.url.includes('/metrics/'))
    .map((c) => c.url.slice(c.url.indexOf('/metrics/')))
}

/** The most recently captured option that actually has series. */
function lastOptionWithSeries(): EChartsOption {
  const withSeries = captured.filter(
    (o) => ((o as { series?: unknown[] }).series?.length ?? 0) > 0,
  )
  const last = withSeries.at(-1)
  if (!last) throw new Error('no option with series captured yet')
  return last
}

function seriesOf(option: EChartsOption): Record<string, unknown>[] {
  return (option as { series: Record<string, unknown>[] }).series
}

function renderPanel() {
  return renderWithProviders(<MetricsPanel boardId="b-1" />)
}

beforeEach(() => {
  localStorage.clear()
  captured.length = 0
  void initI18n()
  void i18n.changeLanguage('en')
})

afterEach(() => {
  restoreFetch()
  localStorage.clear()
})

it('renders 5 localized chart tabs, loads CFD by default and builds its option', async () => {
  installMetricHandlers()
  renderPanel()

  expect(await screen.findByTestId('metrics-panel')).toBeInTheDocument()
  // Tab labels come from the MetricChartType_* resx trio (WPF enum texts).
  expect(screen.getByTestId('metrics-tab-cfd')).toHaveTextContent(
    'Cumulative Flow Diagram(CFD)',
  )
  expect(screen.getByTestId('metrics-tab-cycleTime')).toHaveTextContent(
    'Cycle time distribution',
  )
  expect(screen.getByTestId('metrics-tab-throughput')).toHaveTextContent('Throughput')
  expect(screen.getByTestId('metrics-tab-bugsBlocked')).toHaveTextContent('Bugs and blocked')
  expect(screen.getByTestId('metrics-tab-velocity')).toHaveTextContent('Team velocity')

  await waitFor(() => expect(fetchedChartPaths()).toEqual(['/metrics/cfd']))
  await waitFor(() => expect(seriesOf(lastOptionWithSeries())).toHaveLength(2))
  expect(screen.getByTestId('metrics-chart')).toBeInTheDocument()

  // CFD summary line: Max WIP / Ave WIP / Occured date from the summary DTO.
  await waitFor(() =>
    expect(screen.getByTestId('metrics-summary').textContent).toContain('Max WIP: 7'),
  )
  expect(screen.getByTestId('metrics-summary').textContent).toContain('Ave WIP: 4.5')
  expect(screen.getByTestId('metrics-summary').textContent).toContain('Occured date:')
})

it('tab click switches chart, fetches that endpoint, persists kanban.lastMetricChartType', async () => {
  installMetricHandlers()
  const user = userEvent.setup()
  renderPanel()

  await waitFor(() => expect(seriesOf(lastOptionWithSeries())).toHaveLength(2)) // CFD
  expect(localStorage.getItem('kanban.lastMetricChartType')).toBeNull() // untouched mount

  await user.click(screen.getByTestId('metrics-tab-throughput'))
  await waitFor(() => expect(fetchedChartPaths()).toContain('/metrics/throughput'))
  await waitFor(() => expect(seriesOf(lastOptionWithSeries())).toHaveLength(4))
  expect(seriesOf(lastOptionWithSeries())[0].type).toBe('bar')
  expect(localStorage.getItem('kanban.lastMetricChartType')).toBe('throughput')
  // Throughput summary: realAverage/Week and estimated weeks.
  await waitFor(() =>
    expect(screen.getByTestId('metrics-summary').textContent).toContain(
      'Average throughput:6/Week',
    ),
  )
  expect(screen.getByTestId('metrics-summary').textContent).toContain('2.5 weeks')

  await user.click(screen.getByTestId('metrics-tab-velocity'))
  await waitFor(() => expect(fetchedChartPaths()).toContain('/metrics/velocity'))
  await waitFor(() =>
    expect(seriesOf(lastOptionWithSeries()).some((s) => s.name === 'Story points')).toBe(true),
  )
  expect(localStorage.getItem('kanban.lastMetricChartType')).toBe('velocity')
  // Velocity summary goes through format0 on Metrics_Last6WeeksAveVelocity.
  await waitFor(() =>
    expect(screen.getByTestId('metrics-summary').textContent).toContain(
      'Average velocity for last 6 weeks: 9 / Two weeks',
    ),
  )
})

it('restores the last chart type from kanban.lastMetricChartType on mount', async () => {
  installMetricHandlers()
  localStorage.setItem('kanban.lastMetricChartType', 'velocity')
  renderPanel()

  await waitFor(() => expect(fetchedChartPaths()).toEqual(['/metrics/velocity']))
  await waitFor(() =>
    expect(seriesOf(lastOptionWithSeries()).some((s) => s.name === 'Story points')).toBe(true),
  )
})

it('unknown stored chart type falls back to the first chart (CFD)', async () => {
  installMetricHandlers()
  localStorage.setItem('kanban.lastMetricChartType', 'no-such-chart')
  renderPanel()

  await waitFor(() => expect(fetchedChartPaths()).toEqual(['/metrics/cfd']))
})

it('cycle-time chart shows the % bars, days line and average summary', async () => {
  installMetricHandlers()
  renderPanel()
  await screen.findByTestId('metrics-panel')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('metrics-tab-cycleTime'))
  await waitFor(() => expect(fetchedChartPaths()).toContain('/metrics/cycle-time'))
  await waitFor(() => expect(seriesOf(lastOptionWithSeries())).toHaveLength(3))
  const s = seriesOf(lastOptionWithSeries())
  expect(s.filter((x) => x.type === 'bar')).toHaveLength(2)
  expect(s.find((x) => x.type === 'line')).toBeTruthy()
  await waitFor(() =>
    expect(screen.getByTestId('metrics-summary').textContent).toContain(
      'Average cycle time: 3.5 Days',
    ),
  )
})

it('bugs & blocked chart renders both areas and cycle-time unknown estimate shows ∞', async () => {
  installMetricHandlers()
  renderPanel()
  await screen.findByTestId('metrics-panel')

  const user = userEvent.setup()
  await user.click(screen.getByTestId('metrics-tab-bugsBlocked'))
  await waitFor(() => expect(fetchedChartPaths()).toContain('/metrics/bugs-blocked'))
  await waitFor(() => expect(seriesOf(lastOptionWithSeries())).toHaveLength(2))
  expect(seriesOf(lastOptionWithSeries()).map((x) => x.name)).toEqual([
    'Bug fix items',
    'Blocked items',
  ])
})

it('estimated cycle time without a value renders Metrics_UnknownWeeks', async () => {
  installFetch({
    'GET /api/boards/b-1/metrics/throughput': () => ({
      ...THROUGHPUT,
      summary: { realAverageThroughputPerWeek: 0, estimatedCycleTimeWeeks: 0 },
    }),
  })
  localStorage.setItem('kanban.lastMetricChartType', 'throughput')
  renderPanel()

  await waitFor(() =>
    expect(screen.getByTestId('metrics-summary').textContent).toContain('∞ weeks'),
  )
})

it('metrics fetch failure renders the localized load error without crashing', async () => {
  installFetch({
    'GET /api/boards/b-1/metrics/cfd': () => new Response('boom', { status: 500 }),
  })
  renderPanel()

  expect(await screen.findByTestId('metrics-error')).toBeInTheDocument()
  expect(screen.getByTestId('metrics-error')).toHaveTextContent('Failed to load metrics')
})

// CARRY (R7/ledger): i18n runs with returnEmptyString:false, so an empty
// source key would render its RAW KEY on screen (see Metrics_FormatLabelDate).
// Prove every key this panel renders is present and non-empty in all three
// exported bundles — the trio is the single string source.
it('every Metrics_* key the panel renders is non-empty in en/ja/zh', () => {
  const usedKeys = [
    // Tabs
    'MetricChartType_CFD',
    'MetricChartType_CycleTimeDistribution',
    'MetricChartType_Throughput',
    'MetricChartType_BugsAndBlocked',
    'MetricChartType_TeamVelocity',
    // Error + summaries
    'Metrics_LoadError',
    'Metrics_MaxWIP',
    'Metrics_AveWIP',
    'Metrics_OccuredDate',
    'Metrics_AveCycleTime',
    'Metrics_FormatLabelDay',
    'Metrics_AveThroughput',
    'Metrics_PerWeek',
    'Metrics_EstimatedCycleTime',
    'Metrics_Weeks',
    'Metrics_UnknownWeeks',
    'Metrics_Last6WeeksAveVelocity',
    // Series/legend names handed to the builders
    'Metrics_ItemsLabel',
    'Metrics_EstimationMuch',
    'Metrics_EstimationProper',
    'Metrics_EstimationSmall',
    'Metrics_ItemsOfBugfix',
    'Metrics_ItemsOfBlocked',
    'Metrics_StoryPointsLabel',
    'Metrics_DistributionLabel',
  ]
  for (const [locale, bundle] of [
    ['en', en],
    ['ja', ja],
    ['zh', zh],
  ] as const) {
    const table = bundle as Record<string, string>
    for (const key of usedKeys) {
      expect(typeof table[key], `${locale}.${key} missing`).toBe('string')
      expect(table[key], `${locale}.${key} is empty`).not.toBe('')
    }
  }
})
