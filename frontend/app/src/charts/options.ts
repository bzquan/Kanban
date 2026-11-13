// Task 10: pure ECharts option builders for the 5 metrics charts + the tiny
// render hook that binds an option to a DOM node.
//
// Each builder is a pure `Dto -> plain option object` function: no echarts
// instance, no i18n, no DOM. Localizable series names enter through the
// optional `names` parameter (MetricsPanel passes t() values); the defaults
// are the en resx texts so the builders stay usable (and testable) alone.
//
// Review Focus #5 (hard gate): every builder tolerates missing/empty DTO
// fields and returns `series: []` instead of throwing — the API returns the
// DTOs with every field optional.
import * as echarts from 'echarts'
import type { EChartsOption } from 'echarts'
import { useEffect, useRef } from 'react'
import type { RefObject } from 'react'

import { intVal } from '../api/client'
import type {
  BugsBlockedDto,
  CfdDto,
  CycleTimeDto,
  ThroughputDto,
  VelocityDto,
} from '../api/types'

/** (number | string)[] -> number[]; missing array -> []. NaN/undefined -> 0 via intVal. */
function nums(values: (number | string)[] | undefined): number[] {
  return (values ?? []).map((v) => intVal(v))
}

/** Color of the velocity "unknown story points" mark (MUI error.main). */
const UNKNOWN_MARK_COLOR = '#d32f2f'

/**
 * CFD: one stacked-area line per process step over `days`. WPF parity with
 * VisualCFDChart: cumulative flows stacked bottom-up, legend + axis tooltip.
 */
export function cfdOption(d: CfdDto): EChartsOption {
  return {
    tooltip: { trigger: 'axis' },
    legend: {},
    xAxis: { type: 'category', data: d.days ?? [] },
    yAxis: { type: 'value' },
    series: (d.series ?? [])
      .filter((s) => (s.cumulativeFlowCounts?.length ?? 0) > 0)
      .map((s) => ({
        name: s.stepName ?? '',
        type: 'line' as const,
        stack: 'cfd',
        areaStyle: {},
        data: nums(s.cumulativeFlowCounts),
      })),
  }
}

/** Localizable labels cycleTimeOption stamps onto its series/axes. */
export interface CycleTimeNames {
  /** Legend name of the average-days line overlay (en: Metrics_AveCycleTime). */
  average?: string
  /** Name of the percentage y-axis (en: Metrics_DistributionLabel). */
  distribution?: string
}

/**
 * Cycle-time distribution: stacked % bars per step per week (x = weekEnds)
 * plus the averageCycleTimeDays line overlay on a second y-axis (days).
 */
export function cycleTimeOption(d: CycleTimeDto, names: CycleTimeNames = {}): EChartsOption {
  const weeks = d.weeks ?? []
  // Step order = first appearance across the weeks' buckets (stable legend).
  const stepNames: string[] = []
  for (const week of weeks) {
    for (const bucket of week.buckets ?? []) {
      const name = bucket.stepName ?? ''
      if (!stepNames.includes(name)) stepNames.push(name)
    }
  }
  const series: EChartsOption['series'] = stepNames.map((stepName) => ({
    name: stepName,
    type: 'bar' as const,
    stack: 'distribution',
    data: weeks.map(
      (week) =>
        intVal((week.buckets ?? []).find((b) => (b.stepName ?? '') === stepName)?.percentage),
    ),
  }))
  const averages = nums(d.averageCycleTimeDays)
  if (averages.length > 0) {
    // Push after the bars so the line renders on top of the stacks.
    ;(series as unknown as unknown[]).push({
      name: names.average ?? 'Average cycle time',
      type: 'line',
      yAxisIndex: 1, // days live on the second axis
      symbol: 'circle',
      data: averages,
    })
  }
  return {
    tooltip: { trigger: 'axis' },
    legend: {},
    xAxis: { type: 'category', data: d.weekEnds ?? [] },
    yAxis: [
      { type: 'value', name: names.distribution ?? 'Distribution(%)', max: 100 },
      { type: 'value' },
    ],
    series,
  }
}

/** Localizable labels throughputOption stamps onto its series. */
export interface ThroughputNames {
  /** Completed-items bar (en: Metrics_ItemsLabel). */
  items?: string
  /** aheadOfScheduleCount stack (en: Metrics_EstimationMuch). */
  ahead?: string
  /** onTimeCount stack (en: Metrics_EstimationProper). */
  onTime?: string
  /** delayedCount stack (en: Metrics_EstimationSmall). */
  delayed?: string
}

/**
 * Throughput: the weekly completed-items bar plus the performance split
 * (ahead / on time / delayed) stacked beside it — WPF VisualThroughput maps
 * ahead→"Estimation too much", onTime→"proper", delayed→"too small".
 */
