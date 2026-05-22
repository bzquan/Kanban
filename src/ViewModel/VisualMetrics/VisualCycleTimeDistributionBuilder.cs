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
    public class VisualCycleTimeDistributionBuilder
    {
        CycleTimeMetrics m_CycleTimeMetrics;

        public List<AbstractVisualMetrics> CreateCycleTimeDistributions(CycleTimeMetrics cycleTimeMetrics)
        {
            m_CycleTimeMetrics = cycleTimeMetrics;
            SummarizeByWeekly();

            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();

            List<PlotPoint4CycleTime> cycleTimeDistributions = CreateCycleTimeDistribution();
            foreach (PlotPoint4CycleTime plotPoint in cycleTimeDistributions)
            {
                if (plotPoint.IsValid())
                {
                    visuals.Add(new VisualCycleTimeDistribution(plotPoint));
                }
            }

            visuals.Add(CreateSummaryTextInfo(cycleTimeMetrics.Steps4Metrics, cycleTimeDistributions));

            return visuals;
        }

        private void SummarizeByWeekly()
        {
            List<DateTime> dates4SummarizeCycleTimes = new List<DateTime>();
            DateTime metricsDate = AbstractVisualMetrics.FromDate4Metrics;
            for (int i = 0; i < AbstractVisualMetrics.H_SCALE_COUNT; i++)
            {
                metricsDate = metricsDate.AddDays(AbstractVisualMetrics.ONE_WEEK);
                dates4SummarizeCycleTimes.Add(metricsDate);
            }

            m_CycleTimeMetrics.SummarizeByWeekly(dates4SummarizeCycleTimes);
        }

        private VisualTextInfo CreateSummaryTextInfo(IEnumerable<Repository.ProcessStep> steps4Metrics, List<PlotPoint4CycleTime> cycleTimeDistributions)
        {
            int averageCycleTime = m_CycleTimeMetrics.GetWeeklyAverageCycleTime();
            string cycleTimeText = AbstractVisualMetrics.Properties.Metrics_AveCycleTime + averageCycleTime + AbstractVisualMetrics.Properties.Metrics_FormatLabelDay;
            string averageDistributionText = GetAverageCycleTimeTextInfo(steps4Metrics, cycleTimeDistributions);

            string summaryInfo = cycleTimeText;
            if (averageDistributionText != null)
                summaryInfo += " (" + AbstractVisualMetrics.Properties.Metrics_DistributionLabel1 + " - " + averageDistributionText + ")";
            VisualTextInfo textInfo = new VisualTextInfo(summaryInfo, Brushes.Blue, fontSize: 14);

            return textInfo;
        }

        private string GetAverageCycleTimeTextInfo(IEnumerable<Repository.ProcessStep> steps4Metrics, List<PlotPoint4CycleTime> cycleTimeDistributions)
        {
            List<string> percentageList = new List<string>();
            foreach (Repository.ProcessStep step in steps4Metrics)
            {
                int percentage = m_CycleTimeMetrics.GetAverageCycleTime(step);
                if (percentage > 0)
                {
                    percentageList.Add(step.TrimmedName + ": " + percentage + "%");
                }
            }

            if (percentageList.Count > 0)
                return percentageList.Aggregate((current, next) => current + ", " + next);
            else
                return null;
        }

        private List<PlotPoint4CycleTime> CreateCycleTimeDistribution()
        {
            List<PlotPoint4CycleTime> plotPoints = new List<PlotPoint4CycleTime>();

            DateTime metricsDate = AbstractVisualMetrics.FromDate4Metrics;
            Point startPos = VisualXYGridChart.StartPos;
            double x = startPos.X;
            for (int i = 0; i < AbstractVisualMetrics.H_SCALE_COUNT; i++)
            {
                x += AbstractVisualMetrics.SCALE_HORIZONTAL_INTERVAL;
                metricsDate = metricsDate.AddDays(AbstractVisualMetrics.ONE_WEEK);
                CycleTimeDistribution cycleTimeDistribution = m_CycleTimeMetrics.GetCycleTimeDistributionByWeekly(metricsDate);
                PlotPoint4CycleTime plotPoint4CycleTime =
                                        new PlotPoint4CycleTime(new Point(x, startPos.Y), cycleTimeDistribution);
                plotPoints.Add(plotPoint4CycleTime);
            }

            return plotPoints;
        }
    }
}
