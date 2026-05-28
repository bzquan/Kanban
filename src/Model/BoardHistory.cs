using MongoDB.Bson;

using static Kanban.Util.DateTimeUtil;
using static Kanban.Util.Util;

namespace Kanban.Model
{
    public struct WIPInfo
    {
        public int MaxWIP { get; set; }
        public double AverageWIP { get; set; }
        public DateTime MaxWIPDate { get; set; }
        public int WorkingDays4MaxWIP { get; set; }
    }

    public class CardsByDate
    {
        public DateTime Date { get; set; }

        public HashSet<ObjectId> Cards { get; set; } = new HashSet<ObjectId>();
        public HashSet<ObjectId> CumulativeCards { get; set; } = new HashSet<ObjectId>();

        public int CardCount { get; set; } = 0;
        public int CumulativeCardCount
        {
            get { return CumulativeCards.Count; }
        }
        public int CumulativeFlowCardCount { get; set; } = 0;
        public void AddCard(ObjectId cardID)
        {
            Cards.Add(cardID);
            CumulativeCards.Add(cardID);
            CardCount++;
        }

        public void CumulateCards(HashSet<ObjectId> cards)
        {
            CumulativeCards.UnionWith(cards);
        }
    }

    public class TeamVelocity
    {
        public TeamVelocity(int velocity, DateTime date, bool hasUnknownStoryPoints)
        {
            Velocity = velocity;
            Date = date;
            HasUnknownStoryPoints = hasUnknownStoryPoints;
        }

        public int Velocity { get; }
        public DateTime Date { get; }
        public bool HasUnknownStoryPoints { get; }
    }

    public class ProcessHistory
    {
        DateTime m_ActualFromDate { get; set; }
        public Repository.ProcessStep Process { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<CardsByDate> CardsByDateList { get; set; } = new List<CardsByDate>();
        public bool IsLastProcess { get; set; }
        public int CardsInWIPOfLastProcess { get; set; } = 0;

        public ProcessHistory(Repository.ProcessStep process, DateTime from, DateTime to, bool isLastProcess)
        {
            Process = process;
            FromDate = from;
            ToDate = to;
            m_ActualFromDate = to;
            IsLastProcess = isLastProcess;
            for (DateTime date = from; date <= to; date = date.AddDays(1))
            {
                CardsByDateList.Add(new CardsByDate { Date = date });
            }
        }

        public int ActualTotalDays()
        {
            DateTime fromDate4metrics = FromDate;
            if (fromDate4metrics < m_ActualFromDate)
            {
                fromDate4metrics = m_ActualFromDate;
            }
            return (ToDate - fromDate4metrics).Days + 1;
        }

        /// <summary>
        /// Throughtputは最後のProcessのみ有効である。
        /// その他のProcessでは仕掛り作業も計測しているため、正確なThroughputにはならない
        /// </summary>
        /// <returns></returns>
        public double ThroughputPerWeek()
        {
            if (!IsLastProcess) return 0.0;

            CardsByDate cardsByDate = CardsByDateList.Last();
            double cumulativeCount = cardsByDate.CumulativeCardCount;
            double ThroughputPerDay = cumulativeCount / ActualTotalDays();
            return ThroughputPerDay * 7.0;
        }

        /// <summary>
        /// WIP for the process step
        /// </summary>
        public WIPInfo WIP
        {
            get
            {
                int maxWIP = CardsByDateList.Max(x => x.CardCount);
                double averageWIP = CardsByDateList.Sum(x => x.CardCount) / (double)CardsByDateList.Count;
                int workingDays = CardsByDateList.Count(x => (x.CardCount == maxWIP) &&
                                                              x.Date.IsWorkingDay());

                DateTime maxWIPDate = CardsByDateList.First(x => x.CardCount == maxWIP).Date;
                return new WIPInfo
                {
                    MaxWIP = maxWIP,
                    AverageWIP = averageWIP,
                    MaxWIPDate = maxWIPDate,
                    WorkingDays4MaxWIP = workingDays
                };
            }
        }

        public CardsByDate? GetCardsByDate(DateTime date)
        {
            return CardsByDateList.FirstOrDefault(x => x.Date == date);
        }

        public void AddCard(ObjectId cardID, DateTime date)
        {
            if (date < m_ActualFromDate)
            {
                m_ActualFromDate = date;
            }

            CardsByDateList.FirstOrDefault(x => x.Date == date)?.AddCard(cardID);
        }

        public void CumulateCards()
        {
            CardsByDate preCardsByDate = null;
            foreach (CardsByDate cardsByDate in CardsByDateList)
            {
                preCardsByDate.IfNotNull(x => cardsByDate.CumulateCards(x.CumulativeCards));
                preCardsByDate = cardsByDate;
            }
        }
    }

