using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Infrastructure
{
    public interface IProcessExecutor
    {
        void StartProcess(ProcessArgument arg, params Model.IProcessExecutorClient[] clients);
        void OnTerminateRequested(object sender, EventArgs e);
    }
}
