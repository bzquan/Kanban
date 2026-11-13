using Kanban.Model;
using Kanban.Services;
using MongoDB.Bson;

namespace Kanban.Api.Tests.Services;

[TestClass]
public class CardCommandServiceTest
{
    private Kanban.Model.Board _board = null!;

    [TestInitialize]
    public void Setup()
    {
        TestDatabase.Reset();
        TestDatabase.WireModel(TestDatabase.NewClient());
        _board = TestDatabase.NewBoardCommandService().CreateBoardAsync("en").GetAwaiter().GetResult();
    }

    private static CardCommandService Svc() => TestDatabase.NewCardCommandService();

    private static string Format(Kanban.Repository.Activity a) =>
        $"({a.WorkState.ProcessStepSeqNo},{(a.WorkState.IsWIP ? "W" : "D")})";

    private async Task<Model.Card> CreateCard(int step, bool wip) =>
        await Svc().CreateCardAsync(_board._id, step, wip);

    // ---- 最重要テスト: Forward移動の中間Activity合成(既存 CreateActivities4IntermediateProcesses) ----
    // (0,W) → (2,D): 履歴は (0,W)[作成],(0,D)[WIP解除],(1,W),(1,D),(2,W),(2,D) と
    // ギャップなし・StateChangedDate単調増加で積まれる(計画ドラフトの4件配列は
    // WIP解除(0,D)と最終(2,D)の欠落 → 既存Model実動に合わせ6件で固定。Ruling参照)
    [TestMethod]
    public async Task MoveCard_Forward_TwoSteps_SynthesizesGapFreeActivities()
    {
        var card = await CreateCard(0, wip: true);
        await Svc().MoveCardAsync(card._id, new MoveCardRequest { ProcessStepSeqNo = 2, IsWip = false, Index = 0 });

        var acts = await TestDatabase.GetActivities(card._id);
        CollectionAssert.AreEqual(
            new[] { "(0,W)", "(0,D)", "(1,W)", "(1,D)", "(2,W)", "(2,D)" },
            acts.Select(Format).ToArray(),
            "actual: [" + string.Join(",", acts.Select(a => $"{Format(a)}@{a.StateChangedDate:HH:mm:ss.fff}")) + "]");

        for (int i = 1; i < acts.Count; i++)
        {
            Assert.IsTrue(acts[i].StateChangedDate > acts[i - 1].StateChangedDate,
                          $"activity {i} must postdate activity {i - 1}");
        }
    }

    [TestMethod]
    public async Task MoveCard_Backward_SingleActivity()
    {
        var card = await CreateCard(2, wip: false);
        var before = (await TestDatabase.GetActivities(card._id)).Count;

        await Svc().MoveCardAsync(card._id, new MoveCardRequest { ProcessStepSeqNo = 0, IsWip = true, Index = 0 });

        var after = await TestDatabase.GetActivities(card._id);
        Assert.HasCount(before + 1, after);              // 追加Activityは1件だけ(既存Modelの後方移動)
        Assert.AreEqual("(0,W)", Format(after[^1]),
            "actual: [" + string.Join(",", after.Select(a => $"{Format(a)}@{a.StateChangedDate:HH:mm:ss.fff}")) + "]");
    }

    [TestMethod]
    public async Task ReorderWithinSameColumn_UpdatesSeqNoOnly()
    {
        var a = await CreateCard(0, wip: true);
        var b = await CreateCard(0, wip: true);
        var c = await CreateCard(0, wip: true);
        var cActivitiesBefore = (await TestDatabase.GetActivities(c._id)).Count;

        // c を列先頭(index=0)へ並べ替え(同一列内 → Activityなし・SeqNoのみ変更・既存WPF同じ)
        await Svc().MoveCardAsync(c._id, new MoveCardRequest { ProcessStepSeqNo = 0, IsWip = true, Index = 0 });

        Assert.AreEqual(cActivitiesBefore, (await TestDatabase.GetActivities(c._id)).Count);
        var column = await TestDatabase.GetColumnCards(_board, step: 0, wip: true);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, column.Select(x => x.SeqNo).ToArray());
        Assert.AreEqual(c._id, column[0]._id);
    }

    // ---- Review Focus 3: 列範囲外move → ArgumentOutOfRangeException ----
    [TestMethod]
    public async Task MoveCard_InvalidStep_ThrowsOutOfRange()
    {
        var card = await CreateCard(0, wip: true);
        await ThrowsAsync<ArgumentOutOfRangeException>(
            () => Svc().MoveCardAsync(card._id, new MoveCardRequest { ProcessStepSeqNo = 99, IsWip = true, Index = 0 }));
        await ThrowsAsync<ArgumentOutOfRangeException>(
            () => Svc().MoveCardAsync(card._id, new MoveCardRequest { ProcessStepSeqNo = -1, IsWip = true, Index = 0 }));
    }

    // ---- Review Focus 6: 同一カード/同列への並行moveは last-write-wins、列SeqNoは 0..N-1 に収まる ----
    [TestMethod]
    public async Task ConcurrentMoves_LeaveColumnCompacted()
    {
        var a = await CreateCard(0, wip: true);
        var b = await CreateCard(0, wip: true);

        await Task.WhenAll(
            Svc().MoveCardAsync(a._id, new MoveCardRequest { ProcessStepSeqNo = 2, IsWip = true, Index = 0 }),
            Svc().MoveCardAsync(b._id, new MoveCardRequest { ProcessStepSeqNo = 2, IsWip = true, Index = 0 }));

        var column = await TestDatabase.GetColumnCards(_board, step: 2, wip: true);
        Assert.HasCount(2, column);
        CollectionAssert.AreEqual(new[] { 0, 1 }, column.Select(x => x.SeqNo).ToArray());
    }

    [TestMethod]
    public async Task SetBlocked_EmitsActivityWithBlockedSnapshot()
    {
        var card = await CreateCard(1, wip: true);
        var before = (await TestDatabase.GetActivities(card._id)).Count;

        await Svc().UpdateFieldsAsync(card._id, new CardPatch { IsBlocked = true });

        var after = await TestDatabase.GetActivities(card._id);
        Assert.HasCount(before + 1, after);                   // 同一ステップの新Activity
        Assert.IsTrue(after[^1].WorkState.IsBlocked);        // blockedスナップショット
        Assert.AreEqual(1, after[^1].WorkState.ProcessStepSeqNo);
    }

    [TestMethod]
    public async Task DeleteCard_RemovesCardAndItsActivities()
    {
        var card = await CreateCard(0, wip: true);
        Assert.IsNotEmpty(await TestDatabase.GetActivities(card._id));

        await Svc().DeleteCardAsync(card._id);

        Assert.HasCount(0, await TestDatabase.GetColumnCards(_board, step: 0, wip: true));
        Assert.HasCount(0, await TestDatabase.GetActivities(card._id));
    }

    private static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            Assert.Fail($"expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }
        Assert.Fail($"expected {typeof(TException).Name}, but no exception was thrown");
    }
}
