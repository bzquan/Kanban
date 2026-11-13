using System;
using System.Collections.Generic;
using System.Linq;

using MongoDB.Bson;

namespace Kanban.Model
{
    public class ThroughputMetrics
    {
        BoardHistory m_BoardHistory;
        BoardHistoryWeeklyDecorator m_BoardHistoryWeeklyDecorator;

        public bool IsValid() => m_BoardHistoryWeeklyDecorator.HaveCompletedCards;
        public Repository.ProcessStep ProcessStep { get; set; }
        public int CardCountInWIP { get; set; } = 0;
        public double RealAverageThroughputPerWeek { get; set; } = 0.0;

        public ThroughputMetrics(BoardHistory boardHistory)
        {
            m_BoardHistory = boardHistory;
            m_BoardHistoryWeeklyDecorator = new BoardHistoryWeeklyDecorator(boardHistory);

            ProcessHistory lastProcessHistory = boardHistory.ProcessHistories.Last();
            if (lastProcessHistory == null) return;

            ProcessStep = lastProcessHistory.Process;
            RealAverageThroughputPerWeek = lastProcessHistory.ThroughputPerWeek();
            CardCountInWIP = CountCardCountInWIP(boardHistory, ProcessStep);
        }

        public CardCompletionPerformance ThroughputByWeek(DateTime toDate)
        {
            HashSet<ObjectId> doneCards = m_BoardHistoryWeeklyDecorator.GetDoneCardsByWeek(toDate);
            CardCompletionPerformance completedCardInfo = GetCompletedCardInfo(doneCards);

            return completedCardInfo;
        }

        private CardCompletionPerformance GetCompletedCardInfo(HashSet<ObjectId> doneCards)
        {
            CardCompletionPerformance completedCardInfo = new CardCompletionPerformance();
            foreach (ObjectId cardID in doneCards)
            {
                Card card = CardsActivitiesCache.GetCard(cardID);
                if (card != null)
                {
                    List<Activity> activities = CardsActivitiesCache.GetActivities(card);
                    int workedDays = card.GetWorkedDays(activities);
                    Repository.EstimatedWorkEffort estimate = card.EstimatedWorkEffort;
                    switch (estimate.GetWorkPerformance(workedDays))
                    {
                        case WorkPerformance.AheadOfSchedule:
                            completedCardInfo.AheadOfScheduleCount += 1;
                            break;
                        case WorkPerformance.Delayed:
                            completedCardInfo.DelayedCount += 1;
                            break;
                        case WorkPerformance.OnTime:
                            completedCardInfo.OnTimeCount += 1;
                            break;
                    }
                }
            }

            completedCardInfo.ItemCount = doneCards.Count;

            return completedCardInfo;
        }

        private static int CountCardCountInWIP(BoardHistory boardHistory, Repository.ProcessStep lastProcess)
        {
            int cardsOnBoard = 0;
            foreach (ProcessHistory processHistory in boardHistory.ProcessHistories)
            {
                if (processHistory.Process != lastProcess)
                {
                    // Accumulate cards of last date(current date)
                    CardsByDate cardsByDate = processHistory.CardsByDateList.Last();
                    cardsOnBoard += cardsByDate.CardCount;
                }
                else
                {
                    // Accumulate WIP cards for last process 
                    cardsOnBoard += processHistory.CardsInWIPOfLastProcess;
                }
            }

            return cardsOnBoard;
        }
    }
}
