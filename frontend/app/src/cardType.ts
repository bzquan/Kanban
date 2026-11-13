/**
 * Card-type presentation data ported from the WPF client — values copied,
 * never invented:
 *
 *  - Backgrounds: `Kanban/src/ViewModel/CardTypePresentation.cs`
 *    (`CardTypeView` switch, lines 17–58): the System.Windows.Media.Colors
 *    name mapped to its #RRGGBB value, default branch = Colors.Green.
 *  - Labels: `Kanban.Domain/Repository/CardType.cs` `[Description(...)]`
 *    values copied verbatim — including the source's
 *    "English:Investigate deffect" typo (parity with WPF; fix the C# enum
 *    and regenerate rather than silently correcting here).
 *
 * The API sends `cardType` as the enum name string
 * (`Kanban.Api/Contracts/CardDto.cs:56` — `card.CardType.ToString()`), so
 * both maps are keyed by the enum name.
 */

export const CARD_TYPE_BACKGROUND: Record<string, string> = {
  Feature: '#008000', // Colors.Green
  Improvement: '#00FF00', // Colors.Lime
  BugFix: '#FF0000', // Colors.Red
  DefectInvestigation: '#FFB6C1', // Colors.LightPink
  TechnicalDebt: '#FFFF00', // Colors.Yellow
  Cancelled: '#A9A9A9', // Colors.DarkGray
  Evaluation: '#FFEFD5', // Colors.PapayaWhip
  TestDesign: '#FFA500', // Colors.Orange
  Task: '#F5FFFA', // Colors.MintCream
}

/** `CardTypeView` default branch (CardTypePresentation.cs:55-58): Green. */
export const CARD_TYPE_BACKGROUND_DEFAULT = '#008000'

/** `[Description("English:…|日本語:…|中文:…")]` from CardType.cs, verbatim. */
export const CARD_TYPE_LABELS: Record<string, { en: string; ja: string; zh: string }> = {
  Feature: { en: 'Feature', ja: '新規機能', zh: '新功能' },
  Improvement: { en: 'Improvement', ja: '改善', zh: '改进' },
  BugFix: { en: 'Bug fix', ja: 'バグ改修', zh: '修改Bug' },
  DefectInvestigation: { en: 'Investigate deffect', ja: '障害調査', zh: '调查故障' },
  TechnicalDebt: { en: 'Technical debt', ja: '技術債', zh: '技术债' },
  Cancelled: { en: 'Cancelled item', ja: '取消', zh: '被取消的条目' },
  Evaluation: { en: 'Evaluation', ja: '評価', zh: '评估' },
  TestDesign: { en: 'Test design', ja: '試験設計', zh: '测试设计' },
  Task: { en: 'Task', ja: 'タスク', zh: '细条目' },
}

/** Color for the `cardType` enum name; unknown/missing → WPF default Green. */
export function cardTypeBackground(cardType: string | undefined): string {
  if (cardType === undefined) return CARD_TYPE_BACKGROUND_DEFAULT
  return CARD_TYPE_BACKGROUND[cardType] ?? CARD_TYPE_BACKGROUND_DEFAULT
}

function parseHexColor(hex: string): { r: number; g: number; b: number } | null {
  const m = /^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.exec(hex.trim())
  if (!m) return null
  const digits = m[1]
  const full =
    digits.length === 3
      ? digits
          .split('')
          .map((c) => c + c)
          .join('')
      : digits
  return {
    r: Number.parseInt(full.slice(0, 2), 16),
    g: Number.parseInt(full.slice(2, 4), 16),
    b: Number.parseInt(full.slice(4, 6), 16),
  }
}

/**
 * Readable foreground for a background tint: ITU-R BT.601 luma
 * (0.299R + 0.587G + 0.114B)/255 below 0.55 → white, otherwise black.
 * Anchors: '#008000' → '#fff', '#FFFF00' → '#000'. Non-hex input → '#000'
 * (the helper is only meant for table hex values; black is the safe
 * fallback on the light defaults).
 */
export function textColorFor(hex: string): string {
  const rgb = parseHexColor(hex)
  if (!rgb) return '#000'
  const luma = (0.299 * rgb.r + 0.587 * rgb.g + 0.114 * rgb.b) / 255
  return luma < 0.55 ? '#fff' : '#000'
}
