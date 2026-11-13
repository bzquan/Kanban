import DeleteIcon from '@mui/icons-material/Delete'
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

// localStorage key the board page (and later tasks) read to know where we came from.
const LAST_BOARD_KEY = 'kanban.lastBoardId'

// resx format strings are .NET-style ("Name：{0} "); i18next only interpolates
// {{var}}, so positional placeholders get substituted here.
function format0(template: string, value: string): string {
  return template.replace('{0}', value)
}

export default function BoardListPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const boardsQuery = useQuery({ queryKey: ['boards'], queryFn: boardsApi.list })
  const boards = boardsQuery.data ?? []

  // Board currently being confirmed for deletion; null closes the dialog.
  const [deleteTarget, setDeleteTarget] = useState<BoardDto | null>(null)

  // The server localizes the new board's title/description from uiLanguage.
  const createMutation = useMutation({
    mutationFn: () => boardsApi.create(i18n.language),
    onSuccess: (board) => navigate(`/boards/${board.id}`),
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
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1 }}>
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
                  <IconButton
                    edge="end"
                    aria-label={t('ToolTip_Delete')}
                    data-testid={`board-row-delete-${id}`}
                    onClick={() => setDeleteTarget(board)}
                  >
                    <DeleteIcon />
                  </IconButton>
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
    </Box>
  )
}
