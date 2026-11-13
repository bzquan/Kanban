// Task 10 Step 1: pure-option builder tests. Every builder must be a pure
// (dto → plain object) function: no echarts instance, no i18n, no DOM.
// Review Focus #5 is a hard gate: empty/all-zero fixtures return an option
// object with `series: []` and never throw.
import { describe, expect, it } from 'vitest'

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
  velocityOption,
} from './options'

// EChartsOption's union-heavy typing makes assertions verbose; peek through
// `any` here — the *shape* is what these tests pin, the compiler checks the
// builders themselves.
type AnyOption = {
  series: Record<string, unknown>[]
  xAxis: { data: unknown[] }
  yAxis: unknown
}

function view(option: object): AnyOption {
  return option as unknown as AnyOption
}

// --- fixtures -------------------------------------------------------------

const cfd: CfdDto = {
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

const cycleTime: CycleTimeDto = {
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
        { stepName: 'Backlog', percentage: '50' },
        { stepName: 'Development', percentage: 50 },
      ],
    },
  ],
  averageCycleTimeDays: [3, '4'],
  weeklyAverageCycleTimeDays: 3.5,
}

const throughput: ThroughputDto = {
  weekEnds: ['2026-11-07', '2026-11-14'],
  itemCounts: [5, '7'],
  performance: [
    { aheadOfScheduleCount: 1, onTimeCount: 3, delayedCount: 1 },
    { aheadOfScheduleCount: '2', onTimeCount: 4, delayedCount: 1 },
  ],
  summary: { realAverageThroughputPerWeek: 6, estimatedCycleTimeWeeks: 2.5 },
}

const bugsBlocked: BugsBlockedDto = {
  days: ['2026-11-01', '2026-11-02'],
  bugFixCounts: [1, '2'],
  blockedCounts: [0, 3],
}

const velocity: VelocityDto = {
  weekEnds: ['2026-11-07', '2026-11-14'],
  storyPoints: ['8', 10],
  hasUnknownStoryPoints: [false, true],
  averageVelocity4Every2Weeks: 9,
}

// --- CFD ------------------------------------------------------------------

describe('cfdOption', () => {
  it('stacked area line per step over days, legend + axis tooltip', () => {
    const opt = view(cfdOption(cfd))
    expect(opt.xAxis.data).toEqual(cfd.days)
    expect(opt.series).toHaveLength(2)
    expect(opt.series[0]).toMatchObject({
      name: 'Backlog',
      type: 'line',
      stack: expect.any(String),
      areaStyle: expect.any(Object),
      data: [2, 3, 1], // intVal converts the '3' string
    })
    expect(opt.series[1].data).toEqual([3, 2, 4])
    expect(opt.series[1].stack).toBe(opt.series[0].stack) // one shared stack
    const raw = cfdOption(cfd) as Record<string, unknown>
    expect(raw.tooltip).toBeTruthy()
    expect(raw.legend).toBeTruthy()
  })

  it('empty and all-zero fixtures: option without throwing, series []', () => {
    expect(cfdOption({ days: [], series: [] }).series).toEqual([])
    expect(cfdOption({}).series).toEqual([])
    // all-zero keeps the series but must not throw
    const zero = cfdOption({
      days: ['2026-11-01'],
      series: [{ stepName: 'Backlog', cumulativeFlowCounts: [0] }],
    })
    expect(view(zero).series).toHaveLength(1)
    expect(view(zero).series[0].data).toEqual([0])
  })
})

// --- Cycle time distribution ----------------------------------------------

describe('cycleTimeOption', () => {
  it('stacked % bars per step + average-days line overlay on x=weekEnds', () => {
    const opt = view(cycleTimeOption(cycleTime))
    expect(opt.xAxis.data).toEqual(cycleTime.weekEnds)
    const bars = opt.series.filter((s) => s.type === 'bar')
    expect(bars).toHaveLength(2)
    expect(bars[0]).toMatchObject({
      name: 'Backlog',
      stack: expect.any(String),
      data: [30, 50],
    })
    expect(bars[1].data).toEqual([70, 50])
    const line = opt.series.find((s) => s.type === 'line')
    expect(line).toBeTruthy()
    expect(line?.data).toEqual([3, 4])
    expect(line?.yAxisIndex).toBe(1) // days on the second axis, % on the first
    expect(Array.isArray(opt.yAxis)).toBe(true)
  })

  it('honors localized series names', () => {
    const opt = view(
      cycleTimeOption(cycleTime, { average: '平均サイクルタイム: ', distribution: '分布(%)' }),
    )
    const line = opt.series.find((s) => s.type === 'line')
    expect(line?.name).toBe('平均サイクルタイム: ')
    expect((opt.yAxis as { name?: string }[])[0].name).toBe('分布(%)')
  })

  it('empty and all-zero fixtures: option without throwing, series []', () => {
    expect(cycleTimeOption({}).series).toEqual([])
    expect(
      cycleTimeOption({ weekEnds: [], weeks: [], averageCycleTimeDays: [] }).series,
    ).toEqual([])
    const zero = cycleTimeOption({
      weekEnds: ['2026-11-07'],
      weeks: [{ buckets: [{ stepName: 'Backlog', percentage: 0 }] }],
      averageCycleTimeDays: [0],
    })
    expect(view(zero).series).toHaveLength(2) // one % bar + one line
  })
})

