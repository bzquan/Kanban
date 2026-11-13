using System;
using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;

namespace Kanban.Model
{
    /// <summary>
    /// Cards and their activities will be set by BoardHistoryBuilder and then.
    /// Board page will use this cache to calculate worked days of a card.
    /// </summary>
    public static class CardsActivitiesCache
    {
        public static List<Card> Cards { get; set; } = new List<Card>();
        public static List<Activity> ActivitiesForCards { get; set; } = new List<Activity>();

        public static bool Cached(Card card)
        {
            Card cachedCard = Cards.FirstOrDefault(x => x._id == card._id);
            return (cachedCard != null);
        }

        public static Card GetCard(ObjectId cardID)
        {
            return Cards.FirstOrDefault(x => x._id == cardID);
        }

        public static List<Card> GetCards(HashSet<ObjectId> cardIDs)
        {
            return Cards.FindAll(x => cardIDs.Contains(x._id));
        }

        public static List<Activity> GetActivities(Card card)
        {
            List<Activity> activities = ActivitiesForCards.Where(x => x.CardID == card._id).ToList();
            return activities;
        }

        /// <summary>
        /// Get card activities of which StateChangedDate is begore the endDate(including)
        /// </summary>
        /// <param name="card"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public static List<Activity> GetActivities(Card card, DateTime endDate)
        {
            List<Activity> activities = ActivitiesForCards
                                            .Where(x => (x.CardID == card._id) &&
                                                        (x.StateChangedDate.Date <= endDate))
                                            .ToList();
            return activities;
        }

        public static List<Activity> GetActivities(List<ObjectId> cardIDs)
        {
            List<Activity> activities = ActivitiesForCards
                                            .Where(x => cardIDs.Contains(x.CardID))
                                            .ToList();
            return activities;
        }
    }
}
