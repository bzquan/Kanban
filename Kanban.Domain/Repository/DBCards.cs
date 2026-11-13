using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;
using static Kanban.Repository.CollectionNames;

namespace Kanban.Repository
{
    public class DBCards : IDBCard
    {
        IDBClient m_DBClient;
        IDBActivity m_DBActivities;

        private int LimitNum4QueringLastProcess => Util.ConfigReader.GetValue("LimitNum4QueringLastProcess", 20);

        public DBCards(IDBClient dbClient, IDBActivity dbActivities)
        {
            m_DBClient = dbClient;
            m_DBActivities = dbActivities;
        }

        private void SetDefaultTagIfFieldNotExists()
        {
            var filter_tagExist = Builders<Card>.Filter.Exists("Tag");
            var filter_tagNotExist = Builders<Card>.Filter.Not(filter_tagExist);
            var update = Builders<Card>.Update.Set("Tag", "");
            UpdateResult result = CardsCollection.UpdateMany(filter_tagNotExist, update);
        }

        public async Task Insert(Card card) => await CardsCollection.InsertOneAsync(card);
        public void DropCards() => m_DBClient.DB.DropCollection(CardsCollectionName);

        public async Task<List<Card>> GetCards()
        {
            List<Card> cards = await CardsCollection
                                    .Find(new BsonDocument())
                                    .ToListAsync();
            ConvertDatetime2Local(cards);

            return cards;
        }

        public async Task<List<Card>> GetCards(Board board, List<ObjectId> cardIDs)
        {
            var filter = Builders<Card>.Filter.Where(x => (x.BoardID == board._id) && cardIDs.Contains(x._id));
            List<Card> cards = await CardsCollection
                                        .Find(filter)
                                        .ToListAsync();
            ConvertDatetime2Local(cards);

            return cards;
        }

        public async Task<List<Card>> GetCards(Board board, CardRetrieveCondition retrieveCondition)
        {
            var filter_boardID = Builders<Card>.Filter.Eq("BoardID", board._id);
            var filter_workState_step = Builders<Card>.Filter.Eq(NameOfProcessStepSeqNo, retrieveCondition.CardWorkState.ProcessStepSeqNo);
            var filter_workState_wip = Builders<Card>.Filter.Eq("WorkState.IsWIP", retrieveCondition.CardWorkState.IsWIP);
            var filter_with_texts = CreateWhereFilter(retrieveCondition.CardFilter);
            var filter_onBoard = GetOnBoardFilter(retrieveCondition.BoardType);

            FilterDefinition<Card> filter = (filter_onBoard != null) ?
                    Builders<Card>.Filter.And(filter_boardID, filter_workState_step, filter_workState_wip, filter_onBoard, filter_with_texts) :
                    Builders<Card>.Filter.And(filter_boardID, filter_workState_step, filter_workState_wip, filter_with_texts);

            List<Card> cards = (retrieveCondition.BoardType == OnBoardFilter.OnBackBoard) ?
                                    await RetrieveCardsOnBackBoardFromDB(filter) :
                                    await RetrieveCardsOnFrontBoardFromDB(filter, board, retrieveCondition.CardWorkState);

            ConvertDatetime2Local(cards);
            return cards;
        }

        private async Task<List<Card>> RetrieveCardsOnBackBoardFromDB(FilterDefinition<Card> filter)
        {
            DateTime halfYearsBefore = DateTime.Now.AddDays(-180);
            var filter_onBackBoardDate = Builders<Card>.Filter.Gte("OnBackBoardDate", halfYearsBefore);
            var filter_updated = Builders<Card>.Filter.And(filter, filter_onBackBoardDate);
            return await CardsCollection
                            .Find(filter_updated)
                            .SortByDescending(c => c.StateChangedDate)
                            .ToListAsync();
        }

        private async Task<List<Card>> RetrieveCardsOnFrontBoardFromDB(FilterDefinition<Card> filter, Board board, WorkState cardWorkState)
        {
            bool is_last_phase = (cardWorkState.ProcessStepSeqNo == board.ProcessSteps.Count - 1) && (cardWorkState.IsWIP == false);
            if (is_last_phase)
            {
                return await CardsCollection
                                .Find(filter)
                                .SortByDescending(c => c.StateChangedDate)
                                .Limit(LimitNum4QueringLastProcess)
                                .ToListAsync();
            }
            else
            {
                return await CardsCollection
                                .Find(filter)
                                .SortBy(c => c.SeqNo)
                                .ToListAsync();
            }
        }

