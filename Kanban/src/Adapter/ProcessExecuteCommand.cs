using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Infrastructure
{
    public class ProcessExecuteCommand : IProcessExecuteCommand
    {
        IProcessExecutor m_ProcessExecutor;
        ProcessArgument m_Argument;
        Model.IProcessExecutorClient m_ProcessExecutorClient;

        public ProcessExecuteCommand(IProcessExecutor processExecutor, ProcessArgument arg, Model.IProcessExecutorClient processExecutorClient)
        {
            m_ProcessExecutor = processExecutor;
            m_Argument = arg;
            m_ProcessExecutorClient = processExecutorClient;
        }

        public void Execute()
        {
            m_ProcessExecutor?.StartProcess(m_Argument, m_ProcessExecutorClient);
        }
    }
}
