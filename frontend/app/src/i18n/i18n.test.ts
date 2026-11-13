import i18n from 'i18next'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import { detectLanguage, initI18n } from './index'

// jsdom exposes navigator.language='en-US' and a working localStorage;
// stubGlobal swaps navigator so detection sees each locale in turn.
function stubNavigator(language: string, languages?: string[]): void {
  vi.stubGlobal('navigator', {
    language,
    languages: languages ?? [language],
  })
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

it('detects ja from navigator.language=ja-JP, zh from zh-CN, en otherwise', () => {
  stubNavigator('ja-JP')
  expect(detectLanguage()).toBe('ja')
  stubNavigator('zh-CN')
  expect(detectLanguage()).toBe('zh')
  stubNavigator('en-US')
  expect(detectLanguage()).toBe('en')
  stubNavigator('fr-FR')
  expect(detectLanguage()).toBe('en') // unknown → en default
})

it('localStorage kanban.lang overrides detection', () => {
  localStorage.setItem('kanban.lang', 'zh')
  stubNavigator('en-US')
  expect(detectLanguage()).toBe('zh')
  localStorage.setItem('kanban.lang', 'de') // bogus stored value is ignored
  expect(detectLanguage()).toBe('en')
})

it('missing key falls back to en value; unknown language falls back to en', async () => {
  await initI18n()
  await i18n.changeLanguage('ja')
  expect(i18n.t('Main_Title')).toBe('看板')

  // Key present only in en: under ja it must resolve to the en text, never the raw key.
  i18n.addResource('en', 'ui', 'EnOnlyProbe', 'English only')
  expect(i18n.t('EnOnlyProbe')).toBe('English only')

  // Unknown language: the whole bundle falls back to en values, never raw keys.
  await i18n.changeLanguage('fr')
  expect(i18n.t('Main_Title')).toBe('Kanban')
  expect(i18n.t('EnOnlyProbe')).toBe('English only')
})
