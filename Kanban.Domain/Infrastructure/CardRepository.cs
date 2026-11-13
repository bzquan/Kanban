using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Kanban.Infrastructure
{
    public class CardRepository : Model.ICardRepository
    {
        Repository.IDBCard m_DBCards;

        public CardRepository(Repository.IDBCard dbCards)
        {
            m_DBCards = dbCards;
        }

        public async Task<List<Model.Card>> GetCards(Model.Board board, Model.WorkState cardWorkState, string cardFilter, Repository.OnBoardFilter onBoardFilter)
        {
            Repository.WorkState workState = new Repository.WorkState { ProcessStepSeqNo = cardWorkState.ProcessStepSeqNo, IsWIP = cardWorkState.IsWIP };
            Repository.CardRetrieveCondition cardRetrieveCondition = new Repository.CardRetrieveCondition(workState, cardFilter, onBoardFilter);
            List<Repository.Card> cards = await m_DBCards.GetCards(board.BoardOfRepository, cardRetrieveCondition);
            List<Model.Card> cardList = new List<Model.Card>();
            cards.ForEach(card => cardList.Add(new Model.Card(card)));
            cardList.ForEach(x => x.Board = board);

            await AdjustCardSeqNo(cardList);
            return cardList;
        }

        private async Task AdjustCardSeqNo(List<Model.Card> cards)
        {
            for (int seqNo = 0; seqNo < cards.Count; seqNo++)
            {
                Model.Card card = cards[seqNo];
                if (card.SeqNo != seqNo)
                {
                    card.CardOfRepository.SeqNo = seqNo;
                    await m_DBCards.Update(card.CardOfRepository, nameof(card.CardOfRepository.SeqNo), seqNo);
                }
            }
        }

        public async Task<List<Model.Card>> GetCards(Model.Board board, List<ObjectId> cardIDs)
        {
            List<Repository.Card> cards = await m_DBCards.GetCards(board.BoardOfRepository, cardIDs);
            List<Model.Card> cardList = new List<Model.Card>();
            cards.ForEach(card => cardList.Add(new Model.Card(card)));
            cardList.ForEach(x => x.Board = board);

            return cardList;
        }

        public async Task<UpdateResult> Update<T>(Model.Card card, T propertyValue, [CallerMemberName] string propertyName = "")
        {
            return await m_DBCards.Update(card.CardOfRepository, propertyName, propertyValue);
        }

        public async Task<DeleteResult> Delete(Model.Card card)
        {
            return await m_DBCards.Delete(card.CardOfRepository);
        }

        public async Task<DeleteResult> DeleteNotStartedCards()
        {
            return await m_DBCards.DeleteNotStartedCards();
        }

        public bool ExistCards(Model.Board board, Repository.ProcessStep step)
        {
            return m_DBCards.ExistCards(board.BoardOfRepository, step);
        }
    }
}
