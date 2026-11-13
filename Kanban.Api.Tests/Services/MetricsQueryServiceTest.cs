using MongoDB.Bson;

namespace Kanban.Api.Tests.Services;

[TestClass]
public class MetricsQueryServiceTest
{
    private Kanban.Model.Board _board = null!;

    [TestInitialize]
    public void Setup()
    {
        TestDatabase.Reset();
        TestDatabase.WireModel(TestDatabase.NewClient());
        _board = TestDatabase.NewBoardCommandService().CreateBoardAsync("en").GetAwaiter().GetResult();
    }

    private Kanban.Services.MetricsQueryService Svc() => TestDatabase.NewMetricsQueryService();

    // 今週リリース済みカード(StoryPoints=5) → 終了列(3)の日次CFDにカウント
    [TestMethod]
    public async Task Cfd_ReleasedCard_CountedInEndStepSeriesLastDay()
    {
        await TestDatabase.SeedDoneCard(_board, storyPoints: 5);

        var cfd = await Svc().GetCfdAsync(_board._id);
        Assert.HasCount(64, cfd.Days);                                     // 今日23:59:59/8+1週間前(既存FromDate4Metrics)
        Assert.HasCount(3, cfd.Series);                                     // CycleTime列 1..3(既定5列ボード)
        var endStepName = _board.DevProcess.ProcessSteps[3].Name;
        var endSeries = cfd.Series.Single(s => s.StepName == endStepName);
        Assert.IsGreaterThanOrEqualTo(1, endSeries.CumulativeFlowCounts[^1], "tail: [" + string.Join(",", endSeries.CumulativeFlowCounts.TakeLast(5)) + "] days tail: [" + string.Join(",", cfd.Days.TakeLast(5)) + "]");
        Assert.IsGreaterThan(0, cfd.Summary.TotalDays);
    }

    // ベロシティ週次合計: StoryPoints 5 のカードを1枚リリース → 対象週=5(既存式: 6週合計/3)
    [TestMethod]
    public async Task Velocity_ReleasedStoryPoints5_LastWeekEquals5()
    {
        await TestDatabase.SeedDoneCard(_board, storyPoints: 5);

        var velocity = await Svc().GetVelocityAsync(_board._id);
        Assert.HasCount(9, velocity.StoryPoints);                    // H_SCALE_COUNT(9)×1週
        Assert.AreEqual(5, velocity.StoryPoints[^1]);
        Assert.IsFalse(velocity.HasUnknownStoryPoints[^1]);
        Assert.AreEqual(1, (int)velocity.AverageVelocity4Every2Weeks);      // 5/3 整数除算(既存式)
    }

    // blocked活動のあるカード → BlockedCounts ≥1(既存 VisualBugFixAndBlockedBuilder と同じ2系列)
    [TestMethod]
    public async Task BugsBlocked_BlockedCard_CountedToday()
    {
        var cardSvc = TestDatabase.NewCardCommandService();
        var card = await cardSvc.CreateCardAsync(_board._id, 1, true);
        await cardSvc.UpdateFieldsAsync(card._id, new Kanban.Services.CardPatch { IsBlocked = true });

        var bb = await Svc().GetBugsBlockedAsync(_board._id);
        Assert.HasCount(bb.Days.Count, bb.BlockedCounts);
        Assert.HasCount(bb.Days.Count, bb.BugFixCounts);
        Assert.IsGreaterThanOrEqualTo(1, bb.BlockedCounts[^1]);
        Assert.AreEqual(0, bb.BugFixCounts[^1]);                            // Featureカードなので BugFix=0
    }

    // Review Focus 5: 空ボード全系列ゼロ・例外なし
    [TestMethod]
    public async Task AllFiveMetrics_EmptyBoard_ReturnZerosWithoutException()
    {
        var svc = Svc();

        var cfd = await svc.GetCfdAsync(_board._id);
        Assert.IsTrue(cfd.Series.All(s => s.CumulativeFlowCounts.All(c => c == 0)));

        var cycleTime = await svc.GetCycleTimeAsync(_board._id);
        Assert.HasCount(9, cycleTime.WeekEnds);
        Assert.IsTrue(cycleTime.Weeks.All(w => w.Buckets.All(b => b.Percentage == 0)));

        var throughput = await svc.GetThroughputAsync(_board._id);
        Assert.IsTrue(throughput.ItemCounts.All(c => c == 0));

        var bugsBlocked = await svc.GetBugsBlockedAsync(_board._id);
        Assert.IsTrue(bugsBlocked.BlockedCounts.All(c => c == 0));
        Assert.IsTrue(bugsBlocked.BugFixCounts.All(c => c == 0));

        var velocity = await svc.GetVelocityAsync(_board._id);
        Assert.IsTrue(velocity.StoryPoints.All(p => p == 0));
    }

    [TestMethod]
    public async Task UnknownBoard_ThrowsKeyNotFound()
    {
        try
        {
            await Svc().GetCfdAsync(ObjectId.GenerateNewId());
        }
        catch (KeyNotFoundException)
        {
            return;
        }
        Assert.Fail("expected KeyNotFoundException");
    }
}
