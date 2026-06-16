namespace Kanban.Model
{
    public class CardCompletionPerformance
    {
        private int m_ItemCount = 0;

        public int AheadOfScheduleCount { get; set; } = 0;
        public int DelayedCount { get; set; } = 0;
        public int OnTimeCount { get; set; } = 0;

        public int AheadOfSchedulePercentage { get; private set; } = 0;
        public int DelayedPercentage { get; private set; } = 0;
        public int OnTimePercentage { get; private set; } = 0;

        public int ItemCount
        {
            get { return m_ItemCount; }
            set
            {
                m_ItemCount = value;
                if (m_ItemCount == 0) return;

                AheadOfSchedulePercentage = ToPercent(AheadOfScheduleCount);
                DelayedPercentage = ToPercent(DelayedCount);
                if (IsLessThan100Percent && HasOnTimeItems)
                {
                    OnTimePercentage = CalcOnTimePercentage;
                }
                if (IsLessThan100Percent && !HasOnTimeItems)
                {
                    AheadOfSchedulePercentage = RoundUpAheadOfSchedulePercentage;
                }
            }
        }

        private int ToPercent(int part) => (int)(100.0 * ((double)part / ItemCount));
        private bool IsLessThan100Percent => AheadOfSchedulePercentage + DelayedPercentage < 100;
        private bool HasOnTimeItems => OnTimeCount > 0;
        private int CalcOnTimePercentage => 100 - (AheadOfSchedulePercentage + DelayedPercentage);
        private int RoundUpAheadOfSchedulePercentage => 100 - DelayedPercentage;
    }
}
