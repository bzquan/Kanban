/**
 * Substitute the first .NET-style positional placeholder ({0}) in `template`
 * with `value`. resx strings are .NET format strings ("Name：{0} "); i18next
 * only interpolates {{var}}, so positional ones get substituted here.
 *
 * The replacer function is load-bearing: String.replace interprets $-sequences
 * ($&, $', $`, $$) in a *replacement string*, which would corrupt user text
 * containing them. A function replacement inserts the value verbatim.
 */
export function format0(template: string, value: string): string {
  return template.replace('{0}', () => value)
}
