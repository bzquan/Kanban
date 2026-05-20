using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Kanban.Infrastructure
{
    public class ProcessExecutor : IProcessExecutor
    {
        private ProcessDecorator m_CmdProcess;
        private BackgroundWorker m_BackgroundWorker;
        private List<Model.IProcessExecutorClient> m_ProcessExecutorClients;

        public void StartProcess(ProcessArgument arg, params Model.IProcessExecutorClient[] clients)
        {
            RegisitOnTerminateRequested(clients);
            InitBackgroundWorker();
            m_BackgroundWorker.RunWorkerAsync(arg);
        }

        private void RegisitOnTerminateRequested(Model.IProcessExecutorClient[] clients)
        {
            m_ProcessExecutorClients = clients.ToList();
            m_ProcessExecutorClients.ForEach(x => x.TerminateRequested += OnTerminateRequested);
            m_ProcessExecutorClients.ForEach(x => x.OnStarted());
        }

        public void OnTerminateRequested(object sender, EventArgs e)
        {
            try
            {
                m_BackgroundWorker.CancelAsync();
                m_CmdProcess.Kill();
            }
            catch (Exception ex)
            {
                m_ProcessExecutorClients.ForEach(x => x.OnMessage(ex.Message));
            }
        }

        private void InitBackgroundWorker()
        {
            m_BackgroundWorker = new System.ComponentModel.BackgroundWorker();
            m_BackgroundWorker.WorkerReportsProgress = true;
            m_BackgroundWorker.WorkerSupportsCancellation = true;
            m_BackgroundWorker.DoWork += BackgroundWorker_DoWork;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker backgroundWorker = sender as BackgroundWorker;
            ProcessArgument arg = e.Argument as ProcessArgument;

            m_CmdProcess = new ProcessDecorator();
            m_CmdProcess.OutputDataReceived += OnOutputDataReceived;
            m_CmdProcess.ErrorDataReceived += OnOutputDataReceived;

            try
            {
                m_CmdProcess.Start(arg);

                m_CmdProcess.BeginOutputReadLine();
                m_CmdProcess.BeginErrorReadLine();

                m_CmdProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                m_ProcessExecutorClients.ForEach(x => x.OnMessage(ex.Message));
            }

            m_ProcessExecutorClients.ForEach(x => x.AutoResetEvent.Set());
            m_ProcessExecutorClients.ForEach(x => x.OnCompleted());
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            m_ProcessExecutorClients.ForEach(x => x.OnMessage(e.Data));
        }
    }
}
