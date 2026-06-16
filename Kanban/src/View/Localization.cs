using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban
{
    public class Localization : Util.ILocalization
    {
        public string BoardDefaultTitle => Properties.Resources.Board_DefaultTitle;

        public string BoardDefaultDescription => Properties.Resources.Board_DefaultDescription;
        public string CardDefaultTitle => Properties.Resources.Card_DefaultTitle;

        public string ProcessBackLogTitle => Properties.Resources.Process_BackLogTitle;
        public string ProcessRequirementTitle => Properties.Resources.Process_RequirementTitle;
        public string ProcessDevelopmentTitle => Properties.Resources.Process_DevelopmentTitle;
        public string ProcessTestTitle => Properties.Resources.Process_TestTitle;
        public string ProcessReleaseTitle => Properties.Resources.Process_ReleaseTitle;
        public string ProcessPreparingTitle => Properties.Resources.Process_PreparingTitle;
        public string ProcessWIPTitle => Properties.Resources.Process_WIPTitle;
        public string ProcessDoneTitle => Properties.Resources.Process_DoneTitle;
        public string ProcessNewStepTitle => Properties.Resources.Process_NewProcessStepTitle;
    }
}
