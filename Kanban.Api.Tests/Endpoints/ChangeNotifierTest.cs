using System.Net;
using System.Net.Http.Json;
using Kanban.Api;
using Kanban.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace Kanban.Api.Tests.Endpoints;

/// <summary>将来の SignalR 実装は IBoardChangeNotifier の差し替えだけで済む(設計書§4.4)ことを固定する。</summary>
public class TestNotifier : Kanban.Services.IBoardChangeNotifier
{
    public List<ObjectId> Calls { get; } = new();
    public void BoardChanged(ObjectId boardId) => Calls.Add(boardId);
}

[TestClass]
public class ChangeNotifierTest
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;
    private TestNotifier _notifier = null!;
    private ObjectId _boardId;

    [TestInitialize]
    public async Task Setup()
    {
        TestDatabase.Reset();
        _notifier = new TestNotifier();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
            // 後勝ち: NoOp を差し替えて TestNotifier を singleton として使う
            b.ConfigureServices(services => services.AddSingleton<Kanban.Services.IBoardChangeNotifier>(_notifier));
        });
        _client = _app.CreateClient();

        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage = "en" });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        _boardId = ObjectId.Parse((await resp.Content.ReadFromJsonAsync<BoardDto>())!.Id);
        _notifier.Calls.Clear();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _app.Dispose();
    }

    [TestMethod]
    public async Task CreateCard_InvokesBoardChangedOnceWithBoardId()
    {
        var resp = await _client.PostAsJsonAsync("/api/cards",
            new { boardId = _boardId.ToString(), processStepSeqNo = 0, isWip = true });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);

        Assert.HasCount(1, _notifier.Calls);
        Assert.AreEqual(_boardId, _notifier.Calls[0]);
    }

    [TestMethod]
    public async Task ReadEndpoints_DoNotInvokeNotifier()
    {
        await _client.GetAsync("/api/boards");
        await _client.GetAsync($"/api/boards/{_boardId}/cards");
        await _client.GetAsync($"/api/boards/{_boardId}/metrics/cfd");

        Assert.HasCount(0, _notifier.Calls);
    }

    [TestMethod]
    public async Task DeleteCard_InvokesBoardChangedOnceWithBoardId()
    {
        var resp = await _client.PostAsJsonAsync("/api/cards",
            new { boardId = _boardId.ToString(), processStepSeqNo = 0, isWip = true });
        var card = (await resp.Content.ReadFromJsonAsync<CardDto>())!;
        _notifier.Calls.Clear();

        var deleteResp = await _client.DeleteAsync($"/api/cards/{card.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResp.StatusCode);

        Assert.HasCount(1, _notifier.Calls);
        Assert.AreEqual(_boardId, _notifier.Calls[0]);
    }

    [TestMethod]
    public async Task MoveCard_InvokesBoardChangedOnceWithBoardId()
    {
        var resp = await _client.PostAsJsonAsync("/api/cards",
            new { boardId = _boardId.ToString(), processStepSeqNo = 0, isWip = true });
        var card = (await resp.Content.ReadFromJsonAsync<CardDto>())!;
        _notifier.Calls.Clear();

        var moveResp = await _client.PatchAsJsonAsync(
            $"/api/cards/{card.Id}/move", new { processStepSeqNo = 1, isWip = true, index = 0 });
        Assert.IsTrue(moveResp.IsSuccessStatusCode);

        Assert.HasCount(1, _notifier.Calls);
        Assert.AreEqual(_boardId, _notifier.Calls[0]);
    }
}
