using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Util
{
    public interface ILocalization
    {
        string BoardDefaultTitle { get; }
        string BoardDefaultDescription { get; }

        string CardDefaultTitle { get; }

        string ProcessBackLogTitle { get; }
        string ProcessRequirementTitle { get; }
        string ProcessDevelopmentTitle { get; }
        string ProcessTestTitle { get; }
        string ProcessReleaseTitle { get; }
        string ProcessPreparingTitle { get; }
        string ProcessWIPTitle { get; }
        string ProcessDoneTitle { get; }
        string ProcessNewStepTitle { get; }
    }
}
