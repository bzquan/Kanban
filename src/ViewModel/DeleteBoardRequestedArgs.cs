using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    class DeleteBoardRequestedArgs : EventArgs
    {
        public Board Board { get; private set; }

        public DeleteBoardRequestedArgs(Board board)
        {
            Board = board;
        }
    }
}
