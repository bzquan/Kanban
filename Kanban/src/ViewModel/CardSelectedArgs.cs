using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    public class CardSelectedArgs : EventArgs
    {
        Card m_SelectedCard;

        public CardSelectedArgs(Card selectedCard)
        {
            m_SelectedCard = selectedCard;
        }

        public Card SelectedCard => m_SelectedCard;
    }
}
