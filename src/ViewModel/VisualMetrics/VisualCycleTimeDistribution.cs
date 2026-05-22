using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Kanban.Model;

namespace Kanban.ViewModel
{
    public class VisualCycleTimeDistribution : AbstractVisualMetrics
    {
        const double PEN_THICKNESS = 2.0;
        static Pen GraphLinePen = new Pen(Brushes.Black, PEN_THICKNESS);
        static Brush PlotPointBrush = Brushes.Black;

        string ToolTip { get; set; }

        public VisualCycleTimeDistribution(PlotPoint4CycleTime plotPoint)
        {
            using (DrawingContext dc = base.RenderOpen())
            {
                DrawPlotGraph(dc, plotPoint);
            }

            ToolTip = MakeToolTip(plotPoint);
        }

        public override string GetVisualToolTip(Point point)
        {
            return ToolTip;
        }

        private string MakeToolTip(PlotPoint4CycleTime plotPoint)
        {
            if (!plotPoint.IsValid()) return null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Properties.Metrics_CycleTimeLabel + plotPoint.AverageCycleTime4OneCard + Properties.Metrics_DaysPerItem);
            foreach (CycleTimePercentage4Step cycleTime in plotPoint.CycleTimePercentage4Steps)
            {
                if (cycleTime.Percentage > 0)
                {
                    sb.Append(cycleTime.ProcessStep.TrimmedName)
                      .AppendLine(String.Format(" : {0}%({1}" + Properties.Metrics_DaysPerItem + ")", cycleTime.Percentage, cycleTime.WorkedDays));
                }
            }

            return sb.ToString();
        }

        private static void DrawPlotGraph(DrawingContext dc, PlotPoint4CycleTime plotPoint)
        {
            double RECT_WIDTH = 40.0;
            double x = plotPoint.BottomPos.X - RECT_WIDTH / 2.0;
            double chartHeight = VisualXYGridChart.ActualHeight;
            double y = plotPoint.BottomPos.Y - chartHeight;
            Point topLeft = new Point(x, y);
            foreach (CycleTimePercentage4Step cycleTime in plotPoint.CycleTimePercentage4Steps)
            {
                if (cycleTime.Percentage > 0)
                {
                    double height = chartHeight * cycleTime.Percentage / 100.0;
                    Rect rect = new Rect(new Point(x, y), new Size(RECT_WIDTH, height));
                    Brush brush = Util.Util.BrushFromColorName(cycleTime.ProcessStep.LabelColor);
                    dc.DrawRectangle(brush, null, rect);
                    DrawPercentage(dc, rect, cycleTime.Percentage);

                    y += height;
                }
            }

            Rect outRect = new Rect(topLeft, new Size(RECT_WIDTH, chartHeight));
            dc.DrawRectangle(null, GraphLinePen, outRect);
        }
    }
}
