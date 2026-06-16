using MongoDB.Bson;

namespace Kanban.Model
{
    public interface ICardFactory
    {
        Task<Card> CreateCard(ObjectId boardID, WorkState workState, Card srcCard);
    }
}
