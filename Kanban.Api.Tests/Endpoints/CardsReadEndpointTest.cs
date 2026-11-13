using System.Net;
using System.Net.Http.Json;
using Kanban.Api;
using Kanban.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using RepoCard = Kanban.Repository.Card;

namespace Kanban.Api.Tests.Endpoints;

[TestClass]
public class CardsReadEndpointTest
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;
    private ObjectId _boardId;

    [TestInitialize]
    public async Task Setup()
    {
        TestDatabase.Reset();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient();

        // ボードは実クライアントと同じAPI経由で作成(既定5列: 0..4)
        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage = "en" });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        _boardId = ObjectId.Parse((await resp.Content.ReadFromJsonAsync<BoardDto>())!.Id);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _app.Dispose();
    }

    private RepoCard NewCard(
        string title,
        int seqNo = 0,
        int step = 0,
        bool wip = true,
        DateTime? releaseDate = null,
        DateTime? stateChangedDate = null,
        bool onBackBoard = false,
        DateTime? onBackBoardDate = null,
        string executors = "",
        string tag = "") => new()
        {
            BoardID = _boardId,
            Title = title,
            SeqNo = seqNo,
            WorkState = new Kanban.Repository.WorkState { ProcessStepSeqNo = step, IsWIP = wip },
            ReleaseDate = releaseDate ?? new DateTime(2020, 1, 1),
            StateChangedDate = stateChangedDate ?? new DateTime(2020, 1, 1),
            IsOnBackBoard = onBackBoard,
            OnBackBoardDate = onBackBoardDate ?? new DateTime(2020, 1, 1),
            Executors = executors,
            Tag = tag,
        };

    private async Task InsertCards(params RepoCard[] cards) =>
        await TestDatabase.NewClient().DB.GetCollection<RepoCard>("Cards").InsertManyAsync(cards);

    private async Task<List<CardDto>> GetCardsAsync(
        int? step = null, bool? wip = null, string? filter = null, string? onBoard = null)
    {
        var query = new List<string>();
        if (step is not null) query.Add($"processStepSeqNo={step}");
        if (wip is not null) query.Add($"isWip={wip.ToString().ToLowerInvariant()}");
        if (filter is not null) query.Add($"filter={Uri.EscapeDataString(filter)}");
        if (onBoard is not null) query.Add($"onBoard={onBoard}");
        var url = $"/api/boards/{_boardId}/cards" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        var resp = await _client.GetAsync(url);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, $"GET {url}");
        return (await resp.Content.ReadFromJsonAsync<List<CardDto>>())!;
    }

    // ---- テスト1: 列(0,WIP)に SeqNo 2,0,1 で投入 → 0,1,2 順で返る(既存 AdjustCardSeqNo 圧縮) ----
    [TestMethod]
    public async Task GetCards_ReturnsSeqNoAscending_CompactingGaps()
    {
        await InsertCards(NewCard("c2", seqNo: 2), NewCard("c0", seqNo: 0), NewCard("c1", seqNo: 1));
        var cards = await GetCardsAsync();
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, cards.Select(c => c.SeqNo).ToArray());
    }

    // ---- テスト2: 語間OR・Title/Tagはlowercase部分一致・Executorsはそのまま(既存DBCards意味論) ----
    [TestMethod]
    public async Task GetCards_Filter_MatchesTitleExecutorsTag_OrBetweenWords()
    {
        await InsertCards(
            NewCard("alpha item"),                          // Title一致
            NewCard("beta card", executors: "beta-dev"),    // Executors一致(小文字で照合)
            NewCard("gamma card", tag: "gamma-tag"),        // Tag一致
            NewCard("unrelated card"));                     // 不一致
        var cards = await GetCardsAsync(filter: "alpha beta gamma");
        CollectionAssert.AreEquivalent(
            new[] { "alpha item", "beta card", "gamma card" },
            cards.Select(c => c.Title).ToArray());
    }

    // ---- テスト3(Review Focus 2): filter=20261201 はリリース日当日のみ / 空なら全件 ----
    [TestMethod]
    public async Task GetCards_FilterReleaseDate_MatchesOnlyThatDay_EmptyFilterReturnsAll()
    {
        await InsertCards(
            NewCard("release card", releaseDate: new DateTime(2026, 12, 1, 10, 0, 0)),
            NewCard("other one", releaseDate: new DateTime(2026, 12, 2, 10, 0, 0)),
            NewCard("another one", releaseDate: new DateTime(2026, 11, 30, 10, 0, 0)));

        var byDate = await GetCardsAsync(filter: "20261201");
        Assert.HasCount(1, byDate);
        Assert.AreEqual("release card", byDate[0].Title);

        var all = await GetCardsAsync(filter: "");
        Assert.HasCount(3, all);
    }

    // ---- テスト4: onBoard=back は IsOnBackBoard=true かつ OnBackBoardDate 180日以内のみ ----
    [TestMethod]
    public async Task GetCards_BackBoard_FiltersByFlagAnd180DayWindow()
    {
        await InsertCards(
            NewCard("recent back", onBackBoard: true, onBackBoardDate: DateTime.Now.AddDays(-10)),
            NewCard("old back", onBackBoard: true, onBackBoardDate: DateTime.Now.AddDays(-200)),
            NewCard("front card", onBackBoard: false, onBackBoardDate: DateTime.Now));

        var back = await GetCardsAsync(onBoard: "back");
        Assert.HasCount(1, back);
        Assert.AreEqual("recent back", back[0].Title);
    }

    // ---- テスト5: 最終列Doneは StateChangedDate降順 Limit=20(既存 LimitNum4QueringLastProcess) ----
    [TestMethod]
    public async Task GetCards_LastColumnDone_Top20ByStateChangedDateDescending()
    {
        var baseDate = new DateTime(2026, 1, 1);
        await InsertCards(Enumerable.Range(0, 25)
                                    .Select(i => NewCard($"done {i}", seqNo: i, step: 4, wip: false,
                                                          stateChangedDate: baseDate.AddMinutes(i)))
                                    .ToArray());

        var cards = await GetCardsAsync(step: 4, wip: false, onBoard: "front");
        Assert.HasCount(20, cards);
        Assert.AreEqual("done 24", cards[0].Title);
        CollectionAssert.AreEqual(
            cards.Select(c => c.StateChangedDate).OrderByDescending(d => d).ToList(),
            cards.Select(c => c.StateChangedDate).ToList());
    }

    // ---- Task 3 で約束した挙動証跡(台帳 Ruling で先送りになっていたもの): Kanban:LimitNum4QueringLastProcess
    // の上書きが実際に下流(DBCards の最終列Done取得)で消費されること。既定20と区別できる値 3 を要求する。
    [TestMethod]
    public async Task GetCards_LastColumnDone_RespectsLimitOverrideOf3()
    {
        var baseDate = new DateTime(2026, 1, 1);
        await InsertCards(Enumerable.Range(0, 25)
                                    .Select(i => NewCard($"done {i}", seqNo: i, step: 4, wip: false,
                                                          stateChangedDate: baseDate.AddMinutes(i)))
                                    .ToArray());

        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
            b.UseSetting("Kanban:LimitNum4QueringLastProcess", "3");
        });
        var resp = await app.CreateClient()
                            .GetAsync($"/api/boards/{_boardId}/cards?processStepSeqNo=4&isWip=false&onBoard=front");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var cards = (await resp.Content.ReadFromJsonAsync<List<CardDto>>())!;

        Assert.HasCount(3, cards);
        Assert.AreEqual("done 24", cards[0].Title); // StateChangedDate 降順は維持
    }

    // ---- 错误系: ボードid不正→400、未在→404(ボード読み取りと共通の振る舞い) ----
    [TestMethod]
    public async Task GetCards_InvalidBoardId_Returns400()
    {
        var resp = await _client.GetAsync("/api/boards/xyz/cards");
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task GetCards_NonexistentBoard_Returns404()
    {
        var resp = await _client.GetAsync($"/api/boards/{ObjectId.GenerateNewId()}/cards");
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
