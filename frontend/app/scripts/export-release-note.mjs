// Export the WPF client's release notes to the SPA's public assets.
//   Kanban/ReleaseNote/ReleaseNote_jp.html -> public/release-note/ja.html
//   Kanban/ReleaseNote/ReleaseNote.html    -> public/release-note/en.html
//
// Kanban/ReleaseNote/ is the single source of truth: a release edits only
// those two files (user workflow, agreed 2026-11-17). The SPA copies used to
// be hand-duplicated at Task 1 and had already drifted (3.2.0 entry missing
// on the web side). This exporter runs automatically from `npm run dev` /
// `npm run build`, so a built/served web app always matches the WPF notes.
//
// zh: no variant exists on either front end (AboutPage falls zh back to en),
// so none is produced here either — parity with WPF, not a missing locale.

import { copyFile, mkdir } from 'node:fs/promises'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const appRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = resolve(appRoot, '..', '..')
const defaultSrcDir = join(repoRoot, 'Kanban', 'ReleaseNote')
const defaultOutDir = join(appRoot, 'public', 'release-note')

// Renames are the contract: WPF naming (_jp suffix / bare name) -> SPA naming.
export const MAPPINGS = [
  { src: 'ReleaseNote_jp.html', out: 'ja.html' },
  { src: 'ReleaseNote.html', out: 'en.html' },
]

// Verbatim byte copy — no transformation, so WPF's HtmlRichTextBoxBehavior
// and the SPA iframe render the same markup. Missing sources reject (copyFile
// ENOENT), which fails the build instead of silently keeping stale copies.
export async function exportReleaseNotes({ srcDir = defaultSrcDir, outDir = defaultOutDir } = {}) {
  await mkdir(outDir, { recursive: true })
  for (const { src, out } of MAPPINGS) {
    await copyFile(join(srcDir, src), join(outDir, out))
  }
}

export async function main() {
  await exportReleaseNotes()
  for (const { src, out } of MAPPINGS) {
    console.log(`${src} -> public/release-note/${out}`)
  }
}

// Run only as the entrypoint (npm run release-note:export); importing the
// module (tests) must not mutate public/.
// No top-level await here: vitest v5 collection breaks (it() registers after
// the async module evaluation) when a test file imports a TLA module.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    console.error(`release-note export failed: ${err.message}`)
    process.exit(1)
  })
}
