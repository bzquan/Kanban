using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban
{
    class ProcessStepView : System.Windows.Controls.ContentControl
    {
        public ProcessStepView(System.Windows.FrameworkElement parent, string processStepDataTemplate)
        {
            this.MinWidth = 200;
            this.ContentTemplate = parent.FindResource(processStepDataTemplate) as System.Windows.DataTemplate;
        }
    }
}
