using MongoDB.Bson;

namespace Kanban.Model
{
    class ReleasedCard
    {
        public ReleasedCard(Card card, DateTime releaseDate)
        {
            Card = card;
            ReleaseDate = releaseDate;
        }

        public Card Card { get; private set; }
        public DateTime ReleaseDate { get; private set; }
    }

    public class BoardHistoryBuilder
    {
        public const int ONE_WEEK = 7; // One week is 7 days
        public const int TWO_WEEKS = ONE_WEEK * 2;
        public const int SIX_WEEKS = ONE_WEEK * 6;

        Board Board { get; set; }
        DateTime FromDate { get; set; }
        DateTime ToDate { get; set; }
        Repository.ProcessStep EndStepOfBoard { get; set; }
        DevProcess DevProcess { get; set; }

        IBoardRepository m_BoardRepository;
        ICardRepository m_CardRepository;
        IActivityRepository m_ActivityRepository;

        public BoardHistoryBuilder(Board board, DateTime from, DateTime to, IBoardRepository boardRepository, ICardRepository cardRepository, IActivityRepository activityRepository)
        {
            Board = board;
            DevProcess = board.DevProcess;
            EndStepOfBoard = DevProcess.LastStep;

            FromDate = from.Date;
            ToDate = to.Date;

            m_BoardRepository = boardRepository;
            m_CardRepository = cardRepository;
            m_ActivityRepository = activityRepository;
        }

        public async Task<BoardHistory> GetBoardHistory()
        {
            BoardHistory history = new BoardHistory(Board, FromDate, ToDate);

            CardsActivitiesCache.Cards = await ExtractBoardHistory(history);
            CardsActivitiesCache.ActivitiesForCards = await GetActivitiesOf(CardsActivitiesCache.Cards);
            history.CardsInWIPOfLastProcess = await GetCardsOfEndOfCycleTime();
            history.FinalizeHistory();

            return history;
        }

        private async Task<List<Activity>> GetActivitiesOf(List<Card> cards)
        {
            List<ObjectId> cardIDs = new List<ObjectId>();
            cards.ForEach(x => cardIDs.Add(x._id));
            return await m_ActivityRepository.GetActivitiesOf(cardIDs);
        }

        private async Task<List<Card>> ExtractBoardHistory(BoardHistory history)
        {
            // Get activities from begining date(FromDate before one week) to one day after the end date(ToDate) to cumulate cards correctly
            List<Activity> activities = await GetActivities4Metrics(FromDate.AddDays(-ONE_WEEK), ToDate.AddDays(1));
            var cardIDs = activities
                            .Select(a => a.CardID)
                            .Distinct();

            List<Card> cards = await m_CardRepository.GetCards(Board, cardIDs.ToList());
            ExtractBoardHistoryFromActivities(history, activities, cards);

            List<ReleasedCard> releasedCards = ExtractReleasedCardsFromActivities(activities, cards);
            history.Velocity = BuildVelocity(releasedCards);
            history.AverageVelocity4Every2Weeks = BuildAverageVelocity4TwoWeeks(releasedCards);

            return cards;
        }

        private async Task<List<Activity>> GetActivities4Metrics(DateTime from, DateTime to)
        {
            List<Activity> activities = await m_BoardRepository.GetActivities4Metrics(Board, from, to);
            await RemoveActivitiesInWIPStatus(activities);

            return activities;
        }

        private async Task RemoveActivitiesInWIPStatus(List<Activity> activities)
        {
            Repository.ProcessStep lastProcessStep = Board.DevProcess.GetEndOfCycleTime();
            List<Card> cardsOnBackBoard = await m_CardRepository.GetCards(Board, new WorkState { ProcessStepSeqNo = lastProcessStep.PhaseSeqNo, IsWIP = true }, "", Repository.OnBoardFilter.OnBackBoard);
            activities.RemoveAll(a => NeedToRemoveActivity(a, cardsOnBackBoard, lastProcessStep.PhaseSeqNo));
        }

        private bool NeedToRemoveActivity(Activity activity, List<Card> cardsOnBackBoard, int lastProcessStepSeqNo)
        {
            return DevProcess.IsLastProcessStep(activity.WorkState, lastProcessStepSeqNo) &&
                   IsOnBackBoard(activity.CardID, cardsOnBackBoard);
        }

