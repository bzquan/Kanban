using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Repository
{
    public class ProcessStep
    {
        public int PhaseSeqNo { get; set; } = 0;
        public string LabelColor { get; set; } = "LimeGreen";
        public string Name { get; set; } = "不明ステップ";
        public string TrimmedName
        {
            get
            {
                string processName = Name;
                int index = processName.IndexOf('(');   // プロセス名称内の制限条件を外す。例えば、"開発(Limit:5)" => "開発"
                string trimmedProcessName = (index >= 0) ? processName.Substring(0, index) : processName;
                return trimmedProcessName;
            }
        }

        public string WIPTitle { get; set; } = "作業中";
        public string DoneTitle { get; set; } = "完了";
        public bool IsStartOfCycleTime { get; set; }
        public bool IsEndOfCycleTime { get; set; }
        public override string ToString() { return Name; }
    }
}
