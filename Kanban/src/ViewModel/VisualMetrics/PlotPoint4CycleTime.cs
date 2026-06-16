using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Kanban.Model;

namespace Kanban.ViewModel
{
    public class PlotPoint4CycleTime
    {
        public PlotPoint4CycleTime(Point bottomPos, CycleTimeDistribution cycleTimeDistribution)
        {
            BottomPos = bottomPos;
            CycleTimeDistribution = cycleTimeDistribution;
        }

        public Point BottomPos { get; private set; }
        public CycleTimeDistribution CycleTimeDistribution { get; private set; }
        public int AverageCycleTime4OneCard => CycleTimeDistribution.AverageCycleTime4OneCard;
        public List<CycleTimePercentage4Step> CycleTimePercentage4Steps => CycleTimeDistribution.CycleTimePercentage4Steps;
        public bool IsValid() => CycleTimeDistribution.IsValid();
    }
}
