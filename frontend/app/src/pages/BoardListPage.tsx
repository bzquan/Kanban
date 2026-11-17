import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import DeleteIcon from '@mui/icons-material/Delete'
import RefreshIcon from '@mui/icons-material/Refresh'
import TuneIcon from '@mui/icons-material/Tune'
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Typography,
} from '@mui/material'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'

import { boardsApi } from '../api/client'
import type { BoardDto } from '../api/types'
import ProcessStepEditor from '../components/ProcessStepEditor'
import { format0 } from '../util/format'

// localStorage key the board page (and later tasks) read to know where we came from.
const LAST_BOARD_KEY = 'kanban.lastBoardId'

// Plan C (mockup-approved 11-17): rows must read as list items — per-row left
// strip from this cycling palette, thin divider, odd-row tint, row hover and
// a chevron in the action area.
const ROW_STRIP_COLORS = ['#1976d2', '#2e7d32', '#ef6c00'] as const

export default function BoardListPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const boardsQuery = useQuery({ queryKey: ['boards'], queryFn: boardsApi.list })
  const boards = boardsQuery.data ?? []

  // Board currently being confirmed for deletion; null closes the dialog.
  const [deleteTarget, setDeleteTarget] = useState<BoardDto | null>(null)

  // Board whose process-step editor is open (Task 9); null unmounts it —
  // conditional mount is what makes the editor re-prefill per board.
  const [stepsTarget, setStepsTarget] = useState<BoardDto | null>(null)

  // Final review Finding 1: a server failure on the app's front door must be
  // visible — a failed create/delete never spins silently. Each line is the
  // already-localized message; null (and clearing on the next attempt) hides it.
  const [createError, setCreateError] = useState<string | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  // The server localizes the new board's title/description from uiLanguage.
  const createMutation = useMutation({
    mutationFn: () => boardsApi.create(i18n.language),
    onSuccess: (board) => {
      setCreateError(null)
      // Invalidate alongside the navigate-away: with a non-zero staleTime the
      // list would otherwise stay stale when we navigate back to it later.
      void queryClient.invalidateQueries({ queryKey: ['boards'] })
      navigate(`/boards/${board.id}`)
    },
    onError: () => setCreateError(t('BoardList_CreateError')),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => boardsApi.remove(id),
    onSuccess: () => {
      setDeleteTarget(null)
      setDeleteError(null)
      void queryClient.invalidateQueries({ queryKey: ['boards'] })
    },
    // Failure keeps the confirm dialog open (deleteTarget unchanged) with the
    // error line inside it — the user can retry or close deliberately.
    onError: () => setDeleteError(t('BoardList_DeleteError')),
  })

  const openBoard = (id: string): void => {
    try {
      localStorage.setItem(LAST_BOARD_KEY, id)
    } catch {
      // storage unavailable (private mode) — navigation still applies
    }
    navigate(`/boards/${id}`)
  }

  return (
    <Box data-testid="board-list-page" sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, mb: 1 }}>
        {/* System nav (WPF MainWindow's Setting/About buttons): Settings + About. */}
        <Button data-testid="nav-settings-button" onClick={() => navigate('/settings')}>
          {t('ToolTip_GotoSetting')}
        </Button>
        <Button data-testid="nav-about-button" onClick={() => navigate('/about')}>
          {t('ToolTip_ReleaseNote')}
        </Button>
        <Button
          variant="contained"
          data-testid="new-board-button"
          disabled={createMutation.isPending}
          onClick={() => {
            setCreateError(null)
            createMutation.mutate()
          }}
        >
          {t('ToolTip_AddBoard')}
        </Button>
      </Box>

      {createError !== null && (
        <Typography color="error" data-testid="board-create-error" sx={{ mb: 1 }}>
          {createError}
        </Typography>
      )}

      {boardsQuery.isError ? (
        /* Server failure on the front door: mirror BoardPage's board-error
           pattern (localized error + action) so a 5xx never reads as
           "no boards". Retry re-runs the query in place. */
        <Box
          data-testid="board-list-error"
          sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}
        >
          <Typography color="error">{t('BoardList_LoadError')}</Typography>
          <Box>
            <Button
              data-testid="board-list-retry-button"
              startIcon={<RefreshIcon />}
              onClick={() => void boardsQuery.refetch()}
            >
              {t('ToolTip_Refresh')}
            </Button>
          </Box>
        </Box>
      ) : boards.length === 0 ? (
        <Typography data-testid="board-list-empty" color="text.secondary">
          {t('BoardList_Empty')}
        </Typography>
      ) : (
        <List>
          {boards.map((board, index) => {
            const id = board.id ?? ''
            return (
              <ListItem
                key={id}
                data-testid={`board-row-${id}`}
                disablePadding
                sx={{
                  // Plan C list-item identity (see ROW_STRIP_COLORS): strip per
                  // row, thin divider, odd-row tint, whole-row hover. Hover is
                  // declared after the nth-of-type rule so it wins at equal
                  // specificity (same order as the approved mockup).
                  borderLeft: `5px solid ${ROW_STRIP_COLORS[index % ROW_STRIP_COLORS.length]}`,
                  borderBottom: '1px solid #eeeeee',
                  '&:nth-of-type(odd)': { backgroundColor: '#f8f9fb' },
                  '&:hover': { backgroundColor: '#e3f2fd' },
                  '&:hover .row-chevron': { color: '#1976d2' },
                  '& .MuiListItemText-primary': { fontWeight: 700 },
                }}
                secondaryAction={
                  <Box sx={{ display: 'flex', alignItems: 'center' }}>
                    <IconButton
                      edge="end"
                      aria-label={t('board.editSteps')}
                      data-testid={`board-row-steps-${id}`}
                      onClick={() => setStepsTarget(board)}
                    >
                      <TuneIcon />
                    </IconButton>
                    <IconButton
                      edge="end"
                      aria-label={t('ToolTip_Delete')}
                      data-testid={`board-row-delete-${id}`}
                      onClick={() => {
                        setDeleteError(null)
                        setDeleteTarget(board)
                      }}
                    >
                      <DeleteIcon />
                    </IconButton>
                    {/* Decorative click affordance (row itself is the button),
                        styled by the row's :hover .row-chevron rule. */}
                    <ChevronRightIcon
                      className="row-chevron"
                      data-testid={`board-row-chevron-${id}`}
                      sx={{ color: '#bbbbbb' }}
                    />
                  </Box>
                }
              >
                {/* Row-level hover owns the highlight; kill the button's own
                    hover so it doesn't blend over the row tint/hover color. */}
                <ListItemButton
                  onClick={() => openBoard(id)}
                  sx={{ '&:hover': { backgroundColor: 'transparent' } }}
                >
                  <ListItemText
                    primary={format0(t('Board_NameFormat'), board.title ?? '')}
                    secondary={
                      <>
                        {/* plain block spans: ListItemText styles the secondary slot */}
                        <span style={{ display: 'block' }}>
                          {format0(t('Board_DetailFormat'), board.description ?? '')}
                        </span>
                        <span style={{ display: 'block' }}>
                          {`${t('Main_Developers')} ${board.developers ?? ''}`}
                        </span>
                        <span style={{ display: 'block' }}>
                          {format0(
                            t('BoardList_StepsFormat'),
                            String(board.processSteps?.length ?? 0),
                          )}
                        </span>
                      </>
                    }
                  />
                </ListItemButton>
              </ListItem>
            )
          })}
        </List>
      )}

      <Dialog open={deleteTarget !== null} onClose={() => setDeleteTarget(null)}>
        <DialogTitle>{t('Message_DeleteComfirmTitle')}</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            {format0(t('Message_DeleteComfirmQuesion'), deleteTarget?.title ?? '')}
          </Typography>
          {deleteError !== null && (
            <Typography variant="body2" color="error" data-testid="board-delete-error">
              {deleteError}
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)}>{t('View_Close')}</Button>
          <Button
            color="error"
            data-testid="confirm-delete-button"
            disabled={deleteMutation.isPending}
            onClick={() => {
              if (deleteTarget) deleteMutation.mutate(deleteTarget.id ?? '')
            }}
          >
            {t('ToolTip_Delete')}
          </Button>
        </DialogActions>
      </Dialog>

      {stepsTarget !== null && (
        <ProcessStepEditor
          board={stepsTarget}
          open
          onClose={() => setStepsTarget(null)}
        />
      )}
    </Box>
  )
}
