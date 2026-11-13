using Kanban.Model;
using MongoDB.Bson;

namespace Kanban.Services;

/// <summary>
/// CardsActivitiesCache のインスタンス版契約。
/// サーバー側は計算単位で new したインスタンスを CardsActivitiesCache.Current にバインドして
/// 並行リクエスト間の汚染を防ぐ(設計書リスク表「放置は不可」項目)。
/// </summary>
public interface ICardsActivitiesCache
{
    List<Card> Cards { get; set; }
    List<Activity> ActivitiesForCards { get; set; }
    bool Cached(Card card);
    Card GetCard(ObjectId cardID);
    List<Card> GetCards(HashSet<ObjectId> cardIDs);
    List<Activity> GetActivities(Card card);
    List<Activity> GetActivities(Card card, DateTime endDate);
    List<Activity> GetActivities(List<ObjectId> cardIDs);
}

/// <summary>既存静的 CardsActivitiesCache の実装をそのまま持ち込むインスタンス版(ロジック同一)。</summary>
public class CardsActivitiesCacheInstance : ICardsActivitiesCache
{
    public List<Card> Cards { get; set; } = new List<Card>();
    public List<Activity> ActivitiesForCards { get; set; } = new List<Activity>();

    public bool Cached(Card card)
    {
        Card cachedCard = Cards.FirstOrDefault(x => x._id == card._id);
        return (cachedCard != null);
    }

    public Card GetCard(ObjectId cardID)
    {
        return Cards.FirstOrDefault(x => x._id == cardID);
    }

    public List<Card> GetCards(HashSet<ObjectId> cardIDs)
    {
        return Cards.FindAll(x => cardIDs.Contains(x._id));
    }

    public List<Activity> GetActivities(Card card)
    {
        return ActivitiesForCards.Where(x => x.CardID == card._id).ToList();
    }

    /// <summary>StateChangedDate が endDate 以前(含む)の活動。</summary>
    public List<Activity> GetActivities(Card card, DateTime endDate)
    {
        return ActivitiesForCards
                .Where(x => (x.CardID == card._id) &&
                            (x.StateChangedDate.Date <= endDate))
                .ToList();
    }

    public List<Activity> GetActivities(List<ObjectId> cardIDs)
    {
        return ActivitiesForCards
                .Where(x => cardIDs.Contains(x.CardID))
                .ToList();
    }
}
