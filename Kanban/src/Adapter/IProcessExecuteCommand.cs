using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Infrastructure
{
    public interface IProcessExecuteCommand
    {
        void Execute();
    }
}
