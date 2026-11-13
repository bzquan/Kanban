using System.Net;
using System.Net.Http.Json;
using Kanban.Api;
using Kanban.Api.Contracts;
using Kanban.Repository;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Kanban.Api.Tests.Endpoints;

[TestClass]
public class BoardsEndpointTest
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        TestDatabase.Reset();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _app.Dispose();
    }

    private async Task<BoardDto> CreateBoardAsync(string uiLanguage = "en")
    {
        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        var dto = (await resp.Content.ReadFromJsonAsync<BoardDto>())!;
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        return dto;
    }

    private async Task<List<BoardDto>> GetBoardsAsync()
    {
        var resp = await _client.GetAsync("/api/boards");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<List<BoardDto>>())!;
    }

    [TestMethod]
    public async Task Post_Get_Put_Delete_Roundtrip()
    {
        // POST(ja) → 201 + ローカライズ済み既定5列 + camelCase
        var created = await CreateBoardAsync("ja");
        Assert.AreEqual("新規プロジェクト", created.Title);
        Assert.HasCount(5, created.ProcessSteps);
        StringAssert.Contains(created.ProcessSteps[2].Name, "開発");
        var rawBody = await (await _client.GetAsync("/api/boards")).Content.ReadAsStringAsync();
        StringAssert.Contains(rawBody, "\"processSteps\""); // レスポンスは camelCase JSON

        // GET → SeqNo昇順で作成ボードを含む
        var boards = await GetBoardsAsync();
        Assert.IsTrue(boards.Any(b => b.Id == created.Id));

        // PUT → 列1削除+リネーム → 200 + 更新後BoardDto(SeqNo 0..3 に圧縮)
        var newSteps = created.ProcessSteps
                              .Where(s => s.PhaseSeqNo != 1)
                              .Select(s => new ProcessStepDto
                              {
                                  PhaseSeqNo = s.PhaseSeqNo,
                                  LabelColor = s.LabelColor,
                                  Name = s.PhaseSeqNo == 0 ? "Renamed Backlog" : s.Name,
                                  WIPTitle = s.WIPTitle,
                                  DoneTitle = s.DoneTitle,
                                  IsStartOfCycleTime = s.IsStartOfCycleTime,
                                  IsEndOfCycleTime = s.IsEndOfCycleTime,
                              })
                              .ToList();
        var putResp = await _client.PutAsJsonAsync($"/api/boards/{created.Id}/process-steps", newSteps);
        Assert.AreEqual(HttpStatusCode.OK, putResp.StatusCode);
        var updated = (await putResp.Content.ReadFromJsonAsync<BoardDto>())!;
        Assert.HasCount(4, updated.ProcessSteps);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            updated.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Select(s => s.PhaseSeqNo).ToArray());
        Assert.AreEqual("Renamed Backlog", updated.ProcessSteps[0].Name);

        // DELETE → 204、その後 GET は WPFパリティで1枚自動作成
        var deleteResp = await _client.DeleteAsync($"/api/boards/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResp.StatusCode);
        var after = await GetBoardsAsync();
        Assert.HasCount(1, after); // 0枚になると LoadBoards が自動作成(既存WPF動作)
    }

    [TestMethod]
    public async Task Post_InvalidLanguage_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage = "fr" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Get_Empty_AutoCreatesOneBoard()
    {
        // 既存 BoardRepository.LoadBoards の副作用パリティ: Reset 後の GET で1枚できる
        var boards = await GetBoardsAsync();
        Assert.HasCount(1, boards);
    }

    // ---- Review Focus 1: 存在しない/不正な ObjectId → 400/404(500ではない) ----

    [TestMethod]
    public async Task Delete_InvalidId_Returns400()
    {
        var resp = await _client.DeleteAsync("/api/boards/not-a-valid-objectid");
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Delete_Nonexistent_Returns404()
    {
        var resp = await _client.DeleteAsync($"/api/boards/{ObjectId.GenerateNewId()}");
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Put_InvalidId_Returns400()
    {
        var resp = await _client.PutAsJsonAsync("/api/boards/xyz/process-steps", new List<ProcessStepDto>());
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Put_TooFewSteps_Returns409()
    {
        var created = await CreateBoardAsync();
        var tooFew = created.ProcessSteps.Take(2).ToList();
        var resp = await _client.PutAsJsonAsync($"/api/boards/{created.Id}/process-steps", tooFew);
        Assert.AreEqual(HttpStatusCode.Conflict, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "TooFewSteps");

        // 無変更(5列)
        var boards = await GetBoardsAsync();
        Assert.HasCount(5, boards.Single(b => b.Id == created.Id).ProcessSteps);
    }

    [TestMethod]
    public async Task Put_StepInUse_Returns409AndLeavesStepsUnchanged()
    {
        var created = await CreateBoardAsync();

        // 列1にカードを直接投入(カード残存列の削除ガード Review Focus 4)
        var client = TestDatabase.NewClient();
        await client.DB.GetCollection<Card>("Cards").InsertOneAsync(new Card
        {
            BoardID = ObjectId.Parse(created.Id),
            Title = "card in column 1",
            WorkState = new WorkState { ProcessStepSeqNo = 1, IsWIP = true },
        });

        var newSteps = created.ProcessSteps
                              .Where(s => s.PhaseSeqNo != 1)
                              .Select(s => new ProcessStepDto
                              {
                                  PhaseSeqNo = s.PhaseSeqNo,
                                  LabelColor = s.LabelColor,
                                  Name = s.Name,
                                  WIPTitle = s.WIPTitle,
                                  DoneTitle = s.DoneTitle,
                                  IsStartOfCycleTime = s.IsStartOfCycleTime,
                                  IsEndOfCycleTime = s.IsEndOfCycleTime,
                              })
                              .ToList();
        var resp = await _client.PutAsJsonAsync($"/api/boards/{created.Id}/process-steps", newSteps);
        Assert.AreEqual(HttpStatusCode.Conflict, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "StepInUse");

        var boards = await GetBoardsAsync();
        Assert.HasCount(5, boards.Single(b => b.Id == created.Id).ProcessSteps);
    }

    // ---- 仕様書§4.3「並べ替え」(最終レビュー指摘: 400 で拒否していたのは仕様ギャップ) ----
    // クライアントは GET 時点の PhaseSeqNo を身分別子に保ったまま、新しい表示順でリストを送る。
    // カード/Activities は既存 ChangeCardsAndActivitySeqNo で列追従する。

    private async Task<BoardDto> PutStepsAsync(BoardDto created, int[] orderByIdentity)
    {
        var byId = created.ProcessSteps.ToDictionary(s => s.PhaseSeqNo);
        var newSteps = orderByIdentity
                          .Select(id => new ProcessStepDto
                          {
                              PhaseSeqNo = id,
                              LabelColor = byId[id].LabelColor,
                              Name = byId[id].Name,
                              WIPTitle = byId[id].WIPTitle,
                              DoneTitle = byId[id].DoneTitle,
                              IsStartOfCycleTime = byId[id].IsStartOfCycleTime,
                              IsEndOfCycleTime = byId[id].IsEndOfCycleTime,
                          })
                          .ToList();
        var resp = await _client.PutAsJsonAsync($"/api/boards/{created.Id}/process-steps", newSteps);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, $"PUT: {(int)resp.StatusCode} {body}");
        return (await resp.Content.ReadFromJsonAsync<BoardDto>())!;
    }

    private async Task<ObjectId> SeedCardInColumnAsync(ObjectId boardId, int step, string title)
    {
        var cardId = ObjectId.GenerateNewId();
        await TestDatabase.NewClient().DB.GetCollection<Card>("Cards").InsertOneAsync(new Card
        {
            _id = cardId,
            BoardID = boardId,
            Title = title,
            SeqNo = 0,
            WorkState = new WorkState { ProcessStepSeqNo = step, IsWIP = true },
            StateChangedDate = new DateTime(2020, 1, 1),
            ReleaseDate = new DateTime(2020, 1, 1),
        });
        await TestDatabase.NewClient().DB.GetCollection<Activity>("Activities").InsertOneAsync(new Activity
        {
            BoardID = boardId,
            CardID = cardId,
            WorkState = new WorkState { ProcessStepSeqNo = step, IsWIP = true },
            StateChangedDate = new DateTime(2020, 1, 1),
        });
        return cardId;
    }

    private async Task<List<string>> GetColumnTitlesAsync(ObjectId boardId, int step)
    {
        var resp = await _client.GetAsync($"/api/boards/{boardId}/cards?processStepSeqNo={step}&isWip=true");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<List<CardDto>>())!.Select(c => c.Title).ToList();
    }

    [TestMethod]
    public async Task Put_ReorderColumns_Swap_CardsFollowTheirColumn()
    {
        var created = await CreateBoardAsync();
        var boardId = ObjectId.Parse(created.Id);
        await SeedCardInColumnAsync(boardId, step: 1, "card@1");
        await SeedCardInColumnAsync(boardId, step: 2, "card@2");
        await SeedCardInColumnAsync(boardId, step: 3, "card@3");

        // 列1↔2 を入れ替え(身分=元PhaseSeqNo を保ったまま表示順 [0,2,1,3,4] で送る)
        var updated = await PutStepsAsync(created, new[] { 0, 2, 1, 3, 4 });

        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4 },
            updated.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Select(s => s.PhaseSeqNo).ToArray());
        Assert.AreEqual(
            created.ProcessSteps.Single(s => s.PhaseSeqNo == 2).Name,
            updated.ProcessSteps.Single(s => s.PhaseSeqNo == 1).Name);
        Assert.AreEqual(
            created.ProcessSteps.Single(s => s.PhaseSeqNo == 1).Name,
            updated.ProcessSteps.Single(s => s.PhaseSeqNo == 2).Name);

        CollectionAssert.AreEquivalent(new[] { "card@2" }, await GetColumnTitlesAsync(boardId, 1));
        CollectionAssert.AreEquivalent(new[] { "card@1" }, await GetColumnTitlesAsync(boardId, 2));
        CollectionAssert.AreEquivalent(new[] { "card@3" }, await GetColumnTitlesAsync(boardId, 3));
    }

    [TestMethod]
    public async Task Put_ReorderColumns_ThreeColumnRotation_CardsAndActivitiesFollow()
    {
        var created = await CreateBoardAsync();
        var boardId = ObjectId.Parse(created.Id);
        var c1 = await SeedCardInColumnAsync(boardId, step: 1, "card@1");
        var c2 = await SeedCardInColumnAsync(boardId, step: 2, "card@2");
        var c3 = await SeedCardInColumnAsync(boardId, step: 3, "card@3");

        // 表示順 [0,3,1,2,4]: 身分1→pos2, 身分2→pos3, 身分3→pos1(3-サイクル)
        var updated = await PutStepsAsync(created, new[] { 0, 3, 1, 2, 4 });

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 },
            updated.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Select(s => s.PhaseSeqNo).ToArray());

        // カードは列追従
        CollectionAssert.AreEquivalent(new[] { "card@3" }, await GetColumnTitlesAsync(boardId, 1));
        CollectionAssert.AreEquivalent(new[] { "card@1" }, await GetColumnTitlesAsync(boardId, 2));
        CollectionAssert.AreEquivalent(new[] { "card@2" }, await GetColumnTitlesAsync(boardId, 3));

        // 監査証跡の WorkState.ProcessStepSeqNo も列の再採番に追従(既存 ChangeActivitiesSeqNo 意味論)
        var activities = TestDatabase.NewClient().DB.GetCollection<Activity>("Activities");
        foreach (var (cardId, expectedStep) in new[] { (c1, 2), (c2, 3), (c3, 1) })
        {
            var acts = await activities.Find(a => a.CardID == cardId).ToListAsync();
            Assert.HasCount(1, acts);
            Assert.AreEqual(expectedStep, acts[0].WorkState.ProcessStepSeqNo, $"activity of {cardId}");
        }
    }
}
