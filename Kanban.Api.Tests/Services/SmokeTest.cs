using Kanban.Repository;
using MongoDB.Driver;

namespace Kanban.Api.Tests.Services;

[TestClass]
public class SmokeTest
{
    [TestInitialize]
    public void Setup() => TestDatabase.Reset();

    [TestMethod]
    public async Task ConnectAndDropKanbanTest()
    {
        var client = TestDatabase.NewClient();
        await client.DB.GetCollection<Board>("Boards").InsertOneAsync(new Board("t", "d"));
        var list = await client.DB.GetCollection<Board>("Boards").FindAsync(_ => true);
        Assert.HasCount(1, await list.ToListAsync());
        TestDatabase.Reset(); // Dropして消えること
        var after = await client.DB.GetCollection<Board>("Boards").CountDocumentsAsync(_ => true);
        Assert.AreEqual(0, after);
    }
}
