using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Kanban.Util;

namespace Kanban.ViewModel
{
    public class VisualXYGridChart : AbstractVisualMetrics
    {
        const int PADDING_4 = PADDING * 4;
        const double SCALE_HALF_LEN = 3.0;

        static readonly Brush LineBrush = Brushes.Blue;
        static readonly Pen LinePen = new Pen(LineBrush, 2);
        static readonly Pen GridLinePen = new Pen(Brushes.DarkGray, 1);
        static readonly Pen FramePen = new Pen(Brushes.DarkGray, 2);

        public const int ItemCountUnit4CFD = 10;
        public const int ItemCountUnit4Throughput = 5;
        public const int ItemCountUnit4BugsAndBlocked = 2;
        public const int PercentageUnit4CycleTimeDistribution = 10;
        public const int StoryPointUnit4TeamVelocity = 20;

        static double s_Width = METRICS_CHART_WIDTH - PADDING * 2;
        static double s_Height = METRICS_CHART_HEIGHT - PADDING * 2;
        static int WorkItemCountUnit { get; set; } = ItemCountUnit4CFD;
        private MetricChartType CurrentChartType { get; set; } = MetricChartType.CumulativeFlowDiagram;

        public VisualXYGridChart(MetricChartType chartType)
        {
            CurrentChartType = chartType;
            WorkItemCountUnit = GetWorkItemCountUnit();

            Point start = StartPos;
            Point x_end = new Point(start.X + s_Width - PADDING_4, start.Y);
            Point y_end = new Point(start.X, start.Y - s_Height + PADDING_4);

            using (DrawingContext dc = base.RenderOpen())
            {
                DrawGridChartFrame(dc);
                DrawHorizontalLine(dc, start, x_end);
                DrawHorizontalGridLine(dc, start, x_end);
                DrawVerticalLine(dc, start, y_end);
            }
        }

        public static Point StartPos => new Point(PADDING_4, METRICS_CHART_HEIGHT - PADDING_4);
        public static Point EndPos => new Point(StartPos.X + SCALE_HORIZONTAL_INTERVAL * H_SCALE_COUNT, StartPos.Y);
        public static double ActualHeight => SCALE_VERTICAL_INTERVAL * V_SCALE_COUNT;

        private int GetWorkItemCountUnit()
        {
            switch (CurrentChartType)
            {
                case MetricChartType.Throughput:
                    return ItemCountUnit4Throughput;
                case MetricChartType.CumulativeFlowDiagram:
                    return ItemCountUnit4CFD;
                case MetricChartType.CycleTimeDistribution:
                    return PercentageUnit4CycleTimeDistribution;
                case MetricChartType.TeamVelocity:
                    return StoryPointUnit4TeamVelocity;
                default:
                    return ItemCountUnit4Throughput;
            }
        }

        private void DrawGridChartFrame(DrawingContext dc)
        {
            Point pt1 = new Point(0, PADDING);
            Point pt2 = new Point(METRICS_CHART_WIDTH, METRICS_CHART_HEIGHT);
            Rect frame = new Rect(pt1, pt2);
            dc.DrawRectangle(null, FramePen, frame);
        }

        private void DrawHorizontalLine(DrawingContext dc, Point start, Point end)
        {
            dc.DrawLine(LinePen, start, end);
            DrawHorizontalScaleLines(dc, start);
            DrawRightArrow(dc, LineBrush, end);
            DrawTimeText(dc, start, end);
        }

        private void DrawHorizontalGridLine(DrawingContext dc, Point start, Point end)
        {
            Point vPt1 = new Point(start.X + SCALE_HALF_LEN, start.Y);
            Point vPt2 = new Point(end.X + SCALE_HALF_LEN, end.Y);
            for (int i = 0; i < V_SCALE_COUNT; i++)
            {
                vPt1.Y -= SCALE_VERTICAL_INTERVAL;
                vPt2.Y -= SCALE_VERTICAL_INTERVAL;
                dc.DrawLine(GridLinePen, vPt1, vPt2);
            }
        }

