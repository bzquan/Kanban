using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.Threading;

using Kanban.Infrastructure;
using Kanban.Model;

namespace UnitTest
{
    class CmdRunnerClient : IProcessExecutorClient, IDisposable
    {
        private AutoResetEvent m_AutoResetEvent = new AutoResetEvent(false);

        public AutoResetEvent AutoResetEvent
        {
            get
            {
                return m_AutoResetEvent;
            }
        }

        public event EventHandler TerminateRequested;

        public void OnMessage(string line)
        {
            Console.WriteLine(line);
        }

        public void OnCompleted()
        {
            Console.WriteLine("End of program execution.");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    m_AutoResetEvent.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                TerminateRequested?.Invoke(null, null);

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CmdRunnerClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
             GC.SuppressFinalize(this);
        }

        public void OnStarted()
        {
        }
        #endregion
    }

    [TestClass]
    public class RunDBDumpRestoreTest
    {
        string DB_PATH = @"C:\Work\Kanban\Kanban\db.backup\test";

        [TestMethod]
        public void DumpDB()
        {
            // Given
            IProcessExecutorClient client = new CmdRunnerClient();
            ProcessExecutor executor = new ProcessExecutor();
            ProcessArgument arg = new ProcessArgument { ProgramPath = "mongodump.exe", Arguments = "--out " + DB_PATH };

            // When
            executor.StartProcess(arg, client);

            // Then
            client.AutoResetEvent.WaitOne();
        }

        [TestMethod]
        public void RestoreDB()
        {
            // Given
            IProcessExecutorClient client = new CmdRunnerClient();
            ProcessExecutor executor = new ProcessExecutor();
            string param = "--drop " + DB_PATH;
            ProcessArgument arg = new ProcessArgument { ProgramPath = "mongorestore.exe", Arguments = param };

            // When
            executor.StartProcess(arg, client);

            // Then
            client.AutoResetEvent.WaitOne();
        }
    }
}
