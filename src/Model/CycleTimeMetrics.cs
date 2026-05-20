using System;
using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;

namespace Kanban.Model
{
    public class CycleTimeMetrics
    {
        BoardHistory m_BoardHistory;
        BoardHistoryWeeklyDecorator m_BoardHistoryWeeklyDecorator;
        List<CycleTimePercentage4Step> m_CycleTimeDistributions = new List<CycleTimePercentage4Step>();
        Dictionary<DateTime, CycleTimeDistribution> m_WeeklySummary = new Dictionary<DateTime, CycleTimeDistribution>();
        CycleTimeAveragePercentage m_CycleTimeAveragePercentage = new CycleTimeAveragePercentage();

        public IEnumerable<Repository.ProcessStep> Steps4Metrics { get; private set; }

        public CycleTimeMetrics(BoardHistory boardHistory)
        {
            m_BoardHistory = boardHistory;
            Steps4Metrics = BoardHistory.Board.GetCycleTimeSteps();
            m_BoardHistoryWeeklyDecorator = new BoardHistoryWeeklyDecorator(boardHistory);
        }

        public void SummarizeByWeekly(List<DateTime> dates)
        {
            m_WeeklySummary.Clear();
            foreach (DateTime toDate in dates)
            {
                m_WeeklySummary[toDate.Date] = MakeCycleTimeDistributionByWeekly(toDate);
            }

            m_CycleTimeAveragePercentage.Init(Steps4Metrics, AllCycleTimeDistributions);
        }

        public int GetWeeklyAverageCycleTime()
        {
            int totalCycleTimes = AllCycleTimeDistributions.Sum(x => x.AverageCycleTime4OneCard);
            int nonZeroCycleTimesNum = AllCycleTimeDistributions.Count(x => x.AverageCycleTime4OneCard > 0);
            return Util.Util.RoundUpByDivision(totalCycleTimes, nonZeroCycleTimesNum); // 切り上げ
        }

        public int GetAverageCycleTime(Repository.ProcessStep step)
        {
            return m_CycleTimeAveragePercentage.GetAverageCycleTime(step);
        }

        public CycleTimeDistribution GetCycleTimeDistributionByWeekly(DateTime toDate)
        {
            return m_WeeklySummary[toDate.Date];
        }

        private CycleTimeDistribution MakeCycleTimeDistributionByWeekly(DateTime toDate)
        {
            List<ObjectId> doneCards = m_BoardHistoryWeeklyDecorator.GetDoneCardsByWeek(toDate).ToList();
            List<Activity> activities = CardsActivitiesCache.GetActivities(doneCards);
            CycleTimeDistribution cycleTimeDistributionByDate = new CycleTimeDistribution(Steps4Metrics, activities, doneCards);

            return cycleTimeDistributionByDate;
        }

        private IEnumerable<CycleTimeDistribution> AllCycleTimeDistributions
            => m_WeeklySummary.Select(x => x.Value);
    }
}
