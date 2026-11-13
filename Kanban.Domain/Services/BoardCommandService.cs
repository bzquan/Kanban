using Kanban.Model;
using Kanban.Repository;
using Kanban.Util;
using MongoDB.Bson;
using MongoDB.Driver;
using static Kanban.Util.KanbanDefinitions;
using Board = Kanban.Model.Board;

namespace Kanban.Services;

public enum GuardReason
{
    TooFewSteps,
    StepInUse,
}

public class ProcessStepUpdateResult
{
    public bool Success { get; init; }
    public UpdateResult? Update { get; init; }
    public GuardReason? Guard { get; init; }
}

/// <summary>
/// ボード作成/削除/プロセスステップ編集(列編集は既存 Model.Board / DevProcess / BoardRepository のプリミティブのみを使う)。
/// </summary>
public class BoardCommandService
{
    private readonly IBoardFactory m_BoardFactory;
    private readonly IBoardRepository m_BoardRepository;
    private readonly IDBBoard m_DBBoard;
    private readonly ICardRepository m_CardRepository;
    private readonly ILocalizationFactory m_LocalizationFactory;

    public BoardCommandService(
        IBoardFactory boardFactory,
        IBoardRepository boardRepository,
        IDBBoard dbBoard,
        ICardRepository cardRepository,
        ILocalizationFactory localizationFactory)
    {
        m_BoardFactory = boardFactory;
        m_BoardRepository = boardRepository;
        m_DBBoard = dbBoard;
        m_CardRepository = cardRepository;
        m_LocalizationFactory = localizationFactory;
    }

    public async Task<List<Board>> GetBoardsAsync()
    {
        List<Board> boards = await m_BoardRepository.LoadBoards();
        return boards.OrderBy(b => b.SeqNo).ToList();
    }

    /// <summary>
    /// 既定5列を uiLanguage でローカライズしてボードを作成する。
    /// 列の並びは WPF の DevProcess() 既定コンストラクタと同じ(初列のみ WIPTitle=Preparing、
    /// 1列目が IsStartOfCycleTime、3列目が IsEndOfCycleTime)。静的 Localization を変更しないよう
    /// ステップは明示構築し DevProcess(List) を使う。
    /// </summary>
    public async Task<Board> CreateBoardAsync(string uiLanguage)
    {
        ILocalization localization = m_LocalizationFactory.Create(ParseLanguage(uiLanguage));
        string wipTitle = localization.ProcessWIPTitle;
        string doneTitle = localization.ProcessDoneTitle;

        var processSteps = new List<Repository.ProcessStep>
        {
            new() { PhaseSeqNo = 0, LabelColor = LabelColorNames[0], Name = localization.ProcessBackLogTitle, WIPTitle = localization.ProcessPreparingTitle, DoneTitle = doneTitle },
            new() { PhaseSeqNo = 1, LabelColor = LabelColorNames[1], Name = localization.ProcessRequirementTitle, WIPTitle = wipTitle, DoneTitle = doneTitle, IsStartOfCycleTime = true },
            new() { PhaseSeqNo = 2, LabelColor = LabelColorNames[2], Name = localization.ProcessDevelopmentTitle, WIPTitle = wipTitle, DoneTitle = doneTitle },
            new() { PhaseSeqNo = 3, LabelColor = LabelColorNames[3], Name = localization.ProcessTestTitle, WIPTitle = wipTitle, DoneTitle = doneTitle, IsEndOfCycleTime = true },
            new() { PhaseSeqNo = 4, LabelColor = LabelColorNames[4], Name = localization.ProcessReleaseTitle, WIPTitle = wipTitle, DoneTitle = doneTitle },
        };

        var board = new Board(localization.BoardDefaultTitle, localization.BoardDefaultDescription, new DevProcess(processSteps));
        await m_DBBoard.Insert(board.BoardOfRepository);
        return board;
    }

    public async Task DeleteAsync(ObjectId boardId)
    {
        Board board = await FindBoardAsync(boardId);
        await m_BoardRepository.Delete(board);
    }

    /// <summary>
    /// 列の再採番(並べ替え)でカード/Activities を一時的に退避する作業列番号。
    /// 既存 WPF ProcessViewModel.WORK_SEQ_NO と同じ意味論(列数ガードで 100 未満を保証)。
    /// </summary>
    private const int WorkSeqNo4Reorder = 100;

