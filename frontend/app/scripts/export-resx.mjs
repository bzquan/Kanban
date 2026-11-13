// Export the WPF client's three .resx files to flat JSON bundles for i18next.
//   Kanban/src/View/Properties/Resources{,.ja-JP,.zh-CN}.resx
//     -> src/locales/{en,ja,zh}.json   (flat { "key": "text" }, one `ui` namespace)
//
// Regex-based on purpose: resx here is machine-generated, UTF-8, string-only
// entries shaped as <data name="key" xml:space="preserve"><value>text</value></data>.
// XML comments (including the ResX schema header's sample <data> entries) are
// stripped before parsing; XML entities are decoded. Typed/mimetype entries are
// skipped defensively — none exist in the real data today.
//
// Re-runnable and lossless: the app title comes from the existing resx key
// `Main_Title` (App.tsx uses t('Main_Title')), so no hand-added keys live in
// these JSONs — regeneration never drops anything.

import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const appRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = resolve(appRoot, '..', '..')
const resxDir = join(repoRoot, 'Kanban', 'src', 'View', 'Properties')
const outDir = join(appRoot, 'src', 'locales')

const LOCALES = [
  { out: 'en', file: 'Resources.resx' },
  { out: 'ja', file: 'Resources.ja-JP.resx' },
  { out: 'zh', file: 'Resources.zh-CN.resx' },
]

function decodeEntities(text) {
  return text
    .replace(/&#x([0-9a-fA-F]+);/g, (_, hex) => String.fromCodePoint(Number.parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, dec) => String.fromCodePoint(Number(dec)))
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&') // last: the decoded & must not re-trigger the others
}

export function parseResx(raw) {
  const withoutComments = raw.replace(/<!--[\s\S]*?-->/g, '')
  const entries = {}
  const dataRe = /<data name="([^"]+)"[^>]*>([\s\S]*?)<\/data>/g
  for (const match of withoutComments.matchAll(dataRe)) {
    const [, name, body] = match
    if (/\s(?:type|mimetype)=/.test(match[0])) continue
    const valueMatch = /<value(?:\s[^>]*)?>([\s\S]*?)<\/value>/.exec(body)
    let value
    if (valueMatch) {
      value = decodeEntities(valueMatch[1])
    } else if (/<value\s*\/>/.test(body)) {
      value = '' // <value /> — legitimately empty (e.g. Metrics_FormatLabelDate in en)
    } else {
      throw new Error(`resx entry "${name}" has no parsable <value>`)
    }
    if (name in entries) throw new Error(`duplicate resx entry "${name}"`)
    entries[name] = value
  }
  return entries
}

async function exportLocale({ out, file }) {
  const raw = await readFile(join(resxDir, file), 'utf8')
  const entries = parseResx(raw)
  const sorted = Object.fromEntries(
    Object.entries(entries).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)),
  )
  await writeFile(join(outDir, `${out}.json`), `${JSON.stringify(sorted, null, 2)}\n`, 'utf8')
  return Object.keys(sorted).length
}

await mkdir(outDir, { recursive: true })

const counts = {}
for (const locale of LOCALES) {
  counts[locale.out] = await exportLocale(locale)
  console.log(`${locale.out}: ${counts[locale.out]} keys -> src/locales/${locale.out}.json`)
}

// Sanity: all locales must cover exactly the same keys as en, and en — the
// fallback language — should carry no empty values (Metrics_FormatLabelDate is
// empty in the source resx by design; flag, do not fail, so the export stays usable).
const enKeys = Object.keys(JSON.parse(await readFile(join(outDir, 'en.json'), 'utf8')))
const enSet = new Set(enKeys)
let failed = false
for (const locale of LOCALES) {
  const keys = Object.keys(JSON.parse(await readFile(join(outDir, `${locale.out}.json`), 'utf8')))
  const missing = enKeys.filter((k) => !keys.includes(k))
  const extra = keys.filter((k) => !enSet.has(k))
  if (missing.length || extra.length) {
    failed = true
    console.error(
      `${locale.out}: missing=[${missing.join(', ')}] extra=[${extra.join(', ')}]`,
    )
  }
}
const enJson = JSON.parse(await readFile(join(outDir, 'en.json'), 'utf8'))
const emptyEn = enKeys.filter((k) => enJson[k] === '')
if (emptyEn.length) console.warn(`en: empty values (source-faithful): ${emptyEn.join(', ')}`)
if (failed) {
  console.error('export failed: locale key sets diverge from en')
  process.exit(1)
}
