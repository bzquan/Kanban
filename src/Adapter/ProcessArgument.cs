using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Infrastructure
{
    public class ProcessArgument
    {
        public string ProgramPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
    }
}
