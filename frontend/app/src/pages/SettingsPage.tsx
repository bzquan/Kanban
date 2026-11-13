import { Box, MenuItem, Select, Typography } from '@mui/material'
import { useTranslation } from 'react-i18next'

import { setLanguage, type AppLanguage } from '../i18n'

// Language choices are shown as endonyms (English/日本語/中文), like the WPF
// SystemSetting combo (enum Descriptions), so they read the same in every UI
// language; the labels still live in the resx trio — no hardcoded copy.
const LANGUAGES: readonly { value: AppLanguage; labelKey: string }[] = [
  { value: 'en', labelKey: 'Setting_LanguageEnglish' },
  { value: 'ja', labelKey: 'Setting_LanguageJapanese' },
  { value: 'zh', labelKey: 'Setting_LanguageChinese' },
]

// Spec §4.6: the WPF SystemSetting window's DB choice and backup sections are
// deleted — this page is the language switch only.
export default function SettingsPage() {
  const { t, i18n } = useTranslation()

  const current: AppLanguage =
    LANGUAGES.find((l) => (i18n.language ?? 'en').toLowerCase().startsWith(l.value))?.value ??
    'en'

  return (
    <Box data-testid="settings-page" sx={{ p: 2, maxWidth: 480 }}>
      {/* WebSettings_Title (web-only): Setting_Title is the WPF window title and
          still names the DB features spec §4.6 deleted from this page. */}
      <Typography variant="h6" component="h2">
        {t('WebSettings_Title')}
      </Typography>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mt: 2 }}>
        <Typography component="label" htmlFor="settings-language">
          {/* Setting_Language ("Language:") */}
          {t('Setting_Language')}
        </Typography>
        <Select
          id="settings-language"
          data-testid="language-select"
          value={current}
          size="small"
          sx={{ minWidth: 140 }}
          onChange={(e) => setLanguage(e.target.value as AppLanguage)}
        >
          {LANGUAGES.map((l) => (
            <MenuItem key={l.value} value={l.value}>
              {t(l.labelKey)}
            </MenuItem>
          ))}
        </Select>
      </Box>
    </Box>
  )
}
