import i18n from 'i18next'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it } from 'vitest'

import { detectLanguage, initI18n, LANG_STORAGE_KEY } from '../i18n'
import { renderWithProviders } from '../test/renderWithProviders'

import SettingsPage from './SettingsPage'

beforeEach(() => {
  localStorage.clear()
  void initI18n()
  void i18n.changeLanguage('en')
})

afterEach(() => {
  // The i18n singleton is shared across test files in this module graph;
  // leave it on en so untouched tests keep their English expectations.
  void i18n.changeLanguage('en')
  localStorage.clear()
})

// MUI v9 Select opens its popup from the inner combobox element; clicking the
// data-testid root only focuses it.
async function openSelect(): Promise<void> {
  await userEvent.setup().click(await screen.findByRole('combobox'))
}

async function chooseLanguage(label: string): Promise<void> {
  await openSelect()
  await userEvent.setup().click(await screen.findByRole('option', { name: label }))
}

it('shows the language section (only) with its three endonym options', async () => {
  renderWithProviders(<SettingsPage />)

  // WebSettings_Title (web-only key: Setting_Title still says "DB backup",
  // which spec §4.6 contradicts) heads the section; Setting_Language labels the select.
  expect(await screen.findByText('Settings')).toBeInTheDocument()
  expect(screen.getByText('Language:')).toBeInTheDocument()
  // The DB choice/backup sections of the WPF settings window are deleted (spec §4.6):
  // no folder/restore/backup controls render.
  expect(screen.queryByText('Folder:')).not.toBeInTheDocument()
  expect(screen.queryByText('Backup')).not.toBeInTheDocument()

  await openSelect()
  for (const label of ['English', '日本語', '中文']) {
    expect(screen.getByRole('option', { name: label })).toBeInTheDocument()
  }
})

it('selecting 日本語 switches i18n immediately and persists kanban.lang (survives reload)', async () => {
  renderWithProviders(<SettingsPage />)
  await chooseLanguage('日本語')

  // Immediate runtime switch...
  expect(i18n.language).toBe('ja')
  // ...and the persisted manual choice a reload would re-read.
  expect(localStorage.getItem(LANG_STORAGE_KEY)).toBe('ja')
  // Task-3 deferred minor pinned here: setLanguage must write the storage key,
  // otherwise detectLanguage (the reload path) would fall back to sniffing.
  expect(detectLanguage()).toBe('ja')

  // Localized copy follows the switch inside the page (Setting_Language under ja).
  expect(await screen.findByText('言語：')).toBeInTheDocument()
})

it('selecting 中文 persists zh and re-localizes the page', async () => {
  renderWithProviders(<SettingsPage />)
  await chooseLanguage('中文')

  expect(i18n.language).toBe('zh')
  expect(localStorage.getItem(LANG_STORAGE_KEY)).toBe('zh')
  expect(detectLanguage()).toBe('zh')
  expect(await screen.findByText('语言:')).toBeInTheDocument()
})
