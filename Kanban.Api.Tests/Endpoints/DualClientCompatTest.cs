using System.Net;
using System.Net.Http.Json;
using Kanban.Api;
using Kanban.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;

namespace Kanban.Api.Tests.Endpoints;

/// <summary>
/// 設計書リスク表「WPF版現役共存」の検証:
/// API 側で作成→move→blocked→削除したボードを、Model 層(WPF と同じ CardRepository.GetCards /
/// BoardHistoryBuilder の読み取り経路)で読み直し、SeqNo 0..N-1 連続・Activities ギャップなし・
/// メトリクス例外なし をアサートする。
/// </summary>
[TestClass]
public class DualClientCompatTest
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;
    private Kanban.Model.Board _board = null!;

    [TestInitialize]
    public async Task Setup()
    {
        TestDatabase.Reset();
        TestDatabase.WireModel(TestDatabase.NewClient());
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient();

        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage = "en" });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        var createdId = ObjectId.Parse((await resp.Content.ReadFromJsonAsync<BoardDto>())!.Id);
        _board = (await TestDatabase.NewBoardCommandService().GetBoardsAsync()).Single(b => b._id == createdId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _app.Dispose();
    }

    private async Task<CardDto> CreateCardAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/cards",
            new { boardId = _board._id.ToString(), processStepSeqNo = 0, isWip = true });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<CardDto>())!;
    }

    // 連続 write の Activities 逆転(既存 WorkingChangeDateTime 意味論)を避ける pacing 込み
    private async Task MoveAsync(string cardId, int step, bool wip, int index)
    {
        var resp = await _client.PatchAsJsonAsync($"/api/cards/{cardId}/move",
            new { processStepSeqNo = step, isWip = wip, index });
        Assert.IsTrue(resp.IsSuccessStatusCode, $"move: {(int)resp.StatusCode}");
        await Task.Delay(40);
    }

    private async Task PatchAsync(string cardId, object patch)
    {
        var resp = await _client.PatchAsJsonAsync($"/api/cards/{cardId}", patch);
        Assert.IsTrue(resp.IsSuccessStatusCode, $"patch: {(int)resp.StatusCode}");
        await Task.Delay(40);
    }

    [TestMethod]
    public async Task ApiWrites_AreConsistentWhenReadThroughWpfModelLayer()
    {
        // ---- WPF 側クライアント(API)による書き込み ----
        var c1 = await CreateCardAsync();
        var c2 = await CreateCardAsync();
        await MoveAsync(c1.Id, step: 2, wip: true, index: 0);
        await MoveAsync(c2.Id, step: 2, wip: true, index: 0);
        await PatchAsync(c1.Id, new { title = "blocked card", isBlocked = true });   // blocked活動が積まれる

        var deleteResp = await _client.DeleteAsync($"/api/cards/{c2.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // ---- Model 層(WPF と同一経路)で読み直し ----
        // 移動先列: c2 削除後も SeqNo が 0..N-1 連続
        var column = await TestDatabase.GetColumnCards(_board, step: 2, wip: true);
        Assert.HasCount(1, column);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, column.Count).ToArray(),
            column.Select(c => c.SeqNo).ToArray());
        Assert.AreEqual("blocked card", column[0].Title);

        // 移動元列: 削除・移出後も 0..N-1 連続
        var col0 = await TestDatabase.GetColumnCards(_board, step: 0, wip: true);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, col0.Count).ToArray(),
            col0.Select(c => c.SeqNo).ToArray());

        // 監査証跡: StateChangedDate 昇順でギャップなし(単調増加)・blockedスナップショット含む
        var acts = await TestDatabase.GetActivities(column[0]._id);
        Assert.IsGreaterThanOrEqualTo(6, acts.Count, "expected create+close+intermediate+arrival+blocked activities");
        for (int i = 1; i < acts.Count; i++)
        {
            Assert.IsTrue(acts[i].StateChangedDate > acts[i - 1].StateChangedDate,
                          $"activity {i} must postdate {i - 1}");
        }
        Assert.IsTrue(acts.Any(a => a.WorkState.IsBlocked));

        // ---- 既存 BoardHistoryBuilder / Metrics 経路で例外なし(WPFメトリクス共存) ----
        var metrics = TestDatabase.NewMetricsQueryService();
        var cfd = await metrics.GetCfdAsync(_board._id);
        Assert.IsTrue(cfd.Series.All(s => s.CumulativeFlowCounts.Length == cfd.Days.Count));
        _ = await metrics.GetCycleTimeAsync(_board._id);
        _ = await metrics.GetThroughputAsync(_board._id);
        _ = await metrics.GetBugsBlockedAsync(_board._id);
        _ = await metrics.GetVelocityAsync(_board._id);
    }
}