        public async Task<List<Card>> GetCards(Board board)
        {
            var filter = Builders<Card>.Filter.Eq("BoardID", board._id);
            List<Card> cards = await CardsCollection
                            .Find(filter)
                            .SortBy(c => c.SeqNo)
                            .ToListAsync();

            ConvertDatetime2Local(cards);
            return cards;
        }

        private static FilterDefinition<Card> CreateWhereFilter(string cardFilter)
        {
            string[] filterItems = ExtractFilterItems(cardFilter);
            var builder = Builders<Card>.Filter;
            if (filterItems.Count() == 0) return builder.Empty;

            FilterDefinition<Card> orFilter = null;
            foreach (string item in filterItems)
            {
                var filter = Builders<Card>.Filter.Where(card => card.Title.ToLower().Contains(item) ||
                                                                 card.Executors.Contains(item) ||
                                                                 card.Tag.ToLower().Contains(item));
                if (orFilter == null)
                    orFilter = filter;
                else
                    orFilter = Builders<Card>.Filter.Or(orFilter, filter);
            }

            return AddReleaseDateFilter(orFilter, cardFilter);
        }

        private static FilterDefinition<Card> AddReleaseDateFilter(FilterDefinition<Card> orFilter, string cardFilter)
        {
            DateTime? fromDateTime = ExtractReleaseDate(cardFilter);
            if (fromDateTime == null) return orFilter;

            var releaseDateFromfilter = Builders<Card>.Filter.Gte(card => card.ReleaseDate, fromDateTime.Value);
            DateTime toDateTime = fromDateTime.Value.AddHours(23.99);   // 23時間59分
            var releaseDateTofilter = Builders<Card>.Filter.Lte(card => card.ReleaseDate, toDateTime);
            var releaseDatefilter = Builders<Card>.Filter.And(releaseDateFromfilter, releaseDateTofilter);
            return Builders<Card>.Filter.Or(orFilter, releaseDatefilter);
        }

        private static string[] ExtractFilterItems(string cardFilter)
        {
            if ((cardFilter == null) || (cardFilter.Trim().Length == 0)) return new string[0];

            // new char[0] represent white spaces
            // Usage of Split: if the separator parameter is null or contains no characters,
            // white -space characters are assumed to be the delimiters.
            char[] separators = { ' ', '　', ',', '、' };
            string[] filterItems = cardFilter
                                    .Trim()
                                    .ToLower()
                                    .Split(separators, StringSplitOptions.RemoveEmptyEntries);
            return filterItems;
        }

        private static DateTime? ExtractReleaseDate(string cardFilter)
        {
            if ((cardFilter == null) || (cardFilter.Trim().Length == 0)) return null;
            Match match = Regex.Match(cardFilter, @"(\d){4,8}");

            if (!match.Success) return null;

            string matchedDate = match.Value;
            if (IsWrongLength(matchedDate.Length)) return null;

            return MakeDateTimeBy(matchedDate);
        }

        private static bool IsWrongLength(int dateStringLength)
        {
            return (dateStringLength != 4) &&
                   (dateStringLength != 6) &&
                   (dateStringLength != 8);
        }

        private static DateTime? MakeDateTimeBy(string date)
        {
            DateTime today = DateTime.Now;
            string year = today.Year.ToString();
            int dateStringLength = date.Length;
            switch (dateStringLength)
            {
                case 6:
                    year = "20" + date.Substring(0, 2);
                    break;
                case 8:
                    year = date.Substring(0, 4);
                    break;
                default:
                    break;
            }
            try
            {
                string month = date.Substring(date.Length - 4, 2);
                string day = date.Substring(date.Length - 2);

                return DateTime.Parse(year + "-" + month + "-" + day);
            }
            catch
            {
                return null;
            }
        }

