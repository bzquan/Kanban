import DeleteIcon from '@mui/icons-material/Delete'
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

  // The server localizes the new board's title/description from uiLanguage.
  const createMutation = useMutation({
    mutationFn: () => boardsApi.create(i18n.language),
    onSuccess: (board) => {
      // Invalidate alongside the navigate-away: with a non-zero staleTime the
      // list would otherwise stay stale when we navigate back to it later.
      void queryClient.invalidateQueries({ queryKey: ['boards'] })
      navigate(`/boards/${board.id}`)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => boardsApi.remove(id),
    onSuccess: () => {
      setDeleteTarget(null)
      void queryClient.invalidateQueries({ queryKey: ['boards'] })
    },
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
          onClick={() => createMutation.mutate()}
        >
          {t('ToolTip_AddBoard')}
        </Button>
      </Box>

      {boards.length === 0 ? (
        <Typography data-testid="board-list-empty" color="text.secondary">
          {t('BoardList_Empty')}
        </Typography>
      ) : (
        <List>
          {boards.map((board) => {
            const id = board.id ?? ''
            return (
              <ListItem
                key={id}
                data-testid={`board-row-${id}`}
                disablePadding
                secondaryAction={
                  <Box sx={{ display: 'flex' }}>
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
                      onClick={() => setDeleteTarget(board)}
                    >
                      <DeleteIcon />
                    </IconButton>
                  </Box>
                }
              >
                <ListItemButton onClick={() => openBoard(id)}>
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
