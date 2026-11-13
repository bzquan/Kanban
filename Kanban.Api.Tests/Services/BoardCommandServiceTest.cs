using Kanban.Repository;
using Kanban.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Kanban.Api.Tests.Services;

[TestClass]
public class BoardCommandServiceTest
{
    [TestInitialize]
    public void Setup()
    {
        TestDatabase.Reset();
        // 静的結線(Model.Board.BoardRepository 等)を kanban_test 向けに差し替え
        TestDatabase.WireModel(TestDatabase.NewClient());
    }

    private static List<ProcessStep> StepList(params int[] phaseSeqNos) =>
        phaseSeqNos.Select(n => new ProcessStep { PhaseSeqNo = n, Name = $"step{n}", WIPTitle = "w", DoneTitle = "d", LabelColor = "Aqua" }).ToList();

    // MSTest 4 には ThrowsExceptionAsync がないため明示ヘルパーで代替
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

    private async Task InsertCardAt(ObjectId boardId, int phaseSeqNo)
    {
        var client = TestDatabase.NewClient();
        await client.DB.GetCollection<Card>("Cards").InsertOneAsync(new Card
        {
            BoardID = boardId,
            Title = "card",
            WorkState = new WorkState { ProcessStepSeqNo = phaseSeqNo, IsWIP = true },
        });
    }

