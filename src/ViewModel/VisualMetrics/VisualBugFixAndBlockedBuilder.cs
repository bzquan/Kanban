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
    public class VisualBugFixAndBlockedBuilder
    {
        BoardHistory m_BoardHistory;

        public List<AbstractVisualMetrics> CreateBugsAndBlocked(BoardHistory boardHistory)
        {
            m_BoardHistory = boardHistory;
            CardsCountByDays bugfixItems = CreateBugItems();
            CardsCountByDays blockedItems = CreateBlockedItems();

            List<AbstractVisualMetrics> visuals = new List<AbstractVisualMetrics>();
            visuals.Add(CreateBlockedItemViual(blockedItems, bugfixItems));
            visuals.Add(CreateBugFixViual(bugfixItems));

            return visuals;
        }

        private CardsCountByDays CreateBugItems()
        {
            CardsCountByDays items = new CardsCountByDays(VisualXYGridChart.ItemCountUnit4BugsAndBlocked);
            foreach (DateTime day in EachDay(items.FromDate, items.ToDate))
            {
                List<Model.Card> cards = m_BoardHistory.AllCardsByDate(day);
                int numOfBugFixCards = cards.Count(x => x.CardType == Repository.CardType.BugFix);
                items.SetItemCount(day, numOfBugFixCards);
            }

            return items;
        }

        private CardsCountByDays CreateBlockedItems()
        {
            CardsCountByDays items = new CardsCountByDays(VisualXYGridChart.ItemCountUnit4BugsAndBlocked);

            foreach (DateTime day in EachDay(items.FromDate, items.ToDate))
            {
                List<Model.Card> cards = m_BoardHistory.AllCardsByDate(day);

                int numOfBlockedCards = cards.Count(x => IsBocked(x, day));
                items.SetItemCount(day, numOfBlockedCards);
            }

            return items;
        }

        private bool IsBocked(Model.Card card, DateTime day)
        {
            List<Activity> activities = CardsActivitiesCache.GetActivities(card);
            Activity blockedOnActivity = activities.LastOrDefault(x => x.WorkState.IsBlocked &&
                                                                       x.StateChangedDate.Date <= day);
            if (blockedOnActivity == null) return false;

            Activity unblockedOnActivity = activities.FirstOrDefault(x => x.WorkState.IsBlocked == false &&
                                                                          x.StateChangedDate > blockedOnActivity.StateChangedDate &&
                                                                          IsStateChangedDateBeforeDay(x.StateChangedDate, day));

            return (unblockedOnActivity == null);
        }

        private bool IsStateChangedDateBeforeDay(DateTime stateChangedDate, DateTime day)
        {
            if (day == DateTime.Now.Date)
            {
                return stateChangedDate < DateTime.Now;
            }
            else
            {
                return stateChangedDate.Date < day;
            }
        }

        private AbstractVisualMetrics CreateBlockedItemViual(CardsCountByDays blockedItems, CardsCountByDays bugfixItems)
        {
            blockedItems.SetBaseItemCount(bugfixItems);
            return new VisualBlocked(blockedItems, bugfixItems.GetPointInReverseOrder());
        }

        private AbstractVisualMetrics CreateBugFixViual(CardsCountByDays bugfixItems)
        {
            Point[] basePoints = new Point[2];
            basePoints[0] = VisualXYGridChart.EndPos;
            basePoints[1] = VisualXYGridChart.StartPos;
            return new VisualBugs(bugfixItems, basePoints);
        }
    }
}
