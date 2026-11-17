// Contract of the release-note exporter (Plan A, agreed 2026-11-17):
// Kanban/ReleaseNote/*.html is the single source of truth (the user edits
// only these on each WPF release); the SPA's public/release-note/{ja,en}.html
// are generated, never hand-copied — the hand copies had already drifted
// (missing the 3.2.0 entry when WPF had it).

import { access, cp, mkdtemp, mkdir, readFile, rename, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
// No `import { expect, it } from 'vitest'` here: a bare 'vitest' import from
// an .mjs file resolves to a second runtime instance under vitest v5
// ("failed to find the current suite"). vitest.config.ts sets globals: true.

import { MAPPINGS, exportReleaseNotes } from './export-release-note.mjs'

async function makeSrcFixture() {
  const srcDir = await mkdtemp(join(tmpdir(), 'kanban-rn-src-'))
  await writeFile(
    join(srcDir, 'ReleaseNote_jp.html'),
    '<h5>Version 9.9.9 - 2026.11.17</h5>\n<ol><li>Feature：テスト</li></ol>\n',
    'utf8',
  )
  await writeFile(
    join(srcDir, 'ReleaseNote.html'),
    '<h5>Version 9.9.9 - 2026.11.17</h5>\n<ol><li>Feature: test entry</li></ol>\n',
    'utf8',
  )
  return srcDir
}

it('maps the WPF release notes to the SPA public files with verbatim content', async () => {
  const srcDir = await makeSrcFixture()
  const outDir = join(await mkdtemp(join(tmpdir(), 'kanban-rn-out-')), 'release-note')

  await exportReleaseNotes({ srcDir, outDir })

  // Renames are the contract: _jp suffix -> ja, bare name -> en.
  expect(MAPPINGS).toEqual([
    { src: 'ReleaseNote_jp.html', out: 'ja.html' },
    { src: 'ReleaseNote.html', out: 'en.html' },
  ])

  expect(await readFile(join(outDir, 'ja.html'), 'utf8')).toContain('Feature：テスト')
  expect(await readFile(join(outDir, 'en.html'), 'utf8')).toContain('Feature: test entry')
})

it('is re-runnable: a second export overwrites stale output', async () => {
  const srcDir = await makeSrcFixture()
  const outDir = join(await mkdtemp(join(tmpdir(), 'kanban-rn-out-')), 'release-note')
  await mkdir(outDir, { recursive: true })
  // Simulate the drift state: stale copy without the new version.
  await writeFile(join(outDir, 'ja.html'), '<h5>Version 3.1.3</h5>\n', 'utf8')

  await exportReleaseNotes({ srcDir, outDir })
  await exportReleaseNotes({ srcDir, outDir })

  const ja = await readFile(join(outDir, 'ja.html'), 'utf8')
  expect(ja).toContain('Version 9.9.9')
  expect(ja).not.toContain('3.1.3')
})

it('fails loudly when a source file is missing (never silently keeps stale copies)', async () => {
  const srcDir = await makeSrcFixture()
  const { rm } = await import('node:fs/promises')
  await rm(join(srcDir, 'ReleaseNote.html'))
  const outDir = join(await mkdtemp(join(tmpdir(), 'kanban-rn-out-')), 'release-note')

  await expect(exportReleaseNotes({ srcDir, outDir })).rejects.toThrow(/ReleaseNote\.html/)
})

it('importing the script does not regenerate public/release-note (side effects run only as main)', async () => {
  // Deterministic proof of the main-module guard: hide the real output dir,
  // then evaluate the script once more (cache-busted). If the guard were
  // removed, its top-level export would recreate the hidden dir and the
  // access() assertion below would fail. The real dir is restored at the end.
  const appRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
  const realOut = join(appRoot, 'public', 'release-note')
  const hidden = `${realOut}.hidden`
  await rm(hidden, { recursive: true, force: true })
  try {
    await rename(realOut, hidden)
    // Fresh evaluation (query busts the module cache). Absolute URL +
    // @vite-ignore so vite's dynamic-import analysis leaves it alone.
    // (jsdom rewrites import.meta.url to http:, so build the file: URL from
    // the module's real path instead.)
    const scriptPath = join(dirname(fileURLToPath(import.meta.url)), 'export-release-note.mjs')
    const freshUrl = `${pathToFileURL(scriptPath).href}?guard=${Date.now()}`
    await import(/* @vite-ignore */ freshUrl)
    await expect(access(realOut)).rejects.toThrow()
  } finally {
    await cp(hidden, realOut, { recursive: true, force: true }).catch(() => {})
    await rm(hidden, { recursive: true, force: true })
  }
})