    /// <summary>
    /// プロセスステップの一括編集(追加/削除/リネーム/色/サイクル列フラグ/並べ替え 仕様書§4.3)。
    /// クライアントは GET 時点の PhaseSeqNo をそのまま身分別子として送り、リスト順 = 新しい表示順で送る。
    ///  ・PhaseSeqNo が消えている → 列削除(ガード:3件未満/カード残存 → 409)
    ///  ・PhaseSeqNo >= 元個数   → 列追加(挿入位置=リスト中の位置)
    ///  ・身分別子の入れ替え(単調増加でない)→ 並べ替え:カード/Activities を既存
    ///    Board.ChangeCardsAndActivitySeqNo(サイクル/連鎖分解・退避=WorkSeqNo4Reorder)で列追従させる
    ///    (最終レビュー指摘: 400 拒否は仕様ギャップだった)。
    /// 削除は既存 Board.DeleteProcessStep(Activities削除+後続列カードの-1シフト)を降順に適用する。
    /// </summary>
    public async Task<ProcessStepUpdateResult> UpdateProcessStepsAsync(ObjectId boardId, List<Repository.ProcessStep> newSteps)
    {
        Board board = await FindBoardAsync(boardId);

        // --- 検証(全変更前に完了させ、失敗時は DB を触らない) ---
        if (newSteps.Count < 3)
            return new ProcessStepUpdateResult { Success = false, Guard = GuardReason.TooFewSteps };

        if (newSteps.Count >= WorkSeqNo4Reorder)
            throw new ArgumentException($"at most {WorkSeqNo4Reorder - 1} process steps (reorder parking slot)", nameof(newSteps));

        if (newSteps.Count(s => s.IsStartOfCycleTime) > 1 || newSteps.Count(s => s.IsEndOfCycleTime) > 1)
            throw new ArgumentException("at most one start and one end of cycle time are allowed", nameof(newSteps));

        var oldSteps = board.DevProcess.ProcessSteps.OrderBy(s => s.PhaseSeqNo).ToList();
        int oldCount = oldSteps.Count;

        // 身分別子=元PhaseSeqNo(0..oldCount-1)。生存エントリーの重複は同一列を2箇所に置く試みなので 400。
        var retained = newSteps.Select((s, pos) => (step: s, pos)).Where(x => x.step.PhaseSeqNo < oldCount).ToList();
        var oldIds = oldSteps.Select(s => s.PhaseSeqNo).ToHashSet();
        if (retained.GroupBy(x => x.step.PhaseSeqNo).Any(g => g.Count() > 1))
            throw new ArgumentException("duplicate PhaseSeqNo in process steps", nameof(newSteps));
        if (retained.Any(x => !oldIds.Contains(x.step.PhaseSeqNo)))
            throw new ArgumentException("unknown PhaseSeqNo (column identity not found)", nameof(newSteps));

        var keptIds = retained.Select(x => x.step.PhaseSeqNo).ToHashSet();
        var stepsToDelete = oldSteps.Where(s => !keptIds.Contains(s.PhaseSeqNo)).ToList();
        if (stepsToDelete.Any(s => m_CardRepository.ExistCards(board, s)))
            return new ProcessStepUpdateResult { Success = false, Guard = GuardReason.StepInUse };

        // --- 削除(カードが残っていない列のみ。DeleteProcessStep が Activities 削除+後続列カードの-1シフト+保存を担う) ---
        foreach (var step in stepsToDelete.OrderByDescending(s => s.PhaseSeqNo))
        {
            await board.DeleteProcessStep(step);
        }

        // --- 並べ替え/削除詰め: カードと Activities の列番号だけを既存プリミティブで移動する。
        //     削除済み列は空(ガード済み)、追加列は空なので、変化マップは「空の着地点」で終端する連鎖/サイクルに分解できる ---
        var finalPos = retained.ToDictionary(x => x.step.PhaseSeqNo, x => x.pos);
        var changed = new Dictionary<int, int>();
        foreach (int identity in keptIds)
        {
            int current = identity - stepsToDelete.Count(d => d.PhaseSeqNo < identity); // DeleteProcessStep の-1シフト後位置
            int target = finalPos[identity];
            if (current != target) changed[current] = target;
        }
        ReorderColumnSeqNos(board, changed);

        // --- リネーム/色/フラグ適用と追加列の作成(身分別子=元PhaseSeqNo で生存列を特定。ProcessStep はまだ元番号を持つ) ---
        var stepByIdentity = board.DevProcess.ProcessSteps.ToDictionary(s => s.PhaseSeqNo);
        var stepByPos = new Dictionary<int, Repository.ProcessStep>();
        foreach (var (desired, pos) in newSteps.Select((s, i) => (s, i)))
        {
            if (desired.PhaseSeqNo < oldCount)
            {
                var targetStep = stepByIdentity[desired.PhaseSeqNo];
                ApplyEdits(targetStep, desired);
                stepByPos[pos] = targetStep;
            }
            else
            {
                // 追加列の名前は DevProcess.AddProcessStep() の静的既定=EN、続けて ApplyEdits で確定する
                var added = board.DevProcess.AddProcessStep();
                ApplyEdits(added, desired);
                stepByPos[pos] = added;
            }
        }

        // --- 表示順で 0..N-1 へ採番し直して保存(カード列は上の移動で既に一致済み) ---
        foreach (var (pos, step) in stepByPos)
        {
            step.PhaseSeqNo = pos;
        }
        board.DevProcess.SortProcessSteps();
        board.AdjustProcessCycleTimeSteps();
        UpdateResult update = board.SaveProcessStep();
        return new ProcessStepUpdateResult { Success = true, Update = update };
    }

