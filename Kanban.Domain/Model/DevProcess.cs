using System.Collections.Generic;
using System.Linq;
using static Kanban.Util.KanbanDefinitions;

namespace Kanban.Model
{
    public class DevProcess
    {
        List<Repository.ProcessStep> m_ProcessSteps;

        public DevProcess()
        {
            string wipTitle = Localization.ProcessWIPTitle;
            string doneTitle = Localization.ProcessDoneTitle;

            m_ProcessSteps = new List<Repository.ProcessStep>();
            m_ProcessSteps.Add(new Repository.ProcessStep { PhaseSeqNo = 0, LabelColor = LabelColorNames[0], Name = Localization.ProcessBackLogTitle, WIPTitle = Localization.ProcessPreparingTitle, DoneTitle = doneTitle });
            m_ProcessSteps.Add(new Repository.ProcessStep { PhaseSeqNo = 1, LabelColor = LabelColorNames[1], Name = Localization.ProcessRequirementTitle, WIPTitle = wipTitle, DoneTitle = doneTitle, IsStartOfCycleTime = true });
            m_ProcessSteps.Add(new Repository.ProcessStep { PhaseSeqNo = 2, LabelColor = LabelColorNames[2], Name = Localization.ProcessDevelopmentTitle, WIPTitle = wipTitle, DoneTitle = doneTitle });
            m_ProcessSteps.Add(new Repository.ProcessStep { PhaseSeqNo = 3, LabelColor = LabelColorNames[3], Name = Localization.ProcessTestTitle, WIPTitle = wipTitle, DoneTitle = doneTitle, IsEndOfCycleTime = true });
            m_ProcessSteps.Add(new Repository.ProcessStep { PhaseSeqNo = 4, LabelColor = LabelColorNames[4], Name = Localization.ProcessReleaseTitle, WIPTitle = wipTitle, DoneTitle = doneTitle });
        }

        public DevProcess(List<Repository.ProcessStep> processSteps)
        {
            m_ProcessSteps = processSteps;
        }

        public static Util.ILocalization Localization { get; set; }

        public List<Repository.ProcessStep> ProcessSteps
        {
            get { return m_ProcessSteps; }
            set
            {
                m_ProcessSteps = value;
            }
        }

        public Repository.ProcessStep LastStep => m_ProcessSteps.Last();

        public Repository.ProcessStep AddProcessStep()
        {
            string labelColor = LabelColorNames[ProcessSteps.Count % LabelColorNames.Count];
            Repository.ProcessStep newProcessStep = new Repository.ProcessStep { PhaseSeqNo = ProcessSteps.Count, LabelColor = labelColor };
            newProcessStep.Name = Localization.ProcessNewStepTitle;
            newProcessStep.DoneTitle = Localization.ProcessDoneTitle;
            newProcessStep.WIPTitle = Localization.ProcessWIPTitle;
            m_ProcessSteps.Add(newProcessStep);

            return newProcessStep;
        }

        public void DeleteProcessStep(Repository.ProcessStep step)
        {
            ProcessSteps.Remove(step);
        }

        public void SortProcessSteps()
        {
            ProcessSteps.Sort((x, y) => x.PhaseSeqNo - y.PhaseSeqNo);
        }

        public bool AdjustProcessCycleTimeSteps()
        {
            if (!IsCycleTimeStepsStable(ProcessSteps)) return false;

            return AdjustProcessCycleTimeSteps(ProcessSteps);
        }

        public Repository.ProcessStep GetStartOfCycleTime()
        {
            return GetStartOfCycleTime(ProcessSteps);
        }

        public Repository.ProcessStep GetEndOfCycleTime()
        {
            return GetEndOfCycleTime(ProcessSteps);
        }

        public bool IsLastProcessOfBoard(Repository.ProcessStep process)
        {
            return (process == ProcessSteps.Last());
        }

        public IEnumerable<Repository.ProcessStep> GetCycleTimeSteps()
        {
            Repository.ProcessStep startStep = GetStartOfCycleTime();
            Repository.ProcessStep endStep = GetEndOfCycleTime();
            int count = (endStep.PhaseSeqNo - startStep.PhaseSeqNo) + 1;

            return ProcessSteps
                        .Skip(startStep.PhaseSeqNo)
                        .Take(count);
        }

