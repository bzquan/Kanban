using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Model
{
    class CycleTimeAveragePercentage
    {
        IEnumerable<Repository.ProcessStep> m_Steps4Metrics;
        Dictionary<Repository.ProcessStep, int> m_AveragePercentageOfCycleTime = new Dictionary<Repository.ProcessStep, int>();

        public void Init(IEnumerable<Repository.ProcessStep> steps4Metrics, IEnumerable<CycleTimeDistribution> cycleTimeDistributions)
        {
            m_Steps4Metrics = steps4Metrics;
            CalcAverageCycleTime(cycleTimeDistributions);
            AdjustAveragePercentage();
        }

        public int GetAverageCycleTime(Repository.ProcessStep step)
        {
            return m_AveragePercentageOfCycleTime[step];
        }

        private void CalcAverageCycleTime(IEnumerable<CycleTimeDistribution> cycleTimeDistributions)
        {
            int totalWorkedDays = cycleTimeDistributions
                                        .SelectMany(x => x.CycleTimePercentage4Steps)
                                        .Sum(y => y.WorkedDays);

            foreach (Repository.ProcessStep step in m_Steps4Metrics)
            {
                int percentage = 0;
                if (totalWorkedDays > 0)
                {
                    int workedDays4Step = cycleTimeDistributions
                                            .SelectMany(x => x.CycleTimePercentage4Steps)
                                            .Where(y => y.ProcessStep == step)
                                            .Sum(y => y.WorkedDays);
                    percentage = workedDays4Step * 100 / totalWorkedDays;
                }
                m_AveragePercentageOfCycleTime[step] = percentage;
            }
        }

        private void AdjustAveragePercentage()
        {
            int totalPercent = m_AveragePercentageOfCycleTime.Sum(x => x.Value);
            int restPecents = 100 - totalPercent;

            if ((restPecents <= 0) || (restPecents >= 100)) return;

            var cycleTimes = m_AveragePercentageOfCycleTime.Where(x => x.Value > 0).ToList();
            var enumerator = cycleTimes.GetEnumerator();
            // restPecents分を均等にcycleTimesへ振り分ける
            foreach (var n in Enumerable.Range(0, restPecents))
            {
                if (!enumerator.MoveNext())
                {
                    enumerator = cycleTimes.GetEnumerator();
                    enumerator.MoveNext();
                }

                var step = enumerator.Current.Key;
                m_AveragePercentageOfCycleTime[step] = m_AveragePercentageOfCycleTime[step] + 1;
            }
        }
    }
}
