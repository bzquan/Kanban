using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kanban.ViewModel
{
    public class VisualTeamVelocity : AbstractVisualMetrics
    {
        const double PEN_THICKNESS = 2.0;
        static Pen GraphLinePen = new Pen(Brushes.Green, PEN_THICKNESS);

        public VisualTeamVelocity(List<PlotPoint4TeamVelocity> velocityPoints)
        {
            List<Point> points = MakePolygon(velocityPoints);
            using (DrawingContext dc = base.RenderOpen())
            {
                DrawPolygonOrPolyline(dc, null, GraphLinePen, points.ToArray(), FillRule.EvenOdd, false);
                DrawStoryPoints(dc, velocityPoints);
            }
        }

        private List<Point> MakePolygon(List<PlotPoint4TeamVelocity> velocityPoints)
        {
            List<Point> points = new List<Point>();
            velocityPoints.ForEach(p => points.Add(p.Pos));
            return points;
        }

        private void DrawStoryPoints(DrawingContext dc, List<PlotPoint4TeamVelocity> velocityPoints)
        {
            foreach (var p in velocityPoints)
            {
                DrawTextAtPoint(dc, Brushes.Blue, p.Velocity.ToString(), p.Pos);
            }
        }
    }
}
