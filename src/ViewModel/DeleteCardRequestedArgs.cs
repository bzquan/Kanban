using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    class DeleteCardRequestedArgs : EventArgs
    {
        Card m_CardToDelete;

        public DeleteCardRequestedArgs(Card card)
        {
            m_CardToDelete = card;
        }

        public Card CardToDelete => m_CardToDelete;
    }
}
