import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import { Box, CircularProgress, IconButton, Typography } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'

// public/release-note/{en,ja}.html are GENERATED from Kanban/ReleaseNote/
// (single source of truth — the WPF files) by scripts/export-release-note.mjs,
// which runs automatically from npm run dev / build. Never edit them by hand.
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
  const navigate = useNavigate()
  const file = (i18n.language ?? 'en').toLowerCase().startsWith('ja') ? 'ja' : 'en'

  const noteQuery = useQuery({
    queryKey: ['release-note', file],
    queryFn: () => loadReleaseNote(file),
    // Static bundled asset — no background refetch churn.
    staleTime: Infinity,
  })

  return (
    // Grows into the shell's viewport-locked <main> (same pattern as the board
    // page) so the release-note iframe takes the remaining browser height —
    // the fixed 480px left it ~2/3 of the screen (reported 11-17).
    <Box
      data-testid="about-page"
      sx={{ p: 2, display: 'flex', flexDirection: 'column', flexGrow: 1, flexBasis: 0, minHeight: 0 }}
    >
      {/* Icon-only back button matching the board toolbar head (agreed 11-17);
          BoardPage_BackToList is the shared resx trio key. */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexShrink: 0 }}>
        <IconButton
          size="small"
          data-testid="back-to-list-button"
          aria-label={t('BoardPage_BackToList')}
          onClick={() => navigate('/')}
        >
          <ArrowBackIcon fontSize="small" />
        </IconButton>
        {/* AboutControl_ReleaseNote ("Release Note:") */}
        <Typography variant="h6" component="h2">
          {t('AboutControl_ReleaseNote')}
        </Typography>
      </Box>

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
        sx={{ width: '1', flexGrow: 1, minHeight: 0, border: 'none', mt: 1 }}
      />
    </Box>
  )
}
