using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    public class CardsOfBoardRetrievedArgs : EventArgs
    {
        public int ProcessStepSeqNo { get; }

        public CardsOfBoardRetrievedArgs(int processStepSeqNo)
        {
            ProcessStepSeqNo = processStepSeqNo;
        }
    }
}
