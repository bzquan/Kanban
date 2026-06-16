using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kanban.ViewModel
{
    public class VisualTextInfo : AbstractVisualMetrics
    {
        public VisualTextInfo(string info, Brush brush, double fontSize)
        {
            using (DrawingContext dc = base.RenderOpen())
            {
                FormattedText formattedText = GetFormattedText(info, brush, fontSize);
                Point pos = new Point((METRICS_CHART_WIDTH - formattedText.Width) / 2.0, PADDING * 2.0);
                dc.DrawText(formattedText, pos);
            }
        }
    }
}
