import { act, fireEvent, screen } from '@testing-library/react'
import i18n from 'i18next'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import type { CardDto } from '../api/types'
import { textColorFor } from '../cardType'
import { initI18n } from '../i18n'
import { renderWithProviders } from '../test/renderWithProviders'

import CardView, { releaseProgressRatio, workDays } from './CardView'

// Fixtures mirror what GET /api/boards/{id}/cards returns: CardDto fields are
// all optional and the int64 unions arrive as number | string.
function cardFixture(overrides: Partial<CardDto> = {}): CardDto {
  return {
    id: 'c-1',
    seqNo: 1,
    title: 'Login crash',
    cardType: 'BugFix',
    ...overrides,
  }
}

function renderCard(card: CardDto) {
  return renderWithProviders(<CardView card={card} onOpen={() => {}} />)
}

beforeEach(() => {
  void initI18n()
  void i18n.changeLanguage('en')
})

afterEach(() => {
  vi.useRealTimers()
  void i18n.changeLanguage('en')
})

// WPF parity (BoardPage.xaml card ToolTip ← Card.CardContentsInfo): hovering
// the card surfaces the three text fields the card face does not show.
function hoverCard(ms: number): void {
  fireEvent.mouseOver(screen.getByTestId('card'))
  act(() => {
    vi.advanceTimersByTime(ms)
  })
}

it('hovering the card opens a tooltip rendering summary, testCases and designOverview as HTML', () => {
  vi.useFakeTimers()
  renderCard(
    cardFixture({
      summary: '<b>crash</b> on login',
      testCases: '<ul><li>TC1</li></ul>',
      designOverview: '<p>Use <i>guard</i></p>',
    }),
  )

  hoverCard(2000)

  const tooltip = screen.getByRole('tooltip')
  expect(tooltip).toHaveTextContent('Description：')
  expect(tooltip).toHaveTextContent('Test cases：')
  expect(tooltip).toHaveTextContent('Design overview :')
  // Sanitization: none — same accepted trust model as the card-face details.
  expect(tooltip.querySelector('[data-testid="hover-summary"]')?.innerHTML).toContain('<b>crash</b>')
  expect(tooltip.querySelector('[data-testid="hover-test-cases"]')?.innerHTML).toContain('<li>TC1</li>')
  expect(tooltip.querySelector('[data-testid="hover-design-overview"]')?.innerHTML).toContain('<i>guard</i>')
})

it('BugFix card gets the WPF red background (#FF0000) with white text', () => {
  renderCard(cardFixture())
  const card = screen.getByTestId('card')
  expect(card).toHaveStyle({
    backgroundColor: 'rgb(255, 0, 0)',
    color: 'rgb(255, 255, 255)',
  })
  // Title renders, plus the English type label from CardType.cs verbatim.
  expect(card).toHaveTextContent('Login crash')
  expect(screen.getByTestId('card-type-label')).toHaveTextContent('Bug fix')
})

it('blocked card shows the block chip; unblocked card does not', () => {
  const blocked = renderCard(cardFixture({ isBlocked: true }))
  expect(screen.getByTestId('blocked-chip')).toHaveTextContent('Blocked')
  blocked.unmount()

  renderCard(cardFixture({ isBlocked: false }))
  expect(screen.queryByTestId('blocked-chip')).not.toBeInTheDocument()
})

it('storyPoints badge hidden for 0 (number or string), shown when > 0', () => {
  const zero = renderCard(cardFixture({ storyPoints: 0 }))
  expect(screen.queryByTestId('story-points-badge')).not.toBeInTheDocument()
  zero.unmount()

  const zeroStr = renderCard(cardFixture({ storyPoints: '0' }))
  expect(screen.queryByTestId('story-points-badge')).not.toBeInTheDocument()
  zeroStr.unmount()

  const three = renderCard(cardFixture({ storyPoints: 3 }))
  expect(screen.getByTestId('story-points-badge')).toHaveTextContent('3')
  three.unmount()

  // int64 union: "8" must display as 8.
  const eight = renderCard(cardFixture({ storyPoints: '8' }))
  expect(screen.getByTestId('story-points-badge')).toHaveTextContent('8')
  eight.unmount()
})

it('hover tooltip omits sections for fields left empty', () => {
  vi.useFakeTimers()
  renderCard(cardFixture({ testCases: '<ul><li>TC1</li></ul>' }))

  hoverCard(2000)

  const tooltip = screen.getByRole('tooltip')
  expect(tooltip).toHaveTextContent('Test cases：')
  expect(tooltip).not.toHaveTextContent('Description：')
  expect(tooltip).not.toHaveTextContent('Design overview :')
})

it('no tooltip appears on hover when all three hover fields are empty', () => {
  vi.useFakeTimers()
  renderCard(cardFixture())

  hoverCard(2000)

  expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()
})

it('disableHoverInfo suppresses the hover tooltip (drag in progress)', () => {
  vi.useFakeTimers()
  renderWithProviders(
    <CardView card={cardFixture({ summary: '<b>crash</b> on login' })} onOpen={() => {}} disableHoverInfo />,
  )

  hoverCard(2000)

  expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()
})

it('hovering the worked-days progress bar shows only its own tooltip', () => {
  vi.useFakeTimers()
  renderCard(
    cardFixture({
      summary: '<b>crash</b> on login',
      isScheduledToRelease: true,
      createDate: '2026-01-05T00:00:00',
      stateChangedDate: '2026-01-09T00:00:00',
      releaseDate: '2026-01-16T00:00:00',
    }),
  )

  fireEvent.mouseOver(screen.getByTestId('progress-bar'))
  act(() => {
    vi.advanceTimersByTime(2000)
  })

  // The bar's own Worked Days label, not the card contents balloon.
  expect(screen.getByRole('tooltip')).toHaveTextContent('Worked Days')
  expect(screen.queryByTestId('hover-summary')).not.toBeInTheDocument()
})

