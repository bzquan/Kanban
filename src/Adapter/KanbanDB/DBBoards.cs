using MongoDB.Bson;
using MongoDB.Driver;

namespace Kanban.Repository
{
    public class DBBoards : IDBBoard
    {
        IDBClient m_DBClient;
        IDBCard m_DBCards;
        IDBActivity m_DBActivities;

        public DBBoards(IDBClient dbClient, IDBCard dbCards, IDBActivity dbActivies)
        {
            m_DBClient = dbClient;
            m_DBCards = dbCards;
            m_DBActivities = dbActivies;
        }

        public async Task Insert(Board board) => await BoardsCollection.InsertOneAsync(board);
        public async Task Insert(List<Board> boards) => await BoardsCollection.InsertManyAsync(boards);

        public async Task<ReplaceOneResult> Save(Board board) =>
            await BoardsCollection.ReplaceOneAsync(doc => doc._id == board._id,
                                                   board,
                                                   new ReplaceOptions { IsUpsert = true });

        public async Task<Board> CreateNewBord()
        {
            Board board = new Board();
            await Insert(board);
            return board;
        }

        /// <summary>
        /// Get boards from DB.
        /// </summary>
        /// <returns></returns>
        public async Task<List<Board>> GetBoards()
        {
            List<Board> boards = await RetrieveBoards();
            return boards;
        }

        private async Task<List<Board>> RetrieveBoards()
        {
            var filter = Builders<Board>.Filter.Empty;
            List<Board> boards = await BoardsCollection
                                    .Find(filter)
                                    .SortBy(c => c.SeqNo)
                                    .ToListAsync();

            return boards;
        }

        public UpdateResult Update<T>(Board board, string propertyName, T propertyValue)
        {
            var filter = Builders<Board>.Filter.Eq("_id", board._id);
            var update = Builders<Board>.Update.Set(propertyName, propertyValue);

            UpdateResult result = BoardsCollection.UpdateOne(filter, update);
            return result;
        }

        public async Task<UpdateResult> UpdateAsync<T>(Board board, string propertyName, T propertyValue)
        {
            var filter = Builders<Board>.Filter.Eq("_id", board._id);
            var update = Builders<Board>.Update.Set(propertyName, propertyValue);

            UpdateResult result = await BoardsCollection.UpdateOneAsync(filter, update);
            return result;
        }

        public async Task<DeleteResult> Delete(ObjectId board_id)
        {
            DeleteResult delete_card_result = await m_DBCards.DeleteCards(board_id);

            var filter = Builders<Board>.Filter.Eq("_id", board_id);
            DeleteResult result = await BoardsCollection.DeleteOneAsync(filter);
            return result;
        }

        public static string NameOfProcessStepSeqNo =>
            nameof(WorkState) + "." + nameof(WorkState.ProcessStepSeqNo);

        public UpdateResult MoveCard(Board board, int fromSeqNo, int toSeqNo, bool forward)
        {
            m_DBCards.ExecMoveCards(board, fromSeqNo, toSeqNo, forward);
            return m_DBActivities.UpdateActivities(board, fromSeqNo, toSeqNo, forward);
        }

        private static int IncValue4Moving(bool forward) => forward ? 1 : -1;

        public UpdateResult ChangeCardsAndActivitySeqNo(Board board, int fromSeqNo, int toSeqNo)
        {
            m_DBCards.ChangeCardsSeqNo(board, fromSeqNo, toSeqNo);
            return m_DBActivities.ChangeActivitiesSeqNo(board, fromSeqNo, toSeqNo);
        }

        public void DropBoards() => m_DBClient.DB.DropCollection(CollectionNames.BoardCollectionName);

        private IMongoCollection<Board> BoardsCollection => m_DBClient.DB.GetCollection<Board>(CollectionNames.BoardCollectionName);
    }
}
