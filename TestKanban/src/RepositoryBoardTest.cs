using Kanban.Repository;

namespace UnitTest
{
    [TestClass]
    public class RepositoryBoardTest
    {
        IDBClient dbClient;
        IDBBoard dbBoards;

        [TestInitialize]
        public void Setup()
        {
            dbClient = new DBClient();
            dbClient.DBName = "TestDB";
            dbClient.MongoDBClient.DropDatabase(dbClient.DBName);
            IDBActivity dbActivities = new DBActivities(dbClient);
            IDBCard dbCards = new DBCards(dbClient, dbActivities);

            dbBoards = new DBBoards(dbClient, dbCards, dbActivities);
        }

        [TestCleanup]
        public void TearDown()
        {
            dbClient.MongoDBClient.DropDatabase(dbClient.DBName);
        }

        [TestMethod]
        public async Task CreateBoard()
        {
            // Given
            Board board = new Board { Title = "boardName", Description = "Description" };

            // When
            await dbBoards.Insert(board);

            // Then
            List<Board> boards = await dbBoards.GetBoards();
            Assert.HasCount(1, boards);
            Assert.AreEqual("boardName", boards[0].Title);
            Assert.AreEqual("Description", boards[0].Description);
        }

        [TestMethod]
        public async Task GetBoardsTest()
        {
            // Given

            // When
            List<Board> boards = await dbBoards.GetBoards();

            // Then
            Assert.IsEmpty(boards);
        }

        [TestMethod]

        public async Task SaveBoardTest()
        {
            // Given
            Board board = new Board { Title = "boardName", Description = "Description" };
            await dbBoards.Insert(board);

            // When
            board.Title = "New Board Title";
            await dbBoards.Save(board);

            // Then
            List<Board> boards = await dbBoards.GetBoards();
            Assert.HasCount(1, boards);
            Assert.AreEqual("New Board Title", boards[0].Title);
        }

        [TestMethod]
        public async Task UpdateFieldTest()
        {
            // Given
            Board board = new Board { Title = "boardName", Description = "Description" };
            await dbBoards.Insert(board);

            // When
            await dbBoards.UpdateAsync(board, nameof(Board.Title), "New Board Title");

            // Then
            List<Board> boards = await dbBoards.GetBoards();
            Assert.HasCount(1, boards);
            Assert.AreEqual("New Board Title", boards[0].Title);
        }

        [TestMethod]
        public async Task DeleteBoardTest()
        {
            // Given
            Board board = new Board { Title = "boardName", Description = "Description" };
            await dbBoards.Insert(board);

            // When
            await dbBoards.Delete(board._id);

            // Then
            List<Board> boards = await dbBoards.GetBoards();
            Assert.HasCount(0, boards);
        }
    }
}
