using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

using Kanban.Model;

namespace Kanban.ViewModel
{
    class ProcessCFDPoints
    {
        public ProcessHistory ProcessHistory { get; set; }
        public List<Point> Points { get; set; } = new List<Point>();
        public void AddPoint(Point pt) => Points.Add(pt);
        public Point PointByDate(DateTime date)
        {
            
            int daysIndex = (date - ProcessHistory.FromDate).Days;
            if ((daysIndex >= 0) && (daysIndex < Points.Count))
            {
                return Points[daysIndex];
            }

            return Points[0];
        }
    }

    public class VisualCFDBuilder
    {
        BoardHistory m_BoardHistory;
        double XInterval { get; set; } = 0.0;

        public List<AbstractVisualMetrics> CreateCFD(BoardHistory boardHistory)
        {
            m_BoardHistory = boardHistory;
            UpdateXInterval();
            List<ProcessCFDPoints> processCFDPointsList = CreatePlots4AllProcesses();
            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();
            if (processCFDPointsList.Count == 0) return visuals;

            CreateCFDChartFromLastToSecondProcess(processCFDPointsList, visuals);
            CreateCFDChart4FirstProcess(processCFDPointsList, visuals);
            CreateSummaryVisuals(processCFDPointsList, visuals);

            AbstractVisualMetrics defaultHitInfo = visuals.Last(x => x is VisualCFDChart)?.GetVisualHitInfo(VisualXYGridChart.EndPos);
            if (defaultHitInfo != null)
            {
                visuals.Add(defaultHitInfo);
            }

            return visuals;
        }

        private void CreateSummaryVisuals(List<ProcessCFDPoints> processCFDPointsList, List<AbstractVisualMetrics> visuals)
        {
            bool canCalcWIP = processCFDPointsList.Count > 1;
            WIPInfo WIP = canCalcWIP ? m_BoardHistory.WIP : new WIPInfo { MaxWIP = 0 };
            if (WIP.MaxWIP > 0)
            {
                visuals.Add(CreateMAXWIPIndicator(processCFDPointsList, WIP));
            }

            visuals.Add(CreateCDFSummaryInfo(WIP));
        }

        private VisualTextInfo CreateCDFSummaryInfo(WIPInfo WIP)
        {
            string totalMetricsDays = String.Format(AbstractVisualMetrics.Properties.Metrics_DurationOfMetrics, m_BoardHistory.TotalDays());
            StringBuilder sb = new StringBuilder();
            if (WIP.MaxWIP > 0)
            {
                StringBuilder formatter = new StringBuilder();
                formatter
                    .Append(AbstractVisualMetrics.Properties.Metrics_MaxWIP + " {0}, ")
                    .AppendLine(AbstractVisualMetrics.Properties.Metrics_AveWIP + " {1:0.#}")
                    .Append(AbstractVisualMetrics.Properties.Metrics_OccuredDate + " {2}, ")
                    .Append(AbstractVisualMetrics.Properties.Metrics_TotalDate + "{3}" + AbstractVisualMetrics.Properties.Metrics_FormatLabelDate);

                sb.AppendLine(totalMetricsDays)
                      .Append(String.Format(formatter.ToString(),
                                            WIP.MaxWIP,
                                            WIP.AverageWIP,
                                            WIP.MaxWIPDate.ToString(AbstractVisualMetrics.Properties.DateTime_MonthDayFormatter),
                                            WIP.WorkingDays4MaxWIP));
            }
            else
            {
                sb.Append(totalMetricsDays);
            }

            return new VisualTextInfo(sb.ToString(), Brushes.Blue, fontSize: 12);
        }

        private VisualWIPIndicator CreateMAXWIPIndicator(List<ProcessCFDPoints> processCFDPointsList, WIPInfo WIP)
        {
            Point top = processCFDPointsList.Last().PointByDate(WIP.MaxWIPDate);
            Point bottom = processCFDPointsList.First().PointByDate(WIP.MaxWIPDate);

            return new VisualWIPIndicator(WIP, top, bottom);
        }

        private void UpdateXInterval()
        {
            int days = m_BoardHistory.TotalDays() - 1;
            XInterval = (AbstractVisualMetrics.H_SCALE_COUNT * AbstractVisualMetrics.SCALE_HORIZONTAL_INTERVAL) / days;
        }

        private static void CreateCFDChartFromLastToSecondProcess(List<ProcessCFDPoints> processCFDPointsList, List<AbstractVisualMetrics> visuals)
        {
            ProcessCFDPoints upperProcessCFDPoints = processCFDPointsList.LastOrDefault();
            // 最後尾二番目から先頭まで処理する
            foreach (ProcessCFDPoints lowerProcessCFDPoints in Enumerable.Reverse(processCFDPointsList).Skip(1))
            {
                visuals.Add(new VisualCFDChart(upperProcessCFDPoints, lowerProcessCFDPoints));
                upperProcessCFDPoints = lowerProcessCFDPoints;
            }
        }

        private static void CreateCFDChart4FirstProcess(List<ProcessCFDPoints> processCFDPointsList, List<AbstractVisualMetrics> visuals)
        {
            ProcessCFDPoints dummyCFDPoints = MakeXAxisAsDummyProcessCFDPoints(processCFDPointsList[0]);
            visuals.Add(new VisualCFDChart(processCFDPointsList[0], dummyCFDPoints));
        }

        private List<ProcessCFDPoints> CreatePlots4AllProcesses()
        {
            List<ProcessCFDPoints> processCFDPointsList = new List<ProcessCFDPoints>();
            // Processの逆順にてCFDを作成する
            foreach (ProcessHistory processHistory in Enumerable.Reverse(m_BoardHistory.ProcessHistories))
            {
                ProcessCFDPoints processCFDPoints = CreateProcessPoints(processHistory);
                processCFDPointsList.Add(processCFDPoints);
            }

            return processCFDPointsList;
        }

        private ProcessCFDPoints CreateProcessPoints(ProcessHistory processHistory)
        {
            ProcessCFDPoints processCFDPoints = new ProcessCFDPoints { ProcessHistory = processHistory };
            double x = VisualXYGridChart.StartPos.X;

            foreach (CardsByDate cardsByDate in processHistory.CardsByDateList)
            {
                double y = AbstractVisualMetrics.CalculateYCoordinate(cardsByDate.CumulativeFlowCardCount, VisualXYGridChart.ItemCountUnit4CFD);
                processCFDPoints.AddPoint(new Point(x, y));
                x += XInterval;
            }

            return processCFDPoints;
        }

        private static ProcessCFDPoints MakeXAxisAsDummyProcessCFDPoints(ProcessCFDPoints processCFDPoints)
        {
            ProcessCFDPoints dummyCFDPoints = new ProcessCFDPoints { ProcessHistory = processCFDPoints.ProcessHistory };
            dummyCFDPoints.AddPoint(VisualXYGridChart.StartPos);
            dummyCFDPoints.AddPoint(VisualXYGridChart.EndPos);
            return dummyCFDPoints;
        }
    }
}
