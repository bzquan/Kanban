using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

using static Kanban.Util.CircleNumImages;

namespace Kanban.ViewModel
{
    public class ProcessStep : NotifyPropertyChangedBase
    {
        public ProcessStep()
        {
            ProcessStepInfo = new Repository.ProcessStep();
        }

        public ProcessStep(int seqNo, string labelColor)
        {
            ProcessStepInfo = new Repository.ProcessStep { PhaseSeqNo = seqNo, LabelColor = labelColor };
        }

        public ProcessStep(Repository.ProcessStep processStep)
        {
            ProcessStepInfo = processStep;
        }

        public Repository.ProcessStep ProcessStepInfo { get; private set; }

        public DrawingImage SeqNoImage
        {
            get
            {
                string index = (PhaseSeqNo + 1).ToString();
                return GetCircleNumImage(text: index, background: Colors.LightCyan, font_color: Colors.Blue);
            }
        }

        public int PhaseSeqNo
        {
            get { return ProcessStepInfo.PhaseSeqNo; }
            set
            {
                SetProperty(ProcessStepInfo, value);
                base.OnPropertyChanged(nameof(SeqNoImage));
            }
        }

        public string LabelColor
        {
            get { return ProcessStepInfo.LabelColor; }
            set { SetProperty(ProcessStepInfo, value); }
        }

        public string Name
        {
            get { return ProcessStepInfo.Name; }
            set { SetProperty(ProcessStepInfo, value); }
        }

        public string WIPTitle
        {
            get { return ProcessStepInfo.WIPTitle; }
            set { SetProperty(ProcessStepInfo, value); }
        }

        public string DoneTitle
        {
            get { return ProcessStepInfo.DoneTitle; }
            set { SetProperty(ProcessStepInfo, value); }
        }

        bool AllowUpdateCycleTime { get; set; } = true;

        public bool IsStartOfCycleTime
        {
            get { return ProcessStepInfo.IsStartOfCycleTime; }
            set
            {
                if (AllowUpdateCycleTime)
                    SetProperty(ProcessStepInfo, value);
            }
        }

        public bool IsEndOfCycleTime
        {
            get { return ProcessStepInfo.IsEndOfCycleTime; }
            set
            {
                if (AllowUpdateCycleTime)
                    SetProperty(ProcessStepInfo, value);
            }
        }

        public void NotifyCycleTimeChanged()
        {
            // 可笑しい
            AllowUpdateCycleTime = false;
            base.OnPropertyChanged(nameof(IsStartOfCycleTime));
            base.OnPropertyChanged(nameof(IsEndOfCycleTime));
            AllowUpdateCycleTime = true;
        }
    }
}
