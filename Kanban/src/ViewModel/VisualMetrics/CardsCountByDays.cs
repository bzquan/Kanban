using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using static Kanban.Util.DateTimeUtil;
using Kanban.Model;

namespace Kanban.ViewModel
{
    public struct ItemNum
    {
        public int ItemCount { get; set; }
        public int BaseItemCount { get; set; }

        public int TotalItemCount => ItemCount + BaseItemCount;
    }

    public class CardsCountByDays
    {
        private Dictionary<DateTime, ItemNum> ItemsByDay { get; set; } = new Dictionary<DateTime, ItemNum>();
        private double XUnit { get; set; }
        private double YUnit { get; set; }

        public DateTime FromDate { get; private set; }
        public DateTime ToDate { get; private set; }

        public CardsCountByDays(double yUnit)
        {
            YUnit = yUnit;

            FromDate = BoardHistory.FromDate;
            ToDate = BoardHistory.ToDate;

            Initialize();
        }

        private void Initialize()
        {
            XUnit = CalcXUnit();

            double x = VisualXYGridChart.StartPos.X;
            foreach (DateTime day in EachDay(FromDate, ToDate))
            {
                ItemsByDay[day] = new ItemNum();
            }
        }

        private double CalcXUnit()
        {
            int days = (ToDate - FromDate).Days;
            return (AbstractVisualMetrics.H_SCALE_COUNT * AbstractVisualMetrics.SCALE_HORIZONTAL_INTERVAL) / days;
        }

        public void SetItemCount(DateTime day, int count)
        {
            ItemsByDay[day] = new ItemNum { ItemCount = count, BaseItemCount = ItemsByDay[day].BaseItemCount };
        }

        public void SetItemCount(DateTime day, int count, int baseCount)
        {
            ItemsByDay[day] = new ItemNum { ItemCount = count, BaseItemCount = baseCount };
        }

        public void SetBaseItemCount(CardsCountByDays baseItems)
        {
            foreach (DateTime day in EachDay(FromDate, ToDate))
            {
                ItemNum itemNum = ItemsByDay[day];
                ItemsByDay[day] = new ItemNum { ItemCount = ItemsByDay[day].ItemCount, BaseItemCount = baseItems.GetItemCount(day) };
            }
        }

        public int GetItemCount(DateTime day) => ItemsByDay[day].ItemCount;

        public Point[] GetPoint()
        {
            Point[] points = new Point[ItemsByDay.Count];
            int i = 0;
            double x = VisualXYGridChart.StartPos.X;
            foreach (DateTime day in EachDay(FromDate, ToDate))
            {
                points[i++] = new Point(x, AbstractVisualMetrics.CalculateYCoordinate(ItemsByDay[day].TotalItemCount, YUnit));
                x += XUnit;
            }
            return points;
        }

        public Point[] GetPointInReverseOrder()
        {
            Point[] points = GetPoint();
            Array.Reverse(points);

            return points;
        }

        public Point GetPoint(DateTime day)
        {
            ItemNum itemNum;
            if (ItemsByDay.TryGetValue(day, out itemNum))
            {
                int days = (day - FromDate).Days;
                double x = VisualXYGridChart.StartPos.X + XUnit * days;
                double y = AbstractVisualMetrics.CalculateYCoordinate(itemNum.TotalItemCount, YUnit);

                return new Point(x, y);
            }
            else
            {
                return VisualXYGridChart.StartPos;
            }
        }
    }
}
