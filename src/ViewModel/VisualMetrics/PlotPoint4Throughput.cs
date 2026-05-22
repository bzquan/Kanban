using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Kanban.Model;

namespace Kanban.ViewModel
{
    public class PlotPoint4Throughput
    {
        CardCompletionPerformance m_CompletedCardInfo;

        public Point Pos { get; set; }
        public int ItemCount { get; set; } = 0;

        public CardCompletionPerformance CompletedCardInfo
        {
            get { return m_CompletedCardInfo; }
            set
            {
                m_CompletedCardInfo = value;
                ItemCount = m_CompletedCardInfo.ItemCount;
            }
        }
    }
}