        private bool IsOnBackBoard(ObjectId cardID, List<Card> cardsOnBackBoard)
        {
            Card card = cardsOnBackBoard.FirstOrDefault(x => x._id == cardID);
            if (card == null) return false;

            if ((card.CardType == Repository.CardType.DefectInvestigation) ||
                (card.CardType == Repository.CardType.Cancelled))
            {
                return true;
            }

            bool cumulateCardsOnBackBoard = Util.ConfigReader.GetValue("CumulateCardsOnBackBoard", true);
            return (cumulateCardsOnBackBoard == false);
        }

        private void ExtractBoardHistoryFromActivities(BoardHistory history, List<Activity> activities, List<Card> cards)
        {
            IEnumerable<Repository.ProcessStep> steps4CycleTime = Board.GetCycleTimeSteps();
            foreach (var card in cards)
            {
                List<Activity> activitiesOfCard = activities.FindAll(a => a.CardID == card._id);
                foreach (Repository.ProcessStep process in steps4CycleTime)
                {
                    List<DateTime> dates = DaysStayedInProcess(process, activitiesOfCard);
                    foreach (DateTime date in dates)
                    {
                        history.AddCardCount(process, date, card._id);
                    }
                }
            }
        }

        private List<TeamVelocity> BuildVelocity(List<ReleasedCard> releasedCards)
        {
            List<TeamVelocity> velocityList = new List<TeamVelocity>();
            for (DateTime toDate = FromDate.AddDays(ONE_WEEK); toDate < ToDate.AddDays(1); toDate = toDate.AddDays(ONE_WEEK))
            {
                DateTime fromDate = toDate.AddDays(-TWO_WEEKS);
                var velocity = releasedCards
                                 .Where(r => r.ReleaseDate >= fromDate && r.ReleaseDate <= toDate)
                                 .Sum(r => r.Card.StoryPoints);
                var hasUnknownStoryPoints = releasedCards
                                                   .Where(r => r.ReleaseDate >= fromDate && r.ReleaseDate <= toDate)
                                                   .FirstOrDefault(r => r.Card.StoryPoints == Repository.Card.UNKNOWN_STORY_POINTS) != null;


                velocityList.Add(new TeamVelocity(velocity, toDate, hasUnknownStoryPoints));
            }

            return velocityList;
        }

        private int BuildAverageVelocity4TwoWeeks(List<ReleasedCard> releasedCards)
        {
            int totalUserStoryPointsReleased = 0;
            DateTime fromDate4AverageVelocity = ToDate.AddDays(-SIX_WEEKS);
            totalUserStoryPointsReleased = releasedCards
                                 .Where(r => r.ReleaseDate >= fromDate4AverageVelocity && r.ReleaseDate <= ToDate)
                                 .Sum(r => r.Card.StoryPoints);
            int averageBy2weeks = totalUserStoryPointsReleased / 3;
            return averageBy2weeks;
        }
        /// <summary>
        /// リリース済みのカードの取得
        /// リリース済みのカードとは、Processの最後尾のStepまで完了したカードを示す。
        /// 備考：作業中のカードはリリース済みとを見なす
        /// </summary>
        /// <param name="activities">最近のActivity</param>
        /// <param name="cards">確認対象カード</param>
        /// <returns></returns>
        private List<ReleasedCard> ExtractReleasedCardsFromActivities(List<Activity> activities, List<Card> cards)
        {
            Repository.ProcessStep processToMeasure = DevProcess.GetEndOfCycleTime();
            List<Card> finishedCards =
                cards.Where(x => IsReleasedCard(x, processToMeasure))
                     .ToList();

            List<ReleasedCard> releasedCards = new List<ReleasedCard>();
            foreach (var card in finishedCards)
            {
                List<Activity> releaseActivities = activities.FindAll(a => (a.CardID == card._id) && (a.WorkState.ProcessStepSeqNo == processToMeasure.PhaseSeqNo));

                if (releaseActivities.Count > 0)
                {
                    DateTime releaseDate = releaseActivities.Max(a => a.StateChangedDate);
                    releasedCards.Add(new ReleasedCard(card, releaseDate));
                }
            }
            return releasedCards;
        }

