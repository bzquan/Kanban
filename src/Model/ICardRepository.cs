using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.Runtime.CompilerServices;
using MongoDB.Bson;

namespace Kanban.Model
{
    public interface ICardRepository
    {
        Task<List<Card>> GetCards(Board board, WorkState cardWorkState, string cardFilter, Repository.OnBoardFilter onBoardFilter);
        Task<List<Card>> GetCards(Board board, List<ObjectId> cardIDs);
        Task<UpdateResult> Update<T>(Card card, T propertyValue, [CallerMemberName] string propertyName = "");
        Task<DeleteResult> Delete(Card card);
        Task<DeleteResult> DeleteNotStartedCards();
        bool ExistCards(Board board, Repository.ProcessStep step);
    }
}
