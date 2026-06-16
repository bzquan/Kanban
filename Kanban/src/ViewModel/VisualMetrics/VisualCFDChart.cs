using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;
using Kanban.Model;

namespace Kanban.ViewModel
{
    class VisualCFDChart : AbstractVisualMetrics
    {
        ProcessHistory ProcessHistory { get; set; }
        List<Point> m_Points;
        string m_ToolTip = null;

        public VisualCFDChart(ProcessCFDPoints mainPoints, ProcessCFDPoints subPoints)
        {
            ProcessHistory = mainPoints.ProcessHistory;
            m_Points = MakePolygon(mainPoints, subPoints);
            DrawCFDChart(ProcessHistory.Process.LabelColor);
        }

        public override string GetVisualToolTip(Point point)
        {
            if (m_ToolTip == null)
            {
                m_ToolTip = GetCFDInfo();
            }
            return m_ToolTip;
        }

        private string GetCFDInfo()
        {
            string trimmedProcessName = ProcessHistory.Process.TrimmedName;
            bool canCalcWIP = !ProcessHistory.IsLastProcess;
            WIPInfo WIP = canCalcWIP ? ProcessHistory.WIP : new WIPInfo { MaxWIP = 0 };

            if (ProcessHistory.IsLastProcess)
            {
                return trimmedProcessName + Properties.Metrics_DoneCumulative;
            }
            else
            {
                if (WIP.MaxWIP > 0)
                {
                    return GetMaxWIPTextInfo(WIP);
                }
                else
                {
                    return trimmedProcessName;
                }
            }
        }

        private string GetMaxWIPTextInfo(WIPInfo WIP)
        {
            StringBuilder formatter = new StringBuilder();
            formatter
                .AppendLine(Properties.Metrics_MaxWIP + " {0}")
                .AppendLine(Properties.Metrics_AveWIP + " {1:0.#}")
                .AppendLine(Properties.Metrics_OccuredDate + " {2}")
                .Append(Properties.Metrics_TotalDate + "{3}" + Properties.Metrics_FormatLabelDate);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(ProcessHistory.Process.TrimmedName)
                  .Append(String.Format(formatter.ToString(),
                                        WIP.MaxWIP,
                                        WIP.AverageWIP,
                                        WIP.MaxWIPDate.ToString(Properties.DateTime_MonthDayFormatter),
                                        WIP.WorkingDays4MaxWIP));
            return sb.ToString();
        }

        public override VisualHitInfo GetVisualHitInfo(Point hitPoint)
        {
            return new VisualCFDHitInfo(ProcessHistory, m_Points);
        }

        private List<Point> MakePolygon(ProcessCFDPoints mainPoints, ProcessCFDPoints subPoints)
        {
            List<Point> points = new List<Point>();
            mainPoints.Points.ForEach(p => points.Add(p));

            // subPointsの最後尾点から点を追加
            // The AsEnumerable needs to be there to prevent the List Reverse method from being used
            // which would reverse original list elements.
            var reversedPoints = subPoints
                                    .Points
                                    .AsEnumerable()
                                    .Reverse()
                                    .ToList();
            
            reversedPoints.ForEach(p => points.Add(p));

            return points;
        }

        private void DrawCFDChart(string colorName)
        {
            using (DrawingContext dc = base.RenderOpen())
            {
                Brush brush = Util.Util.BrushFromColorName(colorName);
                DrawPolygonOrPolyline(dc, brush, null, m_Points.ToArray(), FillRule.EvenOdd, true);
            }
        }
    }
}
