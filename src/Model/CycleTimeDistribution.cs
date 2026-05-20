using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;
using static Kanban.Util.DateTimeUtil;

namespace Kanban.Model
{
    public class CycleTimeDistribution
    {
        IEnumerable<Repository.ProcessStep> m_Steps4Metrics;
        public List<CycleTimePercentage4Step> CycleTimePercentage4Steps { get; set; } = new List<CycleTimePercentage4Step>();

        public CycleTimeDistribution(IEnumerable<Repository.ProcessStep> steps4Metrics, List<Activity> activities, List<ObjectId> doneCards)
        {
            m_Steps4Metrics = steps4Metrics;
            CreateCycleTimeDistributions(activities, doneCards);
            CalcPercentageOfCycleTimeDistributions();
            AdjustCycleTimeDistribution();

            AverageCycleTime4OneCard = CalcAverageCycleTime(activities, doneCards);
        }

        public int AverageCycleTime4OneCard { get; private set; }

        public bool IsValid()
        {
            int sum = CycleTimePercentage4Steps.Sum(x => x.Percentage);
            return (sum > 0);
        }

        private int CalcAverageCycleTime(List<Activity> activities, List<ObjectId> doneCards)
        {
            if (doneCards.Count == 0) return 0;

            int firstStepSeqNo = m_Steps4Metrics.First().PhaseSeqNo;
            int lastStepSeqNo = m_Steps4Metrics.Last().PhaseSeqNo;
            int totalDays = 0;
            foreach (ObjectId cardID in doneCards)
            {
                Activity startActivity = activities.FirstOrDefault(x => (x.CardID == cardID) && (x.WorkState.ProcessStepSeqNo == firstStepSeqNo) && x.WorkState.IsWIP);
                Activity endActivity = activities.LastOrDefault(x => (x.CardID == cardID) && (x.WorkState.ProcessStepSeqNo == lastStepSeqNo) && !x.WorkState.IsWIP);
                if ((startActivity != null) && (endActivity != null))
                {
                    totalDays += WorkDays(startActivity.StateChangedDate, endActivity.StateChangedDate);
                }
            }

            int averageCycleTime = Util.Util.RoundUpByDivision(totalDays, doneCards.Count);　// 切り上げ
            return averageCycleTime;
        }

        private void CreateCycleTimeDistributions(List<Activity> activities, List<ObjectId> doneCards)
        {
            foreach (Repository.ProcessStep step in m_Steps4Metrics)
            {
                CycleTimePercentage4Steps.Add(new CycleTimePercentage4Step(step, activities, doneCards));
            }
        }

        private void CalcPercentageOfCycleTimeDistributions()
        {
            int totalDays = CycleTimePercentage4Steps.Sum(x => x.WorkedDays);
            foreach (CycleTimePercentage4Step cycle_time in CycleTimePercentage4Steps)
            {
                if (totalDays > 0)
                {
                    cycle_time.Percentage = cycle_time.WorkedDays * 100 / totalDays;
                }
                else
                {
                    cycle_time.Percentage = 0;
                }
            }
        }

        private void AdjustCycleTimeDistribution()
        {
            int totalPercent = CycleTimePercentage4Steps.Sum(x => x.Percentage);
            int restPecents = 100 - totalPercent;

            if ((restPecents <= 0) || (restPecents >= 100)) return;

            var cycleTimes = CycleTimePercentage4Steps.Where(x => x.Percentage > 0);
            var enumerator = cycleTimes.GetEnumerator();
            // restPecents分を均等にcycleTimesへ振り分ける
            foreach (var n in Enumerable.Range(0, restPecents))
            {
                if (!enumerator.MoveNext())
                {
                    enumerator = cycleTimes.GetEnumerator();
                    enumerator.MoveNext();
                }

                enumerator.Current.AddAdjustedPercentage(1);
            }
        }
    }
}