        public bool SetDefaultCycleTimeDuration()
        {
            return AdjustProcessCycleTimeSteps(ProcessSteps);
        }

        public bool IsLastProcessStep(WorkState workState, int lastProcessStepSeqNo)
        {
            return (workState.ProcessStepSeqNo == lastProcessStepSeqNo) && !workState.IsWIP;
        }

        public bool IsCycleTimeStepsStable()
        {
            return IsCycleTimeStepsStable(ProcessSteps);
        }

        /// <summary>
        /// Adjust process cycle time steps if process steps are stable and
        /// process steps are inconsitent
        /// </summary>
        /// <returns>true if process cycle time is adjusted</returns>
        private static bool AdjustProcessCycleTimeSteps(List<Repository.ProcessStep> processSteps)
        {
            if (!IsCycleTimeStepsStable(processSteps)) return false;

            return MakeValidProcessCycleTimeSteps(processSteps);
        }

        private static Repository.ProcessStep GetStartOfCycleTime(List<Repository.ProcessStep> processSteps)
        {
            return GetRawStartOfCycleTime(processSteps) ?? processSteps.First();
        }

        private static Repository.ProcessStep GetRawStartOfCycleTime(List<Repository.ProcessStep> processSteps)
        {
            return processSteps.FirstOrDefault(x => x.IsStartOfCycleTime);
        }

        private static Repository.ProcessStep GetEndOfCycleTime(List<Repository.ProcessStep> processSteps)
        {
            return GetRawEndOfCycleTime(processSteps) ?? processSteps.Last();
        }

        private static Repository.ProcessStep GetRawEndOfCycleTime(List<Repository.ProcessStep> processSteps)
        {
            return processSteps.FirstOrDefault(x => x.IsEndOfCycleTime);
        }

        private static bool IsCycleTimeStepsStable(List<Repository.ProcessStep> processSteps)
        {
            int startOfCycleTimeCount = processSteps.Count(x => x.IsStartOfCycleTime);
            if (startOfCycleTimeCount > 1) return false;

            int endOfCycleTimeCount = processSteps.Count(x => x.IsEndOfCycleTime);
            if (endOfCycleTimeCount > 1) return false;

            return true;
        }

        /// <summary>
        /// Process cycle timeの開始と終了の一貫性を保つように、
        /// StartOfCycleTimeとEndOfCycleTimeを調節する
        /// </summary>
        /// <param name="processSteps"></param>
        /// <returns>true：一貫性がなかったので、調節した、false：一貫性があったので、調節不要である</returns>
        private static bool MakeValidProcessCycleTimeSteps(List<Repository.ProcessStep> processSteps)
        {
            if (IsCycleTimeStepsValid(processSteps)) return false;

            Repository.ProcessStep startOfCycleTime = GetStartOfCycleTime(processSteps);
            Repository.ProcessStep endOfCycleTime = GetEndOfCycleTime(processSteps);
            if (startOfCycleTime.PhaseSeqNo > endOfCycleTime.PhaseSeqNo)
            {
                endOfCycleTime = startOfCycleTime;
            }

            UpdateCycleTime(processSteps, startOfCycleTime, endOfCycleTime);
            return true;
        }

        private static bool IsCycleTimeStepsValid(List<Repository.ProcessStep> processSteps)
        {
            Repository.ProcessStep startOfCycleTime = GetRawStartOfCycleTime(processSteps);
            if (startOfCycleTime == null) return false;

            Repository.ProcessStep endOfCycleTime = GetRawEndOfCycleTime(processSteps);
            if (endOfCycleTime == null) return false;

            return (startOfCycleTime.PhaseSeqNo <= endOfCycleTime.PhaseSeqNo);
        }

        private static void UpdateCycleTime(List<Repository.ProcessStep> processSteps, Repository.ProcessStep startOfCycleTime, Repository.ProcessStep endOfCycleTime)
        {
            foreach (Repository.ProcessStep step in processSteps)
            {
                step.IsStartOfCycleTime = (step == startOfCycleTime);
                step.IsEndOfCycleTime = (step == endOfCycleTime);
            }
        }
    }
}
