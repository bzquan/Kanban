using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kanban.ViewModel
{
    public class VisualBugFixAndBlocked : AbstractVisualMetrics
    {
        private CardsCountByDays m_BugBlockedItems;
        private Point[] m_BasePoints;

        public VisualBugFixAndBlocked(CardsCountByDays points, Point[] basePoints, Brush brush)
        {
            m_BugBlockedItems = points;
            m_BasePoints = basePoints;

            Draw(brush);
        }

        private void Draw(Brush brush)
        {
            using (DrawingContext dc = base.RenderOpen())
            {
                Point[] polygon = CreatePolygon();
                DrawPolygonOrPolyline(dc, brush, null, polygon, FillRule.EvenOdd, draw_polygon: true);
                DrawItemCount(dc, brush, m_BugBlockedItems, false);
            }
        }

        private Point[] CreatePolygon()
        {
            Point[] points = m_BugBlockedItems.GetPoint();

            Point[] polygon = new Point[points.Length + m_BasePoints.Length];
            points.CopyTo(polygon, index: 0);
            m_BasePoints.CopyTo(polygon, index: points.Length);
            return polygon;
        }
    }

    public class VisualBugs : VisualBugFixAndBlocked
    {
        public VisualBugs(CardsCountByDays points, Point[] basePoints) : base(points, basePoints, Brushes.Red)
        {
        }

        public override string GetVisualToolTip(Point point) => Properties.Metrics_ItemsOfBugfix;
    }

    public class VisualBlocked : VisualBugFixAndBlocked
    {
        public VisualBlocked(CardsCountByDays points, Point[] basePoints) : base(points, basePoints, Brushes.DeepPink)
        {
        }

        public override string GetVisualToolTip(Point point) => Properties.Metrics_ItemsOfBlocked;
    }
}
