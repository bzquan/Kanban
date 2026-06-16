using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kanban.Model;

namespace Kanban.ViewModel
{
    public class VisualThroughput : AbstractVisualMetrics
    {
        const double PEN_THICKNESS = 2.0;
        static Pen GraphLinePen = new Pen(Brushes.Black, PEN_THICKNESS);
        static Brush PlotPointBrush = Brushes.Black;

        string ToolTip { get; set; }

        public VisualThroughput(string colorName, PlotPoint4Throughput plotPoint)
        {
            GraphLinePen = GetGraphLinePen(colorName);

            using (DrawingContext dc = base.RenderOpen())
            {
                DrawPlotPoints(dc, plotPoint);
                DrawPlotGraph(dc, plotPoint);
            }

            ToolTip = MakeToolTip(plotPoint.CompletedCardInfo);
        }

        private string MakeToolTip(CardCompletionPerformance completedCardInfo)
        {
            if (completedCardInfo.ItemCount == 0) return null;

            StringBuilder sb = new StringBuilder(Properties.Metrics_DoneItems + completedCardInfo.ItemCount);
            if (completedCardInfo.OnTimeCount > 0)
            {
                sb.AppendLine()
                  .Append(Properties.Metrics_EstimationProper + completedCardInfo.OnTimeCount);
            }
            if (completedCardInfo.AheadOfScheduleCount > 0)
            {
                sb.AppendLine()
                  .Append(Properties.Metrics_EstimationMuch + completedCardInfo.AheadOfScheduleCount);
            }
            if (completedCardInfo.DelayedCount > 0)
            {
                sb.AppendLine()
                  .Append(Properties.Metrics_EstimationSmall + completedCardInfo.DelayedCount);
            }

            return sb.ToString();
        }

        public override string GetVisualToolTip(Point point)
        {
            return ToolTip;
        }

        private static Pen GetGraphLinePen(string colorName)
        {
            PlotPointBrush = Util.Util.BrushFromColorName(colorName);
            return new Pen(PlotPointBrush, PEN_THICKNESS);
        }

        private static void DrawPlotPoints(DrawingContext dc, PlotPoint4Throughput plot)
        {
            FormattedText formattedText = GetFormattedText(plot.CompletedCardInfo.ItemCount.ToString(), Brushes.Black, emSize: 16,
                                                           fontFamity: Util.HandWriteFontFamily.Bradley_Hand_ITC_FontFamily);
            double x = plot.Pos.X - formattedText.Width / 2;
            double y = plot.Pos.Y - formattedText.Height;
            dc.DrawText(formattedText, new Point(x, y));
        }

        private static void DrawPlotGraph(DrawingContext dc, PlotPoint4Throughput plotPoint)
        {
            if (plotPoint.CompletedCardInfo.ItemCount == 0) return;

            const double MARGIN = 2.0;
            double rectWidth = SCALE_HORIZONTAL_INTERVAL / 2.0 - MARGIN;
            double x = plotPoint.Pos.X - rectWidth;
            double y = plotPoint.Pos.Y;
            Rect rect = new Rect(new Point(x, y), new Size(rectWidth * 2.0, VisualXYGridChart.StartPos.Y - y - MARGIN));
            dc.DrawRectangle(null, GraphLinePen, rect);
            DrawPercentage(dc, plotPoint.CompletedCardInfo, rect);
        }

        private static void DrawPercentage(DrawingContext dc, CardCompletionPerformance completedCardInfo, Rect rect)
        {
            Point nextTopLeft = rect.TopLeft;
            if (completedCardInfo.OnTimePercentage > 0)
            {
                nextTopLeft = DrawEstimationPerformance(dc, Brushes.Green, nextTopLeft, rect, completedCardInfo.OnTimePercentage, "〇");
            }
            if (completedCardInfo.AheadOfSchedulePercentage > 0)
            {
                nextTopLeft = DrawEstimationPerformance(dc, Brushes.Yellow, nextTopLeft, rect, completedCardInfo.AheadOfSchedulePercentage, "△");
            }
            if (completedCardInfo.DelayedPercentage > 0)
            {
                nextTopLeft = DrawEstimationPerformance(dc, Brushes.Red, nextTopLeft, rect, completedCardInfo.DelayedPercentage, "∇");
            }
        }

        private static Point DrawEstimationPerformance(DrawingContext dc, Brush brush, Point topLeft, Rect rect, int percent, string symbol)
        {
            double height = rect.Height * percent / 100.0;
            Rect r = new Rect(topLeft, new Size(rect.Width, height));
            dc.DrawRectangle(brush, null, r);
            DrawPercentage(dc, r, percent, symbol);

            return r.BottomLeft;
        }
    }
}
