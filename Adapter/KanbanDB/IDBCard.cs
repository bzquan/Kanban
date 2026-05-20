using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MongoDB.Bson;

namespace Kanban.Repository
{
    public enum OnBoardFilter { OnFrontBoard, OnBackBoard, All}

    public struct CardRetrieveCondition
    {
        public CardRetrieveCondition(WorkState cardWorkState, string cardFilter, OnBoardFilter boardType)
        {
            CardWorkState = cardWorkState;
            CardFilter = cardFilter;
            BoardType = boardType;
        }

        public WorkState CardWorkState { get; }
        public string CardFilter { get; }
        public OnBoardFilter BoardType { get; }
    }

    public interface IDBCard
    {
        Task Insert(Card card);
        Task<List<Card>> GetCards(); // note: no async here
        Task<List<Card>> GetCards(Board board, CardRetrieveCondition retrieveCondition);
        Task<List<Card>> GetCards(Board board);
        Task<List<Card>> GetCards(Board board, List<ObjectId> cardIDs);
        Task<UpdateResult> Update<T>(Card card, string propertyName, T propertyValue);
        Task<DeleteResult> DeleteCards(ObjectId board_id);
        Task<DeleteResult> Delete(Card card);
        Task<DeleteResult> DeleteNotStartedCards();
        UpdateResult ExecMoveCards(Board board, int fromSeqNo, int toSeqNo, bool forward);
        UpdateResult ChangeCardsSeqNo(Board board, int fromSeqNo, int toSeqNo);
        bool ExistCards(Board board, Repository.ProcessStep step);
        IMongoCollection<Card> CardsCollection { get; }
        void DropCards();
    }
}
