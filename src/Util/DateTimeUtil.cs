using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Util
{
    public static class DateTimeUtil
    {
        /// <summary>
        /// Usage:
        ///   foreach (DateTime day in EachDay(StartDate, EndDate))
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static IEnumerable<DateTime> EachDay(DateTime from, DateTime to)
        {
            for (var day = from.Date; day.Date <= to.Date; day = day.AddDays(1))
                yield return day;
        }

        public static bool IsWorkingDay(this DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Saturday &&
                   date.DayOfWeek != DayOfWeek.Sunday;
        }

        public static int WorkDays(DateTime from, DateTime to)
        {
            var totalDays = 0;
            foreach (DateTime date in EachDay(from, to))
            {
                if (date.IsWorkingDay()) totalDays++;
            }

            return totalDays;
        }

    }
}
