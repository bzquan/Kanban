import { Box } from '@mui/material'
import { useParams } from 'react-router'

// Placeholder until Tasks 5/7 replace it with columns, toolbar and DnD.
export default function BoardPage() {
  const { boardId } = useParams<{ boardId: string }>()
  return (
    <Box data-testid="board-page" sx={{ p: 2 }}>
      Board {boardId}
    </Box>
  )
}
