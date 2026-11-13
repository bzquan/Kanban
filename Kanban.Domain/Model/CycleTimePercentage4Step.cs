using System;
using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;
using static Kanban.Util.DateTimeUtil;

namespace Kanban.Model
{
    public class CycleTimePercentage4Step
    {
        int m_AdjustedPercentage;
        int m_CalculatedPercentage;

        public Repository.ProcessStep ProcessStep { get; private set; }
        public int WorkedDays { get; private set; }

        public CycleTimePercentage4Step(Repository.ProcessStep processStep, List<Activity> activities, List<ObjectId> doneCards)
        {
            ProcessStep = processStep;
            if (doneCards.Count > 0)
                WorkedDays = CalcWorkedDays(doneCards, activities) / doneCards.Count;
            else
                WorkedDays = 0;
        }

        public int Percentage
        {
            get { return m_CalculatedPercentage + m_AdjustedPercentage; }
            set { m_CalculatedPercentage = value; }
        }

        public void AddAdjustedPercentage(int percent)
        {
            m_AdjustedPercentage += percent;
        }

        private int CalcWorkedDays(List<ObjectId> doneCards, List<Activity> activities)
        {
            if (BoardHistory.IsLastProcessOfBoard(ProcessStep))
            {
                // Release stepは計測しない
                return 0;
            }

            List<DateTime> workedDays = new List<DateTime>();
            foreach (var cardID in doneCards)
            {
                List<Activity> activitiesOfCard = activities.FindAll(a => a.CardID == cardID);
                List<DateTime> dates = DaysStayedInProcess(activitiesOfCard);
                workedDays.AddRange(dates);
            }

            return workedDays.Count;
        }

        private List<DateTime> DaysStayedInProcess(List<Activity> activitiesOfCard)
        {
            List<DateTime> datesStayed = new List<DateTime>();

            List<Activity> activities4Process = ExtractActivities4Process(ProcessStep, activitiesOfCard);
            foreach (Activity activity in activities4Process)
            {
                DateTime beginDate = activity.StateChangedDate;
                Activity nextActivity = activitiesOfCard.FirstOrDefault(a => a.StateChangedDate > beginDate);
                if (nextActivity != null)
                {
                    DateTime endDate = nextActivity.StateChangedDate;
                    if (IsTimeEnouphStayingProcess(beginDate, endDate))
                    {
                        AppendWorkDays(datesStayed, beginDate, endDate);
                    }
                }
                else
                {
                    AppendWorkDays(datesStayed, beginDate, BoardHistory.ToDate);
                }
            }

            return datesStayed.Distinct().ToList();
        }

        private static List<Activity> ExtractActivities4Process(Repository.ProcessStep process, List<Activity> activitiesOfCard)
        {
            if (process.IsEndOfCycleTime)
                return activitiesOfCard   // 最後ProcessStepについては、Doneを除外し、WIPのみとする
                           .FindAll(a => a.WorkState.ProcessStepSeqNo == process.PhaseSeqNo && a.WorkState.IsWIP);
            else
                return activitiesOfCard
                           .FindAll(a => a.WorkState.ProcessStepSeqNo == process.PhaseSeqNo);
        }

        /// <summary>
        /// 経過時間が10分以上の場合、計測対象とする。
        /// </summary>
        /// <param name="beginDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        private bool IsTimeEnouphStayingProcess(DateTime beginDate, DateTime endDate) =>
            (endDate - beginDate).Minutes >= 10;

        private void AppendWorkDays(List<DateTime> dates, DateTime from, DateTime to)
        {
            foreach (DateTime date in EachDay(from, to))
            {
                if (date.IsWorkingDay())
                    dates.Add(date);
            }
        }
    }
}