    public class BoardHistory
    {
        public static DateTime FromDate { get; set; }
        public static DateTime ToDate { get; set; }
        public List<ProcessHistory> ProcessHistories { get; set; } = new List<ProcessHistory>();

        public static Board Board { get; set; }

        public static DevProcess DevProcess { get; set; }

        public BoardHistory(Board board, DateTime from, DateTime to)
        {
            Board = board;
            DevProcess = board.DevProcess;
            FromDate = from;
            ToDate = to;

            Repository.ProcessStep endProcess = DevProcess.GetEndOfCycleTime();
            foreach (Repository.ProcessStep process in Board.GetCycleTimeSteps())
            {
                bool isEndProcess = (process == endProcess);
                ProcessHistories.Add(new ProcessHistory(process, from.Date, to.Date, isEndProcess));
            }
        }

        public static bool IsLastProcessOfBoard(Repository.ProcessStep process)
        {
            return DevProcess.IsLastProcessOfBoard(process);
        }

        public List<TeamVelocity> Velocity { get; set; } = new List<TeamVelocity>();
        public bool HasUnknownStoryPoints =>
            (Velocity.FirstOrDefault(v => v.HasUnknownStoryPoints) != null);

        public int AverageVelocity4Every2Weeks { get; set; } = 0;

        public void AddCardCount(Repository.ProcessStep step, DateTime date, ObjectId cardID)
        {
            ProcessHistories
                .FirstOrDefault(x => x.Process == step)
                ?.AddCard(cardID, date.Date);
        }

        public int CardsInWIPOfLastProcess
        {
            set
            {
                ProcessHistories.ForEach(x => x.CardsInWIPOfLastProcess = value);
            }
        }

        public void FinalizeHistory()
        {
            ProcessHistories.ForEach(x => x.CumulateCards());
            MakeCumulativeFlowCount();
        }

        /// <summary>
        /// WIP for processes
        /// </summary>
        public WIPInfo WIP
        {
            get
            {
                WIPInfo wip = new WIPInfo { MaxWIP = 0, AverageWIP = 0.0, MaxWIPDate = FromDate, WorkingDays4MaxWIP = 0 };
                foreach (DateTime date in EachDay(FromDate, ToDate))
                {
                    int WIPOfDate = GetWIPByDate(date);
                    wip.AverageWIP += WIPOfDate;
                    if (WIPOfDate > wip.MaxWIP)
                    {
                        wip.MaxWIP = WIPOfDate;
                        wip.MaxWIPDate = date;
                        wip.WorkingDays4MaxWIP = 1;
                    }
                    else if (WIPOfDate == wip.MaxWIP)
                    {
                        wip.WorkingDays4MaxWIP++;
                    }
                }

                wip.AverageWIP /= TotalDays();

                return wip;
            }
        }

        public int TotalDays()
        {
            return (ToDate - FromDate).Days + 1;
        }

        public List<Card> AllCardsByDate(DateTime date)
        {
            HashSet<Card> cards = new HashSet<Card>();
            foreach (ProcessHistory processHistory in ProcessHistories)
            {
                CardsByDate cardsByDate = processHistory.GetCardsByDate(date);
                var c = CardsActivitiesCache.GetCards(cardsByDate.Cards);
                cards.UnionWith(c);
            }

            return cards.ToList();
        }

        /// <summary>
        /// 最後尾Process以外のCard合計がWIPである
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private int GetWIPByDate(DateTime date)
        {
            var processHistories = ProcessHistories.Take(ProcessHistories.Count - 1);
            int totalWIP = processHistories.Sum(x => x.GetCardsByDate(date).CardCount);
            return totalWIP;
        }

        /// <summary>
        /// 最後尾から累積を計算する
        /// </summary>
        private void MakeCumulativeFlowCount()
        {
            ProcessHistory previousHistory = null;
            foreach (ProcessHistory processHistory in Enumerable.Reverse(ProcessHistories))
            {
                MakeCumulativeFlow4Process(processHistory, previousHistory);
                previousHistory = processHistory;
            }
        }

        private static void MakeCumulativeFlow4Process(ProcessHistory processHistory, ProcessHistory previousHistory)
        {
            foreach (CardsByDate cardsByDate in processHistory.CardsByDateList)
            {
                if (previousHistory == null)
                {
                    // use cumulative count if no previous history
                    cardsByDate.CumulativeFlowCardCount = cardsByDate.CumulativeCardCount;
                }
                else
                {
                    CardsByDate preCardsByDate = previousHistory.GetCardsByDate(cardsByDate.Date);
                    cardsByDate.CumulativeFlowCardCount = cardsByDate.CardCount + preCardsByDate.CumulativeFlowCardCount;
                }
            }
        }
    }
}