export function throughputOption(d: ThroughputDto, names: ThroughputNames = {}): EChartsOption {
  const series: unknown[] = []
  const itemCounts = nums(d.itemCounts)
  if (itemCounts.length > 0) {
    series.push({ name: names.items ?? 'Items', type: 'bar', data: itemCounts })
  }
  const performance = d.performance ?? []
  if (performance.length > 0) {
    const stacks: readonly [keyof NonNullable<ThroughputDto['performance']>[number], string][] = [
      ['aheadOfScheduleCount', names.ahead ?? 'Estimation too much'],
      ['onTimeCount', names.onTime ?? 'Estimation proper'],
      ['delayedCount', names.delayed ?? 'Estimation too small'],
    ]
    for (const [field, name] of stacks) {
      series.push({
        name,
        type: 'bar',
        // One shared stack: the 3 performance bars stack together (the
        // itemCounts total bar above them stays unstacked).
        stack: 'performance',
        data: performance.map((p) => intVal(p[field])),
      })
    }
  }
  return {
    tooltip: { trigger: 'axis' },
    legend: {},
    xAxis: { type: 'category', data: d.weekEnds ?? [] },
    yAxis: { type: 'value' },
    series: series as EChartsOption['series'],
  }
}

/** Localizable labels bugsBlockedOption stamps onto its two areas. */
export interface BugsBlockedNames {
  bugFix?: string
  blocked?: string
}

/** Bugs & blocked: two independent area lines over `days`. */
export function bugsBlockedOption(
  d: BugsBlockedDto,
  names: BugsBlockedNames = {},
): EChartsOption {
  const series: unknown[] = []
  const bugFix = nums(d.bugFixCounts)
  if (bugFix.length > 0) {
    series.push({
      name: names.bugFix ?? 'Bug fix items',
      type: 'line',
      areaStyle: {},
      data: bugFix,
    })
  }
  const blocked = nums(d.blockedCounts)
  if (blocked.length > 0) {
    series.push({
      name: names.blocked ?? 'Blocked items',
      type: 'line',
      areaStyle: {},
      data: blocked,
    })
  }
  return {
    tooltip: { trigger: 'axis' },
    legend: {},
    xAxis: { type: 'category', data: d.days ?? [] },
    yAxis: { type: 'value' },
    series: series as EChartsOption['series'],
  }
}

/** Localizable labels velocityOption stamps onto its line. */
export interface VelocityNames {
  storyPoints?: string
}

/**
 * Team velocity: story points line over weekEnds. Weeks flagged
 * hasUnknownStoryPoints (cards without SP) get a red "!" data-point mark —
 * WPF draws those weeks' points hollow/flagged.
 */
export function velocityOption(d: VelocityDto, names: VelocityNames = {}): EChartsOption {
  const points = d.storyPoints ?? []
  const unknown = d.hasUnknownStoryPoints ?? []
  const series: unknown[] = []
  if (points.length > 0) {
    series.push({
      name: names.storyPoints ?? 'Story points',
      type: 'line',
      data: points.map((v, i) =>
        unknown[i]
          ? {
              value: intVal(v),
              itemStyle: { color: UNKNOWN_MARK_COLOR },
              label: { show: true, formatter: '!', position: 'top', color: UNKNOWN_MARK_COLOR },
            }
          : intVal(v),
      ),
    })
  }
  return {
    tooltip: { trigger: 'axis' },
    legend: {},
    xAxis: { type: 'category', data: d.weekEnds ?? [] },
    yAxis: { type: 'value' },
    series: series as EChartsOption['series'],
  }
}

/**
 * Bind an ECharts option to a DOM node: `init` once per node, `setOption`
 * (notMerge) whenever the option changes, `dispose` on unmount, `resize` on
 * window resize. Tests stub this module's useECharts to capture options
 * (jsdom has no canvas for echarts).
 */
export function useECharts(ref: RefObject<HTMLElement | null>, option: EChartsOption): void {
  const chartRef = useRef<ReturnType<typeof echarts.init> | null>(null)
  // Init/dispose are tied to the node identity (ref is a stable object); the
  // option flows through the second effect on every change.
  useEffect(() => {
    const node = ref.current
    if (!node) return
    const chart = echarts.init(node)
    chartRef.current = chart
    const onResize = (): void => chart.resize()
    window.addEventListener('resize', onResize)
    return () => {
      window.removeEventListener('resize', onResize)
      chartRef.current = null
      chart.dispose()
    }
  }, [ref])
  useEffect(() => {
    chartRef.current?.setOption(option, true)
  }, [option])
}
