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
    public class VisualThroughputBuilder
    {
        ThroughputMetrics m_Metrics;

        public List<AbstractVisualMetrics> CreateThroughputChart(ThroughputMetrics metrics)
        {
            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();
            m_Metrics = metrics;
            if (metrics.IsValid())
            {
                List<PlotPoint4Throughput> plotPoints = MakePlotPoints(m_Metrics);
                visuals.AddRange(CreateThrouputs(plotPoints));
                visuals.Add(CreateSummaryInfo());
            }

            return visuals;
        }

        private static List<PlotPoint4Throughput> MakePlotPoints(ThroughputMetrics metrics)
        {
            DateTime metricsDate = AbstractVisualMetrics.FromDate4Metrics;
            Point startPos = VisualXYGridChart.StartPos;
            double x = startPos.X;
            List<PlotPoint4Throughput> plotPoints = new List<PlotPoint4Throughput>();
            foreach (var n in Enumerable.Range(0, AbstractVisualMetrics.H_SCALE_COUNT + 1))
            {
                CardCompletionPerformance completedCardInfo = metrics.ThroughputByWeek(metricsDate);
                double y = CalculateYCoordinate(completedCardInfo.ItemCount);
                plotPoints.Add(new PlotPoint4Throughput { Pos = new Point(x, y), CompletedCardInfo = completedCardInfo });

                x += AbstractVisualMetrics.SCALE_HORIZONTAL_INTERVAL;
                metricsDate = metricsDate.AddDays(AbstractVisualMetrics.ONE_WEEK);
            }

            return plotPoints;
        }

        private static double CalculateYCoordinate(int completed_items)
        {
            double unit = VisualXYGridChart.ItemCountUnit4Throughput;
            double offset = (AbstractVisualMetrics.SCALE_VERTICAL_INTERVAL * completed_items) / unit;
            return (VisualXYGridChart.StartPos.Y - offset);
        }

        private List<AbstractVisualMetrics> CreateThrouputs(List<PlotPoint4Throughput> plotPoints)
        {
            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();
            // draw throughput from 2nd plots to avoid dirty Y axis
            foreach (var plotPoint in plotPoints.Skip(1))
            {
                visuals.Add(new VisualThroughput(m_Metrics.ProcessStep.LabelColor, plotPoint));
            }

            return visuals;
        }

        private AbstractVisualMetrics CreateSummaryInfo()
        {
            double averageThroughputPerWeek = m_Metrics.RealAverageThroughputPerWeek;
            string averageThroughputPerWeekText =
                String.Format(AbstractVisualMetrics.Properties.Metrics_AveThroughput + "{0:0.#}" + AbstractVisualMetrics.Properties.Metrics_PerWeek, averageThroughputPerWeek);

            double averageThroughput = averageThroughputPerWeek;
            double cycleTime = (averageThroughput > 0.0) ? m_Metrics.CardCountInWIP / averageThroughput : -1.0;
            string cycleTimeText = (cycleTime >= 0.0) ?
                                        String.Format(AbstractVisualMetrics.Properties.Metrics_EstimatedCycleTime + "{0:0.#}" + AbstractVisualMetrics.Properties.Metrics_Weeks, cycleTime) :
                                        AbstractVisualMetrics.Properties.Metrics_EstimatedCycleTime + AbstractVisualMetrics.Properties.Metrics_UnknownWeeks;

            StringBuilder summary = new StringBuilder();
            summary
                .AppendLine(averageThroughputPerWeekText)
                .Append(cycleTimeText);

            return new VisualTextInfo(summary.ToString(), Brushes.Blue, fontSize: 12);
        }
    }
}
