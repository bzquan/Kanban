using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MongoDB.Driver;

using static Kanban.Repository.DBClient;
using static Kanban.Repository.CollectionNames;
using MongoDB.Bson;
using System;

namespace Kanban.Repository
{
    public class DBActivities : IDBActivity
    {
        IDBClient m_DBClient;

        public DBActivities(IDBClient dbClient)
        {
            m_DBClient = dbClient;
            CreateIndexes();
        }

        public async Task Insert(Activity activity)
        {
            await ActivitiesCollection.InsertOneAsync(activity);
        }

        public async Task<DeleteResult> DeleteActivities(Board board, int seqNo)
        {
            var filter_activity_board = Builders<Activity>.Filter.Eq("BoardID", board._id);
            var filter_activity_seqNo = Builders<Activity>.Filter.Eq(NameOfProcessStepSeqNo, seqNo);
            var filter = Builders<Activity>.Filter.And(filter_activity_board, filter_activity_seqNo);
            return await ActivitiesCollection.DeleteManyAsync(filter);
        }

        public async Task<DeleteResult> DeleteActivities(ObjectId board_id)
        {
            var filter_activity = Builders<Activity>.Filter.Eq("BoardID", board_id);
            return await ActivitiesCollection.DeleteManyAsync(filter_activity);
        }

        public async Task<DeleteResult> DeleteActivitiesOf(Card card)
        {
            return await DeleteActivitiesOf(card._id);
        }

        public async Task<DeleteResult> DeleteActivitiesOf(ObjectId card_id)
        {
            var filter = Builders<Activity>.Filter.Eq("CardID", card_id);
            DeleteResult result = await ActivitiesCollection.DeleteManyAsync(filter);
            return result;
        }

        public void DropActivities() => m_DBClient.DB.DropCollection(ActivitiesCollectionName);

        public async Task<List<Activity>> GetActivitiesOf(Card card)
        {
            var filter = Builders<Activity>.Filter.Eq("CardID", card._id);
            return await GetActivities(filter, sortAscending: false);
        }

        public async Task<List<Activity>> GetActivitiesOf(List<ObjectId> cardIDs)
        {
            var filter = Builders<Activity>.Filter.Where(x => cardIDs.Contains(x.CardID));
            return await GetActivities(filter, sortAscending: true);
        }

        public async Task<List<Activity>> GetActivities4Metrics(Board board, DateTime from, DateTime to)
        {
            int fromSeqNo = board.ProcessSteps.First().PhaseSeqNo; ;
            int toSeqNo = board.ProcessSteps.Last().PhaseSeqNo;

            var filter_board = Builders<Activity>.Filter.Eq("BoardID", board._id);
            var filter_from = Builders<Activity>.Filter.Gte(nameof(Activity.StateChangedDate), from);
            var filter_to = Builders<Activity>.Filter.Lt(nameof(Activity.StateChangedDate), to);
            var filter_seqNo1 = Builders<Activity>.Filter.Gte(NameOfProcessStepSeqNo, fromSeqNo);
            var filter_seqNo2 = Builders<Activity>.Filter.Lte(NameOfProcessStepSeqNo, toSeqNo);
            var filter = Builders<Activity>.Filter.And(filter_board, filter_from, filter_to, filter_seqNo1, filter_seqNo2);

            return await GetActivities(filter, sortAscending: true);
        }
        private static string NameOfProcessStepSeqNo => DBBoards.NameOfProcessStepSeqNo;

        private async Task<List<Activity>> GetActivities(FilterDefinition<Activity> filter, bool sortAscending)
        {
            List<Activity> activities;
            if (sortAscending)
                activities = await ActivitiesCollection
                                     .Find(filter)
                                     .SortBy(c => c.StateChangedDate)
                                     .ToListAsync();
            else
                activities = await ActivitiesCollection
                                     .Find(filter)
                                     .SortByDescending(c => c.StateChangedDate)
                                     .ToListAsync();

            activities.ForEach(x => x.ConvertLocalTime());
            return activities;
        }

        public UpdateResult UpdateActivities(Board board, int fromSeqNo, int toSeqNo, bool forward)
        {
            var filter_activity_board = Builders<Activity>.Filter.Eq("BoardID", board._id);
            var filter_fromSeqNo = Builders<Activity>.Filter.Gte(NameOfProcessStepSeqNo, fromSeqNo);
            var filter_toSeqNo = Builders<Activity>.Filter.Lte(NameOfProcessStepSeqNo, toSeqNo);
            var filter_activity = Builders<Activity>.Filter.And(filter_activity_board, filter_fromSeqNo, filter_toSeqNo);
            var update_activity = Builders<Activity>.Update.Inc(NameOfProcessStepSeqNo, IncValue4Moving(forward));

            return ActivitiesCollection.UpdateMany(filter_activity, update_activity);
        }

        private static int IncValue4Moving(bool forward) => DBCards.IncValue4Moving(forward);

        public UpdateResult ChangeActivitiesSeqNo(Board board, int fromSeqNo, int toSeqNo)
        {
            var filter_activityOfBoard = Builders<Activity>.Filter.Eq("BoardID", board._id);
            var filter_activitySeqNo = Builders<Activity>.Filter.Eq(NameOfProcessStepSeqNo, fromSeqNo);
            var filter_activity = Builders<Activity>.Filter.And(filter_activityOfBoard, filter_activitySeqNo);
            var update = Builders<Activity>.Update.Set(NameOfProcessStepSeqNo, toSeqNo);

            return ActivitiesCollection.UpdateMany(filter_activity, update);
        }

        public async void CreateIndexes()
        {
            var keys = Builders<Activity>
                        .IndexKeys
                        .Ascending("CardID")
                        .Ascending("WorkState");
            await ActivitiesCollection.Indexes.CreateOneAsync(keys);
        }

        private IMongoCollection<Activity> ActivitiesCollection => m_DBClient.DB.GetCollection<Activity>(ActivitiesCollectionName);
    }
}
