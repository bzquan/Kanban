using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kanban.Model;
using System.Windows.Media;
using System.Windows;

namespace Kanban.ViewModel
{
    public class VisualTeamVelocityBuilder
    {
        public List<AbstractVisualMetrics> CreateTeamVelocity(BoardHistory boardHistory)
        {
            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();

            List<PlotPoint4TeamVelocity> velocityPoints = CreatePlotPoint4TeamVelocity(boardHistory.Velocity);
            visuals.Add(new VisualTeamVelocity(velocityPoints));
            visuals.Add(CreateTeamVelocitySummaryInfo(boardHistory.TotalDays(),
                                                      boardHistory.AverageVelocity4Every2Weeks,
                                                      boardHistory.HasUnknownStoryPoints));

            return visuals;
        }

        private List<PlotPoint4TeamVelocity> CreatePlotPoint4TeamVelocity(List<TeamVelocity> velocities)
        {
            List<PlotPoint4TeamVelocity> points = new List<PlotPoint4TeamVelocity>();

            Point startPos = VisualXYGridChart.StartPos;
            double x = startPos.X;
            foreach (TeamVelocity velocity in velocities)
            {
                int storyPoint = velocity.Velocity;
                x += AbstractVisualMetrics.SCALE_HORIZONTAL_INTERVAL;
                double y = YPosFromStoryPoints(storyPoint);
                Point p = new Point(x, y);
                points.Add(new PlotPoint4TeamVelocity() { Pos = p, Velocity = storyPoint });
            }

            return points;
        }

        private double YPosFromStoryPoints(int storyPoints)
        {
            double unit = VisualXYGridChart.StoryPointUnit4TeamVelocity;
            double offset = (AbstractVisualMetrics.SCALE_VERTICAL_INTERVAL * storyPoints) / unit;
            return (VisualXYGridChart.StartPos.Y - offset);
        }

        private VisualTextInfo CreateTeamVelocitySummaryInfo(int totalDays, int velocity, bool hasUnknownStoryPoints)
        {
            string totalMetricsDays = String.Format(AbstractVisualMetrics.Properties.Metrics_DurationOfMetrics, totalDays);
            string averageVelocity = String.Format(AbstractVisualMetrics.Properties.Metrics_Last6WeeksAveVelocity, velocity);
            StringBuilder sb = new StringBuilder();
            if (hasUnknownStoryPoints)
            {
                sb.AppendLine(AbstractVisualMetrics.Properties.Message_HasUnknownStoryPointsInReleasedItem);
            }
            sb.AppendLine(totalMetricsDays)
              .Append(averageVelocity);

            Brush brush = hasUnknownStoryPoints ? Brushes.Red : Brushes.Blue;
            return new VisualTextInfo(sb.ToString(), brush, fontSize: 12);
        }
    }
}