        private bool IsReleasedCard(Card card, Repository.ProcessStep processToMeasure)
        {
            if (card.WorkState.ProcessStepSeqNo > processToMeasure.PhaseSeqNo)
                return true;
            else if (card.WorkState.ProcessStepSeqNo == processToMeasure.PhaseSeqNo)
                return !card.WorkState.IsWIP;   // 仕掛りのカードを除外する
            else  // card.WorkState.ProcessStepSeqNo < processToMeasure.PhaseSeqNo
                return false;
        }

        private List<DateTime> DaysStayedInProcess(Repository.ProcessStep process, List<Activity> activitiesOfCard)
        {
            List<Activity> activities4Process = ExtractActivities4Process(process, activitiesOfCard);
            List<DateTime> datesStayed;

            bool isReleasingStep = (process == EndStepOfBoard);
            if (isReleasingStep)
            {
                datesStayed = DaysStayedInReleasingProcess(activities4Process);
            }
            else
            {
                datesStayed = DaysStayedInNonReleasingProcess(activitiesOfCard, activities4Process);
            }

            return datesStayed.Distinct().ToList();
        }

        private static List<Activity> ExtractActivities4Process(Repository.ProcessStep process, List<Activity> activitiesOfCard)
        {
            if (process.IsEndOfCycleTime)
                return activitiesOfCard   // 最後ProcessStepについては、WIPを除外し、Doneのみとする
                           .FindAll(a => a.WorkState.ProcessStepSeqNo == process.PhaseSeqNo && !a.WorkState.IsWIP);
            else
                return activitiesOfCard
                           .FindAll(a => a.WorkState.ProcessStepSeqNo == process.PhaseSeqNo);
        }

        private List<DateTime> DaysStayedInReleasingProcess(List<Activity> activities4Process)
        {
            List<DateTime> dates = new List<DateTime>();
            Activity releasingActivity = activities4Process.LastOrDefault();
            if (releasingActivity != null)
                dates.Add(releasingActivity.StateChangedDate.Date);

            return dates;
        }

        private List<DateTime> DaysStayedInNonReleasingProcess(List<Activity> activitiesOfCard, List<Activity> activities4Process)
        {
            List<DateTime> dates = new List<DateTime>();
            foreach (Activity activity in activities4Process)
            {
                DateTime beginDate = activity.StateChangedDate;
                DateTime? endDate = LeavingDateFromProcess(beginDate, activitiesOfCard);
                if (endDate != null)
                {
                    if (HasStayedEnoughTimeInProcess(beginDate, endDate.Value))
                    {
                        AppendDates(dates, beginDate, endDate.Value);
                    }
                }
                else
                {
                    AppendDates(dates, beginDate, ToDate);
                }
            }

            return dates;
        }

        private DateTime? LeavingDateFromProcess(DateTime beginDate, List<Activity> activitiesOfCard)
        {
            Activity nextActivity = activitiesOfCard.FirstOrDefault(a => a.StateChangedDate > beginDate);
            return nextActivity?.StateChangedDate;
        }

        // 10分以上留まった場合のみ計測対象とする
        private bool HasStayedEnoughTimeInProcess(DateTime beginDate, DateTime endDate)
            => (endDate - beginDate).TotalMinutes >= 10;

        private void AppendDates(List<DateTime> dates, DateTime from, DateTime to)
        {
            foreach (DateTime date in Util.DateTimeUtil.EachDay(from, to))
            {
                dates.Add(date);
            }
        }

        private async Task<int> GetCardsOfEndOfCycleTime()
        {
            Repository.ProcessStep endProcess = DevProcess.GetEndOfCycleTime();
            WorkState workState = new WorkState { ProcessStepSeqNo = endProcess.PhaseSeqNo, IsWIP = false };
            List<Card> cardsInWIPOfLastProcess = await m_CardRepository.GetCards(Board, workState, cardFilter: "", onBoardFilter: Repository.OnBoardFilter.OnFrontBoard);
            return cardsInWIPOfLastProcess.Count;
        }
    }
}