    /// <summary>
    /// 列番号の単射写像(現在位置→目標位置)を既存 ChangeCardsAndActivitySeqNo のみで実現する。
    /// サイクルは退避(WorkSeqNo4Reorder)を経由して逆向きに流し、連鎖は空の着地点(削除済み列/追加列位置)から逆向きに流す。
    /// 着地点が必ず空であることは呼び出し側の不変条件(削除列はカードなしガード済み・追加列は常に空)から従う。
    /// </summary>
    private static void ReorderColumnSeqNos(Board board, Dictionary<int, int> changed)
    {
        var remaining = new Dictionary<int, int>(changed);
        while (remaining.Count > 0)
        {
            int start = remaining.Keys.First();
            var chain = new List<int> { start };
            var visited = new HashSet<int> { start };
            bool isCycle = false;
            int cur = start;
            while (remaining.TryGetValue(cur, out int nxt))
            {
                if (!visited.Add(nxt)) { isCycle = true; break; }
                chain.Add(nxt);
                cur = nxt;
            }

            if (isCycle)
            {
                board.ChangeCardsAndActivitySeqNo(chain[0], WorkSeqNo4Reorder);            // 退避
                for (int i = chain.Count - 1; i >= 1; i--)
                {
                    board.ChangeCardsAndActivitySeqNo(chain[i], remaining[chain[i]]);
                }
                board.ChangeCardsAndActivitySeqNo(WorkSeqNo4Reorder, remaining[chain[0]]); // 復旧
            }
            else
            {
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    board.ChangeCardsAndActivitySeqNo(chain[i], remaining[chain[i]]);
                }
            }

            foreach (int key in chain)
            {
                remaining.Remove(key);
            }
        }
    }

    private static void ApplyEdits(Repository.ProcessStep step, Repository.ProcessStep desired)
    {
        step.Name = desired.Name ?? step.Name;
        step.WIPTitle = desired.WIPTitle ?? step.WIPTitle;
        step.DoneTitle = desired.DoneTitle ?? step.DoneTitle;
        step.LabelColor = desired.LabelColor ?? step.LabelColor;
        step.IsStartOfCycleTime = desired.IsStartOfCycleTime;
        step.IsEndOfCycleTime = desired.IsEndOfCycleTime;
    }

    private async Task<Board> FindBoardAsync(ObjectId boardId)
    {
        List<Board> boards = await m_BoardRepository.LoadBoards();
        return boards.FirstOrDefault(b => b._id == boardId)
            ?? throw new KeyNotFoundException($"board {boardId} not found");
    }

    public static Languages ParseLanguage(string uiLanguage) => uiLanguage?.ToLowerInvariant() switch
    {
        "ja" => Languages.Japanese,
        "zh" => Languages.Chinese,
        "en" => Languages.English,
        _ => throw new ArgumentException($"unsupported ui language: {uiLanguage} (expected ja|zh|en)", nameof(uiLanguage)),
    };
}
