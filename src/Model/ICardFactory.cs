using MongoDB.Bson;
using System.Threading.Tasks;

namespace Kanban.Model
{
    public interface ICardFactory
    {
        Task<Card> CreateCard(ObjectId boardID, WorkState workState);
    }
}
