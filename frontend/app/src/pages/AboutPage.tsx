import { Box, CircularProgress, Typography } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'

// public/release-note/{en,ja}.html are verbatim copies of
// Kanban/ReleaseNote/ReleaseNote(_jp).html; Vite serves public/ at root.
async function loadReleaseNote(file: 'en' | 'ja'): Promise<string> {
  // globalThis.fetch is resolved at call time so tests can stub it
  // (src/test/fetchStub keys on "GET /release-note/<file>.html").
  const res = await globalThis.fetch(`/release-note/${file}.html`)
  if (!res.ok) throw new Error(`release-note ${file}: HTTP ${res.status}`)
  return res.text()
}

// WPF AboutControlView equivalent: shows the release-note HTML as-is. The
// iframe (srcDoc, never src) isolates the WPF-era markup/styles from MUI.
export default function AboutPage() {
  const { t, i18n } = useTranslation()
  const file = (i18n.language ?? 'en').toLowerCase().startsWith('ja') ? 'ja' : 'en'

  const noteQuery = useQuery({
    queryKey: ['release-note', file],
    queryFn: () => loadReleaseNote(file),
    // Static bundled asset — no background refetch churn.
    staleTime: Infinity,
  })

  return (
    <Box data-testid="about-page" sx={{ p: 2 }}>
      {/* AboutControl_ReleaseNote ("Release Note:") */}
      <Typography variant="h6" component="h2">
        {t('AboutControl_ReleaseNote')}
      </Typography>

      {noteQuery.isPending && <CircularProgress size={20} data-testid="release-note-loading" />}
      {noteQuery.isError && (
        <Typography color="error" data-testid="release-note-error">
          {/* New resx key (WPF bundled the note locally, it could not fail). */}
          {t('About_LoadError')}
        </Typography>
      )}

      <Box
        component="iframe"
        data-testid="release-note"
        title={t('ToolTip_ReleaseNote')}
        srcDoc={noteQuery.data ?? ''}
        // Static trusted markup; sandbox it anyway (no scripts, no same-origin).
        sandbox=""
        sx={{ width: '1', height: 480, border: 'none', mt: 1 }}
      />
    </Box>
  )
}
