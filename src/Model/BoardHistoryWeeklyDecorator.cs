using System;
using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;

namespace Kanban.Model
{
    class CardsOnBoard
    {
        public DateTime Date { get; set; }
        public HashSet<ObjectId> DoneCards { get; set; }
    }

    public class BoardHistoryWeeklyDecorator
    {
        List<CardsOnBoard> m_CompletedCards = new List<CardsOnBoard>();

        public BoardHistoryWeeklyDecorator(BoardHistory boardHistory)
        {
            BoardHistory = boardHistory;

            ProcessHistory lastProcessHistory = boardHistory.ProcessHistories.Last();
            if (lastProcessHistory == null) return;

            m_CompletedCards = ExtractCardHistory(lastProcessHistory.CardsByDateList);
        }

        public BoardHistory BoardHistory { get; private set; }

        public bool HaveCompletedCards => m_CompletedCards.Count > 0;

        public HashSet<ObjectId> GetDoneCardsByWeek(DateTime toDate)
        {
            HashSet<ObjectId> doneCards = new HashSet<ObjectId>();
            foreach (DateTime date in Util.DateTimeUtil.EachDay(toDate.AddDays(-6), toDate))
            {
                CardsOnBoard cardsOnBoard = m_CompletedCards.FirstOrDefault(x => x.Date == date);
                if (cardsOnBoard != null)
                {
                    doneCards.UnionWith(cardsOnBoard.DoneCards);
                }
            }

            return doneCards;
        }

        private static List<CardsOnBoard> ExtractCardHistory(List<CardsByDate> cardsByDateList)
        {
            List<CardsOnBoard> cardsOnBoards = new List<CardsOnBoard>();
            foreach (CardsByDate cardsByDate in cardsByDateList)
            {
                CardsOnBoard cardsOnBoard =
                    new CardsOnBoard { Date = cardsByDate.Date, DoneCards = cardsByDate.Cards };
                cardsOnBoards.Add(cardsOnBoard);
            }

            return cardsOnBoards;
        }
    }
}
