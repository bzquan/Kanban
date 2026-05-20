using System.Diagnostics;
using System.IO;

namespace Kanban.Infrastructure
{
    public class ProcessDecorator : Process
    {
        public void Start(ProcessArgument arg)
        {
            this.StartInfo = CreateProcessStartInfo(arg);
            this.EnableRaisingEvents = true;    // request process raise events, e.g. OutputDataReceived
            base.Start();
        }

        private ProcessStartInfo CreateProcessStartInfo(ProcessArgument arg)
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo(fileName: arg.ProgramPath, arguments: arg.Arguments);
            procStartInfo.UseShellExecute = false;
            // To correctly handle output and/or error redirection you must also redirect input.
            // It seem to be feature/bug in runtime of the external application you are starting 
            procStartInfo.RedirectStandardInput = true;

            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError = true;
            procStartInfo.CreateNoWindow = true;
            if (arg.WorkingDirectory == null)
            {
                if (Directory.Exists(arg.WorkingDirectory))
                {
                    procStartInfo.WorkingDirectory = arg.WorkingDirectory;
                }
            }

            return procStartInfo;
        }
    }
}
