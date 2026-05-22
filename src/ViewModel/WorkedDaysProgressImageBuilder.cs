using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kanban.ViewModel
{
    static class WorkedDaysProgressImageBuilder
    {
        const double length = 10;
        static readonly Pen RectPen = new Pen(new SolidColorBrush(Colors.Black), 1);

        public static DrawingImage GetWorkedDaysImage(int workedDays, int thresholdDays, List<GradientStop> gradient4ThresholdDays, int maxDays)
        {
            return DrawWorkedDaysImage(workedDays, thresholdDays, maxDays, gradient4ThresholdDays);
        }

        private static DrawingImage DrawWorkedDaysImage(int fillRectCount, int thresholdCount, int maxRectCount, List<GradientStop> gradient4ThresholdDays)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                DrawRectWinthinThreshold(dc, fillRectCount, thresholdCount, gradient4ThresholdDays);
                DrawRectOutOfThreshold(dc, fillRectCount, thresholdCount, maxRectCount);
                DrawRectOutline(dc, maxRectCount);
            }

            return new DrawingImage(visual.Drawing);
        }

        private static void DrawRectWinthinThreshold(DrawingContext dc, int fillRectCount, int thresholdCount, List<GradientStop> gradient4ThresholdDays)
        {
            if (fillRectCount == 0) return;

            int withinThresholdRectCount = Math.Min(fillRectCount, thresholdCount);
            float maxGradientOffset = (float)withinThresholdRectCount / (float)thresholdCount;
            LinearGradientBrush linearGradientBrush = CreateLinearGradientBrush(gradient4ThresholdDays, maxGradientOffset);
            Rect rect = new Rect(0, 0, length * withinThresholdRectCount, length);
            dc.DrawRectangle(linearGradientBrush, null, rect);
        }

        private static LinearGradientBrush CreateLinearGradientBrush(List<GradientStop> gradient4ThresholdDays, float maxGradientOffset)
        {
            LinearGradientBrush linearGradientBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };

            var gradientStopInRange = gradient4ThresholdDays
                                        .Where(x => x.Offset <= maxGradientOffset);
            foreach (GradientStop g in gradientStopInRange)
            {
                linearGradientBrush.GradientStops.Add(g);
            }
            return linearGradientBrush;
        }

        private static void DrawRectOutOfThreshold(DrawingContext dc, int fillRectCount, int thresholdCount, int maxRectCount)
        {
            int outOfThresholdCount = Math.Max(fillRectCount - thresholdCount, 0);
            int outOfThresholdRectCount = Math.Min(maxRectCount - thresholdCount, outOfThresholdCount);
            if (outOfThresholdRectCount > 0)
            {
                int withinThresholdRectCount = Math.Min(fillRectCount, thresholdCount);
                Rect rect = new Rect(length * withinThresholdRectCount, 0, length * outOfThresholdRectCount, length);
                dc.DrawRectangle(Brushes.DeepPink, null, rect);
            }
        }

        private static void DrawRectOutline(DrawingContext dc, int rectCount)
        {
            Rect rect = new Rect(0, 0, length, length);
            for (int i = 0; i < rectCount; i++)
            {
                dc.DrawRectangle(null, RectPen, rect);
                rect.X += length;
            }
        }
    }
}