    [TestMethod]
    public async Task CreateBoardJp_GetsFiveLocalizedSteps()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("ja");
        Assert.HasCount(5, board.DevProcess.ProcessSteps);
        Assert.AreEqual(1, board.DevProcess.ProcessSteps[1].IsStartOfCycleTime ? 1 : 0);
        Assert.IsTrue(board.DevProcess.ProcessSteps[3].IsEndOfCycleTime);
        StringAssert.Contains(board.DevProcess.ProcessSteps[2].Name, "開発"); // ja.json の Development 列名
    }

    [TestMethod]
    public async Task CreateBoardZh_GetsLocalizedSteps()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("zh");
        StringAssert.Contains(board.DevProcess.ProcessSteps[2].Name, "开发"); // zh.json の Development 列名
        Assert.AreEqual("新项目", board.Title);
    }

    [TestMethod]
    public async Task CreateBoardUnknownLanguage_ThrowsArgumentException()
    {
        var svc = TestDatabase.NewBoardCommandService();
        await ThrowsAsync<ArgumentException>(() => svc.CreateBoardAsync("fr"));
    }

    [TestMethod]
    public async Task GetBoardsAsync_WhenEmpty_AutoCreatesOneBoard()
    {
        // 既存 BoardRepository.LoadBoards の「1件も無いと1枚自動作成」(WPF動作)パリティ
        var svc = TestDatabase.NewBoardCommandService();
        var boards = await svc.GetBoardsAsync();
        Assert.HasCount(1, boards);
    }

    [TestMethod]
    public async Task DeleteSteps_WhenGuardFails_ReturnsGuardWithoutChange()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");

        // ガード1: 3件未満 → TooFewSteps(既存 CanDeleteProcessStep のガード)
        var tooFew = await svc.UpdateProcessStepsAsync(board._id, StepList(0, 2));
        Assert.IsFalse(tooFew.Success);
        Assert.AreEqual(GuardReason.TooFewSteps, tooFew.Guard);

        // ガード2: カードが残る列の削除 → StepInUse
        await InsertCardAt(board._id, phaseSeqNo: 1);
        var inUse = await svc.UpdateProcessStepsAsync(board._id, StepList(0, 2, 3, 4));
        Assert.IsFalse(inUse.Success);
        Assert.AreEqual(GuardReason.StepInUse, inUse.Guard);

        // どちらのガードでも DB は無変更(5列のまま)
        var after = (await svc.GetBoardsAsync()).Single(b => b._id == board._id);
        Assert.HasCount(5, after.DevProcess.ProcessSteps);
    }

    [TestMethod]
    public async Task UpdateProcessSteps_DeleteAndRename_PersistsCompactedSeqNo()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");

        var newSteps = StepList(0, 2, 3, 4);          // 列1を削除
        newSteps[0].Name = "Renamed Backlog";          // リネーム
        var result = await svc.UpdateProcessStepsAsync(board._id, newSteps);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Update);

        var after = (await svc.GetBoardsAsync()).Single(b => b._id == board._id);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            after.DevProcess.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Select(s => s.PhaseSeqNo).ToArray());
        Assert.AreEqual("Renamed Backlog", after.DevProcess.ProcessSteps[0].Name);
    }

    [TestMethod]
    public async Task UpdateProcessSteps_AppendsNewStep()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");

        // 元 5列 → 1列追加(追加は seqNo >= 元個数 のエントリー)で合計6列、SeqNo は 0..5 に再圧縮
        var newSteps = StepList(0, 1, 2, 3, 4, 9);
        newSteps[5].Name = "Appended";
        var result = await svc.UpdateProcessStepsAsync(board._id, newSteps);
        Assert.IsTrue(result.Success);

        var after = (await svc.GetBoardsAsync()).Single(b => b._id == board._id);
        Assert.HasCount(6, after.DevProcess.ProcessSteps);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4, 5 },
            after.DevProcess.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Select(s => s.PhaseSeqNo).ToArray());
        Assert.AreEqual("Appended", after.DevProcess.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Last().Name);
    }

    [TestMethod]
    public async Task UpdateProcessSteps_Reorder_SucceedsAndKeepsIdentityNamesAtNewPositions()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");

        // 仕様書§4.3 の並べ替え: 身分=元PhaseSeqNo を保ったリスト順 [2,1,0,3,4] → 成功(最終レビュー指摘で 400 拒否を差し替え)
        var result = await svc.UpdateProcessStepsAsync(board._id, StepList(2, 1, 0, 3, 4));
        Assert.IsTrue(result.Success);

        var after = (await svc.GetBoardsAsync()).Single(b => b._id == board._id);
        Assert.HasCount(5, after.DevProcess.ProcessSteps);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4 },
            after.DevProcess.ProcessSteps.OrderBy(s => s.PhaseSeqNo).Select(s => s.PhaseSeqNo).ToArray());
        Assert.AreEqual("step2", after.DevProcess.ProcessSteps.Single(s => s.PhaseSeqNo == 0).Name);
        Assert.AreEqual("step0", after.DevProcess.ProcessSteps.Single(s => s.PhaseSeqNo == 2).Name);
    }

    [TestMethod]
    public async Task UpdateProcessSteps_DuplicateIdentity_ThrowsArgumentException()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");

        // 同一列(身分別子)を2箇所に置く試み → 不正入力 400 相当
        await ThrowsAsync<ArgumentException>(
            () => svc.UpdateProcessStepsAsync(board._id, StepList(1, 1, 2, 3, 4)));
    }

    [TestMethod]
    public async Task UpdateProcessSteps_InvalidCycleFlags_ThrowsArgumentException()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");

        var steps = StepList(0, 1, 2, 3, 4);
        steps[0].IsStartOfCycleTime = true;
        steps[1].IsStartOfCycleTime = true; // 開始列が2つ
        await ThrowsAsync<ArgumentException>(
            () => svc.UpdateProcessStepsAsync(board._id, steps));
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_ThrowsKeyNotFoundException()
    {
        var svc = TestDatabase.NewBoardCommandService();
        await ThrowsAsync<KeyNotFoundException>(
            () => svc.DeleteAsync(ObjectId.GenerateNewId()));
    }

    [TestMethod]
    public async Task DeleteAsync_Existing_RemovesBoard()
    {
        var svc = TestDatabase.NewBoardCommandService();
        var board = await svc.CreateBoardAsync("en");
        await svc.DeleteAsync(board._id);
        var remaining = await svc.GetBoardsAsync();
        Assert.IsFalse(remaining.Any(b => b._id == board._id));
    }
}