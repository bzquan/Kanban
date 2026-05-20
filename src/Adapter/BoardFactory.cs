using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Infrastructure
{
    public class BoardFactory : Model.IBoardFactory
    {
        private Repository.IDBBoard m_DBBoard;
        private Util.ILocalization m_Localization;

        public BoardFactory(Repository.IDBBoard dbBoard, Util.ILocalization localization)
        {
            m_DBBoard = dbBoard;
            m_Localization = localization;
        }

        public async Task<Model.Board> CreateBoard()
        {
            Model.DevProcess defaultDevProcess = new Model.DevProcess();
            Model.Board board = new Model.Board(m_Localization.BoardDefaultTitle, m_Localization.BoardDefaultDescription, defaultDevProcess);
            await m_DBBoard.Insert(board.BoardOfRepository);
            return board;
        }
    }
}
