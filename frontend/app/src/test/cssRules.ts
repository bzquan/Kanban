// Test-only helper: jsdom's getComputedStyle silently drops some declarations
// inside emotion's autoprefixed rules (e.g. flex-direction / flex-grow sitting
// behind -webkit- duplicates), so layout contracts are asserted against the
// injected stylesheet text for the element's emotion classes instead.
export function cssRulesFor(el: HTMLElement): string {
  const classes = Array.from(el.classList).filter((c) => c !== '')
  let out = ''
  el.ownerDocument.querySelectorAll('style').forEach((s) => {
    const t = s.textContent ?? ''
    for (const c of classes) {
      if (t.includes(`.${c}`)) out += t
    }
  })
  return out
}
