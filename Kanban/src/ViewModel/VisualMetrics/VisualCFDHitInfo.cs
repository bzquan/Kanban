using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Kanban.Model;
using static Kanban.Util.DateTimeUtil;

namespace Kanban.ViewModel
{
    public class VisualCFDHitInfo : VisualHitInfo
    {
        static readonly Pen LinePen = new Pen(Brushes.Red, 1);

        public VisualCFDHitInfo(ProcessHistory processHistory, List<Point> points)
        {
            using (DrawingContext dc = base.RenderOpen())
            {
                DrawPolygonOrPolyline(dc, null, LinePen, points.ToArray(), FillRule.EvenOdd, true);
                DrawPlotPoints(dc, processHistory);

                bool canCalcWIP = !processHistory.IsLastProcess;
                WIPInfo WIP = canCalcWIP ? processHistory.WIP : new WIPInfo { MaxWIP = 0 };
                DrawWIPIndicator(dc, WIP, processHistory, points);
            }
        }

        private static void DrawPlotPoints(DrawingContext dc, Model.ProcessHistory processHistory)
        {
            CardsCountByDays items = MakeItemPoints(processHistory);
            DrawItemCount(dc, Brushes.Black, items, true);
        }

        private static CardsCountByDays MakeItemPoints(ProcessHistory processHistory)
        {
            CardsCountByDays items = new CardsCountByDays(VisualXYGridChart.ItemCountUnit4CFD);

            DateTime startDate = items.FromDate;
            for (int i = 1; i < H_SCALE_COUNT + 1; i++)
            {
                DateTime endDate = startDate.AddDays(ONE_WEEK);

                CardsByDate cardsByDate = processHistory.GetCardsByDate(endDate);
                if (processHistory.IsLastProcess)
                {
                    int itemCount = processHistory.IsLastProcess ? cardsByDate.CumulativeFlowCardCount : cardsByDate.CardCount;
                    items.SetItemCount(endDate, cardsByDate.CumulativeFlowCardCount, 0);
                }
                else
                {
                    int baseItemCount = cardsByDate.CumulativeFlowCardCount - cardsByDate.CardCount;
                    items.SetItemCount(endDate, cardsByDate.CardCount, baseItemCount);
                }

                startDate = endDate;
            }

            return items;
        }

        private static void DrawWIPIndicator(DrawingContext dc, WIPInfo WIP, ProcessHistory processHistory, List<Point> points)
        {
            if (WIP.MaxWIP == 0) return;

            int index = (WIP.MaxWIPDate - processHistory.FromDate).Days;
            if ((index >= 0) && (index < points.Count / 2))
            {
                Point top = points[index];
                Point bottom = points[points.Count - index - 1];
                VisualWIPIndicator.DrawWIPIndicator(dc, top, bottom, Brushes.Black, new Pen(Brushes.Black, 1));
            }
        }
    }
}
