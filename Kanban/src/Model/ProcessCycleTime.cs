using System;

namespace Kanban.Model
{
    public class ProcessCycleTime
    {
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now;

        public int WorkDays() => Util.DateTimeUtil.WorkDays(StartDate, EndDate);
    }
}
