using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Repository
{
    public class WorkState
    {
        public int ProcessStepSeqNo { get; set; } = 0;
        public bool IsWIP { get; set; } = true;
        public bool IsBlocked { get; set; } = false;
        public bool IsMergedIntoMaster { get; set; } = false;
        public bool IsMergedIntoMajorBranch { get; set; } = false;
    }
}