// --- Throughput ------------------------------------------------------------

describe('throughputOption', () => {
  it('item bar + ahead/onTime/delayed performance stacks on x=weekEnds', () => {
    const opt = view(throughputOption(throughput))
    expect(opt.xAxis.data).toEqual(throughput.weekEnds)
    expect(opt.series).toHaveLength(4)
    expect(opt.series[0]).toMatchObject({ type: 'bar', data: [5, 7] })
    const stacks = opt.series.slice(1)
    expect(stacks.map((s) => s.type)).toEqual(['bar', 'bar', 'bar'])
    expect(stacks.every((s) => s.stack === stacks[0].stack)).toBe(true)
    expect(stacks.map((s) => s.data)).toEqual([
      [1, 2],
      [3, 4],
      [1, 1],
    ])
  })

  it('honors localized series names', () => {
    const opt = view(
      throughputOption(throughput, { items: '項目数', delayed: '▽Estimation too small: ' }),
    )
    expect(opt.series[0].name).toBe('項目数')
    expect(opt.series[3].name).toBe('▽Estimation too small: ')
  })

  it('empty and all-zero fixtures: option without throwing, series []', () => {
    expect(throughputOption({}).series).toEqual([])
    expect(
      throughputOption({ weekEnds: [], itemCounts: [], performance: [] }).series,
    ).toEqual([])
    const zero = throughputOption({
      weekEnds: ['2026-11-07'],
      itemCounts: [0],
      performance: [{ aheadOfScheduleCount: 0, onTimeCount: 0, delayedCount: 0 }],
    })
    expect(view(zero).series).toHaveLength(4)
  })
})

// --- Bugs & blocked ---------------------------------------------------------

describe('bugsBlockedOption', () => {
  it('two area series over days', () => {
    const opt = view(bugsBlockedOption(bugsBlocked))
    expect(opt.xAxis.data).toEqual(bugsBlocked.days)
    expect(opt.series).toHaveLength(2)
    expect(opt.series[0]).toMatchObject({
      type: 'line',
      areaStyle: expect.any(Object),
      data: [1, 2],
    })
    expect(opt.series[1].data).toEqual([0, 3])
  })

  it('honors localized series names', () => {
    const opt = view(
      bugsBlockedOption(bugsBlocked, { bugFix: '障害対応項目数', blocked: '不順項目数' }),
    )
    expect(opt.series.map((s) => s.name)).toEqual(['障害対応項目数', '不順項目数'])
  })

  it('empty and all-zero fixtures: option without throwing, series []', () => {
    expect(bugsBlockedOption({}).series).toEqual([])
    expect(
      bugsBlockedOption({ days: [], bugFixCounts: [], blockedCounts: [] }).series,
    ).toEqual([])
    const zero = bugsBlockedOption({ days: ['2026-11-01'], bugFixCounts: [0], blockedCounts: [0] })
    expect(view(zero).series).toHaveLength(2)
  })
})

// --- Team velocity -----------------------------------------------------------

describe('velocityOption', () => {
  it('story-points line over weekEnds; unknown-SP weeks get a red ! mark', () => {
    const opt = view(velocityOption(velocity))
    expect(opt.xAxis.data).toEqual(velocity.weekEnds)
    expect(opt.series).toHaveLength(1)
    expect(opt.series[0].type).toBe('line')
    const data = opt.series[0].data as unknown[]
    expect(data[0]).toBe(8) // known → plain number (string coerced)
    const marked = data[1] as {
      value: number
      itemStyle: { color: string }
      label: { formatter: string }
    }
    expect(marked.value).toBe(10)
    expect(marked.itemStyle.color).toMatch(/^#/) // red mark
    expect(marked.label.formatter).toBe('!')
  })

  it('honors a localized series name', () => {
    const opt = view(velocityOption(velocity, { storyPoints: 'ストーリーポイント' }))
    expect(opt.series[0].name).toBe('ストーリーポイント')
  })

  it('missing hasUnknownStoryPoints behaves like all-known', () => {
    const opt = view(velocityOption({ weekEnds: ['w'], storyPoints: ['5'] }))
    expect(view(opt).series[0].data).toEqual([5])
  })

  it('empty and all-zero fixtures: option without throwing, series []', () => {
    expect(velocityOption({}).series).toEqual([])
    expect(
      velocityOption({ weekEnds: [], storyPoints: [], hasUnknownStoryPoints: [] }).series,
    ).toEqual([])
    const zero = velocityOption({ weekEnds: ['2026-11-07'], storyPoints: [0] })
    expect(view(zero).series).toHaveLength(1)
  })
})
