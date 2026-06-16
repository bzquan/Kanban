using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kanban.Infrastructure
{
    public class ProcessExecuteCompositeCommand : IProcessExecuteCommand, Model.IProcessExecutorClient
    {
        Queue<IProcessExecuteCommand> m_CommandQueue = new Queue<IProcessExecuteCommand>();
        Model.IProcessExecutorClient m_ProcessExecutorClient;

        public ProcessExecuteCompositeCommand(Model.IProcessExecutorClient processExecutorClient)
        {
            m_ProcessExecutorClient = processExecutorClient;
        }

        public void EnqueueCammand(IProcessExecuteCommand cmd) => m_CommandQueue.Enqueue(cmd);

        public void Execute()
        {
            try
            {
                bool hasCommands = (m_CommandQueue.Count > 0);
                if (hasCommands)
                {
                    IProcessExecuteCommand command = m_CommandQueue.Dequeue();
                    command.Execute();
                }
                else
                {
                    m_ProcessExecutorClient.OnCompleted();
                    CommandStarted = false;
                }
            }
            catch (Exception ex)
            {
                m_ProcessExecutorClient.OnMessage(ex.Message);
                m_CommandQueue.Clear();
                OnCompleted();
            }
        }

        private bool CommandStarted { get; set; } = false;

         #region IProcessExecutorClient implementation
        public AutoResetEvent AutoResetEvent { get; private set; } = new AutoResetEvent(false);

        public event EventHandler TerminateRequested = delegate { };

        public void OnStarted()
        {
            if (!CommandStarted)
            {
                m_ProcessExecutorClient.OnStarted();
                CommandStarted = true;
            }
        }

        public void OnMessage(string line)
        {
            m_ProcessExecutorClient.OnMessage(line);
        }

        public void OnCompleted()
        {
            Execute();  // Execute next command
        }
        #endregion
    }
}
