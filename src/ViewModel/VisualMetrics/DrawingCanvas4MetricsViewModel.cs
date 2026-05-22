using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

using Kanban.Util;
using Kanban.Model;

namespace Kanban.ViewModel
{
    public class MetricsChartViewModel
    {
        private VisualThroughputBuilder m_VisualCycleTimeBuilder = new VisualThroughputBuilder();
        private VisualCFDBuilder m_VisualCFDBuilder = new VisualCFDBuilder();
        private VisualCycleTimeDistributionBuilder m_VisualCycleTimeDistributionBuilder = new VisualCycleTimeDistributionBuilder();
        private VisualBugFixAndBlockedBuilder m_VisualBugsAndBlockedBuilder = new VisualBugFixAndBlockedBuilder();
        private VisualTeamVelocityBuilder m_VisualTeamVelocityBuilder = new VisualTeamVelocityBuilder();

        private IBoardRepository m_BoardRepository;
        private ICardRepository m_CardRepository;
        private IActivityRepository m_ActivityRepository;

        public MetricsChartViewModel(IAppSettings appSettings, IViewModelProperties properties, IBoardRepository boardRepository, ICardRepository cardRepository, IActivityRepository activityRepository)
        {
            MetricChartShowType = appSettings.LastUsedMetricChartType;
            AbstractVisualMetrics.Properties = properties;
            m_BoardRepository = boardRepository;
            m_CardRepository = cardRepository;
            m_ActivityRepository = activityRepository;
        }

        public MetricChartType MetricChartShowType { get; set; } = MetricChartType.CumulativeFlowDiagram;

        public Board CurrentBoard { get; set; }

        public async Task<List<AbstractVisualMetrics>> CreateMetricsGraph()
        {
            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();

            visuals.Add(new VisualXYGridChart(MetricChartShowType));
            visuals.AddRange(await CreateMetricsGraph(MetricChartShowType));

            return visuals;
        }

        private async Task<List<AbstractVisualMetrics>> CreateMetricsGraph(MetricChartType chartType)
        {
            if (CurrentBoard == null)
            {
                return new List<AbstractVisualMetrics>();
            }

            BoardHistoryBuilder boardHistoryBuilder = new BoardHistoryBuilder(CurrentBoard.BoardModel, AbstractVisualMetrics.FromDate4Metrics, GetEndOfToday(), m_BoardRepository, m_CardRepository, m_ActivityRepository);
            BoardHistory history = await boardHistoryBuilder.GetBoardHistory();
            switch (chartType)
            {
                case MetricChartType.Throughput:
                    return CreateThroughputChart(history);
                case MetricChartType.CumulativeFlowDiagram:
                    return CreateCFDChart(history);
                case MetricChartType.CycleTimeDistribution:
                    return CreateCycleTimeDistributionChart(history);
                case MetricChartType.BugsAndBlocked:
                    return CreateBugsAndBlockedChart(history);
                case MetricChartType.TeamVelocity:
                    return CreateTeamVelocityChart(history);
                default:
                    return new List<AbstractVisualMetrics>();
            }
        }

        private DateTime GetEndOfToday()
        {
            DateTime now = DateTime.Now;
            DateTime today = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);

            return today;
        }

        private List<AbstractVisualMetrics> CreateThroughputChart(Model.BoardHistory history)
        {
            Model.ThroughputMetrics metrics = new Model.ThroughputMetrics(history);
            return m_VisualCycleTimeBuilder.CreateThroughputChart(metrics);
        }

        private List<AbstractVisualMetrics> CreateCFDChart(Model.BoardHistory history)
            => m_VisualCFDBuilder.CreateCFD(history);

        private List<AbstractVisualMetrics> CreateCycleTimeDistributionChart(Model.BoardHistory history)
        {
            Model.CycleTimeMetrics cycleTimeMetrics = new Model.CycleTimeMetrics(history);
            return m_VisualCycleTimeDistributionBuilder.CreateCycleTimeDistributions(cycleTimeMetrics);
        }

        private List<AbstractVisualMetrics> CreateBugsAndBlockedChart(Model.BoardHistory history)
            => m_VisualBugsAndBlockedBuilder.CreateBugsAndBlocked(history);

        private List<AbstractVisualMetrics> CreateTeamVelocityChart(Model.BoardHistory history)
            => m_VisualTeamVelocityBuilder.CreateTeamVelocity(history);
    }
}
