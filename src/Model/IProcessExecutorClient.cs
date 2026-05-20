using System;
using System.Threading;

namespace Kanban.Model
{
    public interface IProcessExecutorClient
    {
        void OnStarted();
        void OnMessage(string line);
        void OnCompleted();
        AutoResetEvent AutoResetEvent { get; }
        event EventHandler TerminateRequested;
    }
}
