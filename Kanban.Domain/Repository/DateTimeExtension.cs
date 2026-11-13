using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Repository
{
    public static class DateTimeExtension
    {
        public static DateTime UTC2Local(this DateTime utc)
        {
            DateTime runtimeKnowsThisIsUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return runtimeKnowsThisIsUtc.ToLocalTime();
        }
    }
}
