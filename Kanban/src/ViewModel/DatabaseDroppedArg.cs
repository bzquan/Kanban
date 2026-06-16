using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    public class DatabaseDroppedArg : EventArgs
    {
        public DatabaseDroppedArg(string dbName)
        {
            DatabaseName = dbName;
        }

        public string DatabaseName { get; private set; }
    }
}