        private static FilterDefinition<Card> GetOnBoardFilter(OnBoardFilter onBoardFilter)
        {
            switch (onBoardFilter)
            {
                case OnBoardFilter.OnFrontBoard:
                    return Builders<Card>.Filter.Or(Builders<Card>.Filter.Exists("IsOnBackBoard", false),
                                                    Builders<Card>.Filter.Eq("IsOnBackBoard", false));
                case OnBoardFilter.OnBackBoard:
                    return Builders<Card>.Filter.Eq("IsOnBackBoard", true);
                default:
                    return null;
            }
        }

        private static void ConvertDatetime2Local(List<Card> cards)
        {
            cards.ForEach(x => x.ConvertLocalTime());
        }

        public async Task<UpdateResult> Update<T>(Card card, string propertyName, T propertyValue)
        {
            var filter = Builders<Card>.Filter.Eq("_id", card._id);
            var update = Builders<Card>.Update.Set(propertyName, propertyValue);

            UpdateResult result = await CardsCollection.UpdateOneAsync(filter, update);
            return result;
        }

        public async Task<DeleteResult> DeleteCards(ObjectId board_id)
        {
            var filter_card = Builders<Card>.Filter.Eq("BoardID", board_id);
            DeleteResult result = await CardsCollection.DeleteManyAsync(filter_card);

            await m_DBActivities.DeleteActivities(board_id);

            return result;
        }

        public async Task<DeleteResult> Delete(Card card)
        {
            var filter = Builders<Card>.Filter.Eq("_id", card._id);
            DeleteResult result = await CardsCollection.DeleteOneAsync(filter);

            await m_DBActivities.DeleteActivitiesOf(card);
            return result;
        }

        public async Task<DeleteResult> DeleteNotStartedCards()
        {
            var notStartedCards_filter = Builders<Card>.Filter.Eq(NameOfProcessStepSeqNo, 0);
            var feature_filter = Builders<Card>.Filter.Eq("CardType", CardType.Feature);
            var filter = Builders<Card>.Filter.And(notStartedCards_filter, feature_filter);
            var cards = await CardsCollection.Find(filter).ToListAsync();
            foreach (Card card in cards)
            {
                await m_DBActivities.DeleteActivitiesOf(card._id);
            }

            return await CardsCollection.DeleteManyAsync(filter);
        }

        public UpdateResult ExecMoveCards(Board board, int fromSeqNo, int toSeqNo, bool forward)
        {
            var filter_cardOfBoard = Builders<Card>.Filter.Eq("BoardID", board._id);
            var filter_fromSeqNo = Builders<Card>.Filter.Gte(NameOfProcessStepSeqNo, fromSeqNo);
            var filter_toSeqNo = Builders<Card>.Filter.Lte(NameOfProcessStepSeqNo, toSeqNo);
            var filter_card = Builders<Card>.Filter.And(filter_cardOfBoard, filter_fromSeqNo, filter_toSeqNo);
            var update_card = Builders<Card>.Update.Inc(NameOfProcessStepSeqNo, IncValue4Moving(forward));

            return CardsCollection.UpdateMany(filter_card, update_card);
        }

        public static int IncValue4Moving(bool forward) => forward ? 1 : -1;

        public UpdateResult ChangeCardsSeqNo(Board board, int fromSeqNo, int toSeqNo)
        {
            var filter_cardOfBoard = Builders<Card>.Filter.Eq("BoardID", board._id);
            var filter_cardSeqNo = Builders<Card>.Filter.Eq(NameOfProcessStepSeqNo, fromSeqNo);
            var filter_card = Builders<Card>.Filter.And(filter_cardOfBoard, filter_cardSeqNo);
            var update = Builders<Card>.Update.Set(NameOfProcessStepSeqNo, toSeqNo);

            return CardsCollection.UpdateMany(filter_card, update);
        }

        private static string NameOfProcessStepSeqNo => DBBoards.NameOfProcessStepSeqNo;

        public bool ExistCards(Board board, Repository.ProcessStep step)
        {
            var filter_boardID = Builders<Card>.Filter.Eq("BoardID", board._id);
            var filter_workState_step = Builders<Card>.Filter.Eq(NameOfProcessStepSeqNo, step.PhaseSeqNo);
            var filter = Builders<Card>.Filter.And(filter_boardID, filter_workState_step);

            return CardsCollection.CountDocuments(filter) > 0;
        }

        public IMongoCollection<Card> CardsCollection => m_DBClient.DB.GetCollection<Card>(CardsCollectionName);
    }
}
