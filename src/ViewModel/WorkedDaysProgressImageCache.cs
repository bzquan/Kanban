using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

using static Kanban.Model.EstimatedWorkEffortExtension;

namespace Kanban.ViewModel
{
    public struct WorkedDaysKey
    {
        private int m_ActualWorkedDays;

        public const int MaxDays = 20;

        public int ActualWorkedDays
        {
            get { return m_ActualWorkedDays; }
            set { m_ActualWorkedDays = Math.Min(value, MaxDays); }
        }
        public Repository.EstimatedWorkEffort EstimatedWorkEffort { get; set; }
    }

    public class WorkedDaysProgressImageCache
    {
        static Dictionary<WorkedDaysKey, DrawingImage> s_WorkedDaysImages = new Dictionary<WorkedDaysKey, DrawingImage>();
        static Dictionary<Repository.EstimatedWorkEffort, List<GradientStop>> s_GradientStops = new Dictionary<Repository.EstimatedWorkEffort, List<GradientStop>>();

        static WorkedDaysProgressImageCache()
        {
            s_GradientStops[Repository.EstimatedWorkEffort.Small] =
                new List<GradientStop> { new GradientStop(Colors.LightGray, 0.0), new GradientStop(Colors.Orange, 1.0) };

            s_GradientStops[Repository.EstimatedWorkEffort.Medium] =
                new List<GradientStop> { new GradientStop(Colors.LightGray, 0.0), new GradientStop(Colors.Yellow, 0.7), new GradientStop(Colors.Orange, 0.85), new GradientStop(Colors.Orange, 1.0) };

            s_GradientStops[Repository.EstimatedWorkEffort.Large] =
                new List<GradientStop> { new GradientStop(Colors.LightGray, 0.0), new GradientStop(Colors.Yellow, 0.7), new GradientStop(Colors.Orange, 0.85), new GradientStop(Colors.Orange, 1.0) };

            s_GradientStops[Repository.EstimatedWorkEffort.LargeExtra] =
                new List<GradientStop> { new GradientStop(Colors.LightGray, 0.0), new GradientStop(Colors.Yellow, 0.7), new GradientStop(Colors.Orange, 0.85), new GradientStop(Colors.Orange, 1.0) };

            s_GradientStops[Repository.EstimatedWorkEffort.Unknown] =
                new List<GradientStop> { new GradientStop(Colors.LightGray, 0.0), new GradientStop(Colors.Orange, 1.0) };
        }

        public static DrawingImage GetWorkedDaysImage(Model.Card card, int workedDays)
        {
            WorkedDaysKey workedDaysKey = new WorkedDaysKey { ActualWorkedDays = workedDays, EstimatedWorkEffort = card.EstimatedWorkEffort };
            DrawingImage workedDaysImage;

            if (!s_WorkedDaysImages.TryGetValue(workedDaysKey, out workedDaysImage))
            {
                workedDaysImage = CreateWorkedDaysImage(card, workedDays);
                s_WorkedDaysImages.Add(workedDaysKey, workedDaysImage);
            }

            return workedDaysImage;
        }

        private static DrawingImage CreateWorkedDaysImage(Model.Card card, int workedDays)
        {
            DrawingImage workedDaysImage = WorkedDaysProgressImageBuilder.
                                                GetWorkedDaysImage(workedDays,
                                                                  card.EstimatedWorkEffort.ThresholdDays(),
                                                                  s_GradientStops[card.EstimatedWorkEffort],
                                                                  WorkedDaysKey.MaxDays);
            return workedDaysImage;
        }
    }
}
