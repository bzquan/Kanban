namespace Kanban.Model
{
    public enum WorkPerformance { AheadOfSchedule, OnTime, Delayed }

    public static class EstimatedWorkEffortExtension
    {
        public static WorkPerformance GetWorkPerformance(this Repository.EstimatedWorkEffort estimatedWorkEffort, int workedDays)
        {
            if (workedDays == 0) return WorkPerformance.AheadOfSchedule;

            int AHEAD_DAYS = estimatedWorkEffort.AheadOfScheduleDays();
            int THRESHOLD_DAYS = estimatedWorkEffort.ThresholdDays();

            if (workedDays <= AHEAD_DAYS)
                return WorkPerformance.AheadOfSchedule;
            else if (workedDays <= THRESHOLD_DAYS)
                return WorkPerformance.OnTime;
            else
                return WorkPerformance.Delayed;
        }

        /// <summary>
        /// Maximum of days(including) which is considered ahead of shcedule.
        /// </summary>
        /// <param name="estimatedWorkEffort"></param>
        /// <returns></returns>
        public static int AheadOfScheduleDays(this Repository.EstimatedWorkEffort estimatedWorkEffort)
        {
            switch (estimatedWorkEffort)
            {
                case Repository.EstimatedWorkEffort.Small:
                    return 0;
                case Repository.EstimatedWorkEffort.Medium:
                    return 2;
                case Repository.EstimatedWorkEffort.Large:
                    return 5;
                case Repository.EstimatedWorkEffort.LargeExtra:
                    return 10;
                default:
                    return 0;
            }
        }

        public static int ThresholdDays(this Repository.EstimatedWorkEffort estimatedWorkEffort)
        {
            switch (estimatedWorkEffort)
            {
                case Repository.EstimatedWorkEffort.Small:
                    return 2;
                case Repository.EstimatedWorkEffort.Medium:
                    return 5;
                case Repository.EstimatedWorkEffort.Large:
                    return 10;
                case Repository.EstimatedWorkEffort.LargeExtra:
                    return 20;
                default:
                    return 5;
            }
        }

        public static string GetImageUri(this Repository.EstimatedWorkEffort estimatedWorkEffort)
        {
            string imageName = "unknown.png";
            switch (estimatedWorkEffort)
            {
                case Repository.EstimatedWorkEffort.Small:
                    imageName = "small.png";
                    break;
                case Repository.EstimatedWorkEffort.Medium:
                    imageName = "medium.png";
                    break;
                case Repository.EstimatedWorkEffort.Large:
                    imageName = "large.png";
                    break;
                case Repository.EstimatedWorkEffort.LargeExtra:
                    imageName = "large_extra.png";
                    break;
                default:
                    imageName = "unknown.png";
                    break;
            }

            return Util.Util.PackImageURI(imageName);
        }
    }
}