        private void DrawHorizontalScaleLines(DrawingContext dc, Point start)
        {
            Point vPt1 = new Point(start.X, start.Y - SCALE_HALF_LEN);
            Point vPt2 = new Point(start.X, start.Y + SCALE_HALF_LEN);
            for (int i = 0; i < H_SCALE_COUNT; i++)
            {
                vPt1.X += SCALE_HORIZONTAL_INTERVAL;
                vPt2.X += SCALE_HORIZONTAL_INTERVAL;
                dc.DrawLine(LinePen, vPt1, vPt2);
            }
        }

        private void DrawTimeText(DrawingContext dc, Point start, Point end)
        {
            DrawDateTextAlongXAxis(dc, start);
            DrawXAxisTitle(dc, end);
        }

        private void DrawDateTextAlongXAxis(DrawingContext dc, Point start)
        {
            double x = start.X;
            Point pos = new Point(x, start.Y + SCALE_HALF_LEN * 2);
            for (int i = 0; i < H_SCALE_COUNT + 1; i++)
            {
                DateTime date = FromDate4Metrics.AddDays(ONE_WEEK * i);
                FormattedText dateText = GetFormattedText(date.ToString(Properties.DateTime_MonthDayFormatter));
                pos.X = x - (dateText.Width / 2);
                dc.DrawText(dateText, pos);
                x += SCALE_HORIZONTAL_INTERVAL;
            }
        }

        private void DrawXAxisTitle(DrawingContext dc, Point end)
        {
            FormattedText formattedText = GetFormattedText(Properties.Metrics_DateLabel);
            Point endPos = new Point(end.X - formattedText.Width / 2,
                                     (end.Y - formattedText.Height - PADDING / 2));
            dc.DrawText(formattedText, endPos);
        }

        private void DrawVerticalLine(DrawingContext dc, Point start, Point end)
        {
            dc.DrawLine(LinePen, start, end);
            DrawVerticalScaleLines(dc, start);
            DrawUpArrow(dc, LineBrush, end);
            DrawItemCountText(dc, start, end);
        }

        private void DrawVerticalScaleLines(DrawingContext dc, Point start)
        {
            Point vPt1 = new Point(start.X - SCALE_HALF_LEN, start.Y);
            Point vPt2 = new Point(start.X + SCALE_HALF_LEN, start.Y);
            for (int i = 0; i < V_SCALE_COUNT; i++)
            {
                vPt1.Y -= SCALE_VERTICAL_INTERVAL;
                vPt2.Y -= SCALE_VERTICAL_INTERVAL;
                dc.DrawLine(LinePen, vPt1, vPt2);
            }
        }

        private void DrawItemCountText(DrawingContext dc, Point start, Point end)
        {
            DrawItemCountTextAlongYAxis(dc, start);
            DrawYAxisTitle(dc, end);
        }

        private void DrawItemCountTextAlongYAxis(DrawingContext dc, Point start)
        {
            double base_x = start.X - SCALE_HALF_LEN * 2;
            double y = start.Y;
            Point pos = new Point(base_x, y);
            int itemCount = 0;
            int UNIT_COUNT = WorkItemCountUnit;
            for (int i = 0; i < V_SCALE_COUNT; i++)
            {
                itemCount += UNIT_COUNT;
                FormattedText countText = GetFormattedText(itemCount.ToString());
                pos.X = base_x - countText.Width;
                y -= SCALE_VERTICAL_INTERVAL;
                pos.Y = y - (countText.Height / 2);
                dc.DrawText(countText, pos);
            }
        }

        private void DrawYAxisTitle(DrawingContext dc, Point end)
        {
            string title = YAxisTitle();
            FormattedText formattedText = GetFormattedText(title);
            Point endPos = new Point((end.X + PADDING), end.Y);
            dc.DrawText(formattedText, endPos);
        }

        private string YAxisTitle()
        {
            switch (CurrentChartType)
            {
                case MetricChartType.CycleTimeDistribution:
                    return Properties.Metrics_DistributionLabel;
                case MetricChartType.TeamVelocity:
                    return Properties.Metrics_StoryPointsLabel;
                default:
                    return Properties.Metrics_ItemsLabel;
            }
        }
    }
}
