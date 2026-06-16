using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    public class GotoBoardPageRequestedArgs : EventArgs
    {
        public GotoBoardPageRequestedArgs(Board board)
        {
            Board = board;
        }

        public Board Board { get; private set; }
    }
}
