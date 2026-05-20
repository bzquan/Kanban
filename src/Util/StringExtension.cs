using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Util
{
    public static class StringExtension
    {
        public static string Truncate(this string str, int maxChars) =>
            str.Length <= maxChars ? str : str.Substring(0, maxChars) + "...";
    }
}
