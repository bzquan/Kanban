using Kanban.Repository;
using MongoDB.Bson;
using MongoDB.Driver;

namespace UnitTest
{
    [TestClass]
    public class RepositoryCardTest
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
        public async Task InsertCardTest()
        {
            // Given
            ObjectId boardID = new ObjectId();
            Card card = new Card { Title = "Card", BoardID = boardID };

            // When
            await dbCards.Insert(card);

            // Then
            List<Card> cards = await dbCards.GetCards();

            Assert.HasCount(1, cards);
            Assert.AreEqual("Card", cards[0].Title);
            Assert.AreEqual(boardID, cards[0].BoardID);
        }

        [TestMethod]
        public async Task UpdateCardTest()
        {
            // Given
            ObjectId boardID = new ObjectId();
            Card card = new Card { Title = "Card", BoardID = boardID };
            await dbCards.Insert(card);

            // When
            card.Title = "Updated Card Name";
            UpdateResult updateResult = await dbCards.Update(card, nameof(Card.Title), card.Title);

            // Then
            Assert.AreEqual(1, updateResult.ModifiedCount);
            List<Card> cards = await dbCards.GetCards();
            Assert.AreEqual("Updated Card Name", cards[0].Title);
        }

        [TestMethod]
        public async Task UpdateDateTimeTest()
        {
            // Given
            ObjectId boardID = new ObjectId();
            Card card = new Card { Title = "Card", BoardID = boardID };
            await dbCards.Insert(card);

            // When
            card.CreateDate = new DateTime(1961, 3, 18);
            card.StateChangedDate = new DateTime(1961, 3, 18);
            card.ReleaseDate = new DateTime(1961, 3, 18);
            await dbCards.Update(card, nameof(Card.CreateDate), card.CreateDate);
            await dbCards.Update(card, nameof(Card.StateChangedDate), card.StateChangedDate);
            await dbCards.Update(card, nameof(Card.ReleaseDate), card.ReleaseDate);

            // Then
            List<Card> cards = await dbCards.GetCards();
            Assert.AreEqual(card.CreateDate, cards[0].CreateDate);
            Assert.AreEqual(card.StateChangedDate, cards[0].StateChangedDate);
            Assert.AreEqual(card.ReleaseDate, cards[0].ReleaseDate);
        }

        [TestMethod]
        public async Task DeleteCardTest()
        {
            // Given
            ObjectId boardID = new ObjectId();
            Card card = new Card { Title = "Card", BoardID = boardID };
            await dbCards.Insert(card);

            // When
            DeleteResult result = await dbCards.Delete(card);

            // Then
            Assert.AreEqual(1, result.DeletedCount);
            List<Card> cards = await dbCards.GetCards();
            Assert.HasCount(0, cards);
        }

        [TestMethod]
        public async Task CascadeDeleteCardTest()
        {
            // Given
            ObjectId boardID = new ObjectId();
            Card card1 = new Card { Title = "Card1", BoardID = boardID };
            await dbCards.Insert(card1);
            Card card2 = new Card { Title = "Card2", BoardID = boardID };
            await dbCards.Insert(card2);

            // When
            DeleteResult result = await dbBoards.Delete(boardID);

            // Then
            // children cards shall be deleted as well
            List<Card> cards = await dbCards.GetCards();
            Assert.HasCount(0, cards);
        }

        [TestMethod]
        public async Task GetCardsOfBoardTest()
        {
            // Given
            List<ProcessStep> processSteps = new List<ProcessStep> { new ProcessStep { PhaseSeqNo = 0, Name = "Test" } };
            Board board = new Board { _id = new ObjectId(), ProcessSteps = processSteps };
            Card card1 = new Card { Title = "Card1", BoardID = board._id };
            await dbCards.Insert(card1);
            Card card2 = new Card { Title = "Card2", BoardID = board._id };
            await dbCards.Insert(card2);

            // When
            List<Card> cards = await dbCards.GetCards(board);

            // Then
            Assert.HasCount(2, cards);
        }
    }
}
