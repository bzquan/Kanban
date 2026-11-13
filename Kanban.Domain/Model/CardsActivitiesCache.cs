using System;
using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;

namespace Kanban.Model
{
    /// <summary>
    /// Cards and their activities will be set by BoardHistoryBuilder and then.
    /// Board page will use this cache to calculate worked days of a card.
    ///
    /// 実装は ICardsActivitiesCache 経由の間接参照。WPF は既定の共有インスタンス(従来動作)を、
    /// サーバー(MetricsQueryService)は計算単位で new したインスタンスを Current に差し替えて
    /// 並行リクエスト間の汚染を防ぐ。既存の静的利用箇所(BoardHistory.cs / CycleTimeMetrics.cs /
    /// CardMetrics.cs / ThroughputMetrics.cs / BoardHistoryBuilder.cs)は無変更で動く。
    /// </summary>
    public static class CardsActivitiesCache
    {
        public static Kanban.Services.ICardsActivitiesCache Current { get; set; }
            = new Kanban.Services.CardsActivitiesCacheInstance();

        public static List<Card> Cards
        {
            get { return Current.Cards; }
            set { Current.Cards = value; }
        }

        public static List<Activity> ActivitiesForCards
        {
            get { return Current.ActivitiesForCards; }
            set { Current.ActivitiesForCards = value; }
        }

        public static bool Cached(Card card) => Current.Cached(card);

        public static Card GetCard(ObjectId cardID) => Current.GetCard(cardID);

        public static List<Card> GetCards(HashSet<ObjectId> cardIDs) => Current.GetCards(cardIDs);

        public static List<Activity> GetActivities(Card card) => Current.GetActivities(card);

        /// <summary>
        /// Get card activities of which StateChangedDate is begore the endDate(including)
        /// </summary>
        public static List<Activity> GetActivities(Card card, DateTime endDate) => Current.GetActivities(card, endDate);

        public static List<Activity> GetActivities(List<ObjectId> cardIDs) => Current.GetActivities(cardIDs);
    }
}
