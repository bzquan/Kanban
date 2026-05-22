using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    class SwitchFrontBackBoardRequestedArgs : EventArgs
    {
        Card m_CardToSwitch;

        public SwitchFrontBackBoardRequestedArgs(Card card)
        {
            m_CardToSwitch = card;
        }

        public Card CardToSwitch => m_CardToSwitch;
    }
}
