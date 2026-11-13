using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kanban.Model
{
    public static class CardMetrics
    {
        public static async Task<int> GetWorkedDays(this Card card)
        {
            if (card.Board == null) return 0;

            Repository.ProcessStep start = card.Board.DevProcess.GetStartOfCycleTime();
            if (!card.HasStarted(start)) return 0;

            List<Activity> activities;
            if (CardsActivitiesCache.Cached(card))
            {
                activities = CardsActivitiesCache.GetActivities(card);
            }
            else
            {
                activities = await Activity.ActivityRepository.GetActivitiesOf(card);
            }
            return card.GetWorkedDays(activities);
        }

        public static int GetWorkedDays(this Card card, List<Activity> activities)
        {
            Repository.ProcessStep start = card.Board.DevProcess.GetStartOfCycleTime();
            if (!card.HasStarted(start)) return 0;

            Activity startActivity = activities.LastOrDefault(x =>
                            (x.WorkState.ProcessStepSeqNo == start.PhaseSeqNo) && x.WorkState.IsWIP);
            if (startActivity == null)
            {
                return 0;
            }

            int endSeqNo = card.GetEndOfCycleTimeProcessSeqNo();
            Activity endActivity = activities.LastOrDefault(x =>
                            (x.WorkState.ProcessStepSeqNo == endSeqNo) && (x.WorkState.IsWIP == false));

            return CalcWorkedDays(startActivity, endActivity);
        }

        private static bool HasStarted(this Card card, Repository.ProcessStep start)
        {
            if (start == null) return false;

            return start.PhaseSeqNo <= card.WorkState.ProcessStepSeqNo;
        }

        private static int GetEndOfCycleTimeProcessSeqNo(this Card card)
        {
            Repository.ProcessStep end = card.Board.DevProcess.GetEndOfCycleTime();
            Repository.ProcessStep last = card.Board.DevProcess.LastStep;
            if (end.PhaseSeqNo < last.PhaseSeqNo)
                return end.PhaseSeqNo;
            else
                return (end.PhaseSeqNo - 1);  // リリース作業をCycleTime表示から除外する
        }

        private static int CalcWorkedDays(Activity startActivity, Activity endActivity)
        {
            DateTime endDate = endActivity?.StateChangedDate ?? DateTime.Now;
            ProcessCycleTime cycleTime = new ProcessCycleTime { StartDate = startActivity.StateChangedDate, EndDate = endDate };
            return cycleTime.WorkDays();
        }
    }
}