it('type label localizes under ja (CardType.cs Description, verbatim)', async () => {
  await i18n.changeLanguage('ja')
  renderCard(cardFixture())
  expect(screen.getByTestId('card-type-label')).toHaveTextContent('バグ改修')
})

it('progress bar renders only when isScheduledToRelease', () => {
  const plain = renderCard(cardFixture())
  expect(screen.queryByTestId('progress-bar')).not.toBeInTheDocument()
  plain.unmount()

  const notScheduled = renderCard(cardFixture({ isScheduledToRelease: false }))
  expect(screen.queryByTestId('progress-bar')).not.toBeInTheDocument()
  notScheduled.unmount()

  renderCard(
    cardFixture({
      isScheduledToRelease: true,
      createDate: '2026-01-05T00:00:00',
      stateChangedDate: '2026-01-09T00:00:00',
      releaseDate: '2026-01-16T00:00:00',
    }),
  )
  expect(screen.getByTestId('progress-bar')).toBeInTheDocument()
})

it('clicking the card calls onOpen', () => {
  const onOpen = vi.fn()
  renderWithProviders(<CardView card={cardFixture()} onOpen={onOpen} />)
  fireEvent.click(screen.getByTestId('card'))
  expect(onOpen).toHaveBeenCalledTimes(1)
})

it('merge chips render per flag', () => {
  const plain = renderCard(cardFixture())
  expect(screen.queryByTestId('merged-master-chip')).not.toBeInTheDocument()
  expect(screen.queryByTestId('merged-major-chip')).not.toBeInTheDocument()
  plain.unmount()

  const master = renderCard(cardFixture({ isMergedIntoMaster: true }))
  expect(screen.getByTestId('merged-master-chip')).toHaveTextContent('Merged into master')
  expect(screen.queryByTestId('merged-major-chip')).not.toBeInTheDocument()
  master.unmount()

  const major = renderCard(cardFixture({ isMergedIntoMajorBranch: true }))
  expect(screen.getByTestId('merged-major-chip')).toHaveTextContent('Merged into major branch')
  major.unmount()
})

it('tag and executors render only when present', () => {
  const plain = renderCard(cardFixture())
  expect(screen.queryByTestId('card-tag')).not.toBeInTheDocument()
  expect(screen.queryByTestId('card-executors')).not.toBeInTheDocument()
  plain.unmount()

  renderCard(cardFixture({ tag: 'auth', executors: 'Ann;Bob' }))
  // Card_TagFormat resx key ("Tag：{0}") via format0.
  expect(screen.getByTestId('card-tag')).toHaveTextContent('Tag：auth')
  expect(screen.getByTestId('card-executors')).toHaveTextContent('Ann;Bob')
})

it('textColorFor picks readable text for the WPF palette', () => {
  // Pinned by the brief (CardTypePresentation.cs Green / Yellow).
  expect(textColorFor('#008000')).toBe('#fff')
  expect(textColorFor('#FFFF00')).toBe('#000')
  // Rest of the table: dark colors → white text, light colors → black text.
  expect(textColorFor('#FF0000')).toBe('#fff') // BugFix Red
  expect(textColorFor('#00FF00')).toBe('#000') // Improvement Lime
  expect(textColorFor('#FFB6C1')).toBe('#000') // DefectInvestigation LightPink
  expect(textColorFor('#A9A9A9')).toBe('#000') // Cancelled DarkGray
  expect(textColorFor('#FFEFD5')).toBe('#000') // Evaluation PapayaWhip
  expect(textColorFor('#F5FFFA')).toBe('#000') // Task MintCream
})

it('workDays matches DateTimeUtil.WorkDays: both ends inclusive, weekends skipped', () => {
  // 2026-01-05 is a Monday, 2026-01-09 a Friday: five working days.
  expect(workDays('2026-01-05T00:00:00', '2026-01-09T00:00:00')).toBe(5)
  // A pure weekend counts nothing (DateTimeUtil.cs:24-28 — Sat/Sun only,
  // no holiday list in the C#).
  expect(workDays('2026-01-10T00:00:00', '2026-01-11T00:00:00')).toBe(0)
  // to < from → the C# foreach never runs → 0.
  expect(workDays('2026-01-09T00:00:00', '2026-01-05T00:00:00')).toBe(0)
})

it('releaseProgressRatio = elapsed create→stateChanged vs stateChanged→release, clamped', () => {
  // Mon 01-05 → Fri 01-09: 5 workdays elapsed; Fri 01-09 → Fri 01-16: 6
  // workdays remaining (stateChangedDate counted once per leg) → 5/11.
  expect(
    releaseProgressRatio(
      '2026-01-05T00:00:00',
      '2026-01-09T00:00:00',
      '2026-01-16T00:00:00',
    ),
  ).toBeCloseTo(5 / 11, 5)

  // Past release date: everything elapsed, clamped to 1.
  expect(
    releaseProgressRatio(
      '2026-01-05T00:00:00',
      '2026-01-23T00:00:00',
      '2026-01-16T00:00:00',
    ),
  ).toBe(1)

  // No days counted at all (all-weekend window) → 0, never NaN.
  expect(
    releaseProgressRatio(
      '2026-01-10T00:00:00',
      '2026-01-10T00:00:00',
      '2026-01-11T00:00:00',
    ),
  ).toBe(0)

  // Missing/invalid dates → 0.
  expect(releaseProgressRatio(undefined, undefined, undefined)).toBe(0)
  expect(releaseProgressRatio('nope', '2026-01-09T00:00:00', '2026-01-16T00:00:00')).toBe(0)
})
