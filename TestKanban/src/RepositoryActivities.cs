using Kanban.Repository;
using MongoDB.Bson;
using MongoDB.Driver;

namespace UnitTest
{
    [TestClass]
    public class RepositoryActivities
    {
        IDBClient dbClient;
        IDBBoard dbBoards;
        IDBCard dbCards;
        IDBActivity dbActivities;

        [TestInitialize]
        public void Setup()
        {
            dbClient = new DBClient();
            dbClient.DBName = "TestDB";
            dbClient.MongoDBClient.DropDatabase(dbClient.DBName);

            dbActivities = new DBActivities(dbClient);
            dbCards = new DBCards(dbClient, dbActivities);
            dbBoards = new DBBoards(dbClient, dbCards, dbActivities);
        }

        [TestCleanup]
        public void TearDown()
        {
            dbClient.MongoDBClient.DropDatabase(dbClient.DBName);
        }

        [TestMethod]
        public async Task CreateActivityTest()
        {
            // Given
            WorkState workState = new WorkState { ProcessStepSeqNo = 0, IsWIP = true };
            Card card = new Card { _id = new ObjectId(), WorkState = workState };

            // When
            DateTime stateChangedDate = new DateTime(1961, 3, 18);
            Activity activity = new Activity { CardID = card._id, WorkState = card.WorkState, StateChangedDate = stateChangedDate };
            await dbActivities.Insert(activity);

            // Then
            List<Activity> activities = await dbActivities.GetActivitiesOf(card);

            Assert.HasCount(1, activities);
            Assert.AreEqual(card.WorkState.ProcessStepSeqNo, activities[0].WorkState.ProcessStepSeqNo);
            Assert.AreEqual(card.WorkState.IsWIP, activities[0].WorkState.IsWIP);
            Assert.AreEqual(stateChangedDate, activities[0].StateChangedDate);
        }

        [TestMethod]
        public async Task RetrieveActivitiesOfCardTest()
        {
            ObjectId boardID = new ObjectId();
            WorkState workState = new WorkState { ProcessStepSeqNo = 0, IsWIP = true };
            Card card = new Card { _id = new ObjectId(), WorkState = workState };

            // When
            Activity activity = new Activity { BoardID = boardID, CardID = card._id, WorkState = card.WorkState, StateChangedDate = DateTime.Now };
            await dbActivities.Insert(activity);

            // Then
            List<Activity> activities = await dbActivities.GetActivitiesOf(card);

            Assert.HasCount(1, activities);
            Assert.AreEqual(card.WorkState.ProcessStepSeqNo, activities[0].WorkState.ProcessStepSeqNo);
            Assert.AreEqual(card.WorkState.IsWIP, activities[0].WorkState.IsWIP);
        }

        [TestMethod]
        public async Task DeleteActivityTest()
        {
            // Given
            WorkState workState = new WorkState { ProcessStepSeqNo = 0, IsWIP = true };
            Card card = new Card { _id = new ObjectId(), WorkState = workState };
            Activity activity = new Activity { CardID = card._id, WorkState = card.WorkState };
            await dbActivities.Insert(activity);

            // When
            DeleteResult result = await dbActivities.DeleteActivitiesOf(card);

            // Then
            Assert.AreEqual(1, result.DeletedCount);
            List<Activity> activities = await dbActivities.GetActivitiesOf(card);
            Assert.IsEmpty(activities);
        }

        [TestMethod]
        public async Task DeleteActivitiesOfCardTest()
        {
            // Given
            WorkState workState = new WorkState { ProcessStepSeqNo = 0, IsWIP = true };
            Card card = new Card { _id = new ObjectId(), WorkState = workState };
            Activity activity = new Activity { CardID = card._id, WorkState = card.WorkState };
            await dbActivities.Insert(activity);

            // When
            DeleteResult result = await dbCards.Delete(card);

            // Then
            List<Activity> activities = await dbActivities.GetActivitiesOf(card);
            Assert.IsEmpty(activities);
        }

        [TestMethod]
        public async Task DeleteActivitiesOfBoardTest()
        {
            // Given
            ObjectId boardID = new ObjectId();
            Card card = new Card { _id = new ObjectId() };
            Activity activity = new Activity { BoardID = boardID, CardID = card._id, WorkState = new WorkState { ProcessStepSeqNo = 0, IsWIP = false } };
            await dbActivities.Insert(activity);

            // When
            DeleteResult result = await dbBoards.Delete(boardID);

            // Then
            List<Activity> activities = await dbActivities.GetActivitiesOf(card);
            Assert.IsEmpty(activities);
        }
    }
}
