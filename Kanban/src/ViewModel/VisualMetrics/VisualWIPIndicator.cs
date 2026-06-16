using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Kanban.Model;

namespace Kanban.ViewModel
{
    public class VisualWIPIndicator : AbstractVisualMetrics
    {
        static readonly Brush LineBrush = Brushes.Red;
        static readonly Pen LinePen = new Pen(LineBrush, 2);

        public VisualWIPIndicator(WIPInfo WIP, Point top, Point bottom)
        {
            using (DrawingContext dc = base.RenderOpen())
            {
                DrawWIPIndicator(dc, WIP, top, bottom, LineBrush, LinePen);
            }
        }

        public static void DrawWIPIndicator(DrawingContext dc, Point top, Point bottom, Brush lineBrush, Pen linePen)
        {
            DrawWIPIndicator(dc, new WIPInfo { MaxWIP = 0 }, top, bottom, lineBrush, linePen);
        }

        private static void DrawWIPIndicator(DrawingContext dc, WIPInfo WIP, Point top, Point bottom, Brush lineBrush, Pen linePen)
        { 
            dc.DrawLine(linePen, top, bottom);
            DrawUpArrow(dc, lineBrush, top);
            DrawDownArrow(dc, lineBrush, bottom);

            if (WIP.MaxWIP > 0)
            {
                string info = "WIP(" + WIP.MaxWIP + ")";
                FormattedText formattedText = GetFormattedText(info, Brushes.Black, emSize: 12);
                double x = top.X - (formattedText.Width / 2.0);
                double y = bottom.Y - (bottom.Y - top.Y) / 2.0;
                Point pos = new Point(x, y);
                dc.DrawText(formattedText, pos);
            }
        }
    }
}
