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
    /// プロセスステップの一括編集(追加/削除/リネーム/色/サイクル列フラグ)。
    /// クライアントは GET 時点の PhaseSeqNo をそのまま身分別子として送る(リネームで PhaseSeqNo は動かさない)。
    ///  ・PhaseSeqNo が消えている → 列削除(ガード:3件未満/カード残存 → 409)
    ///  ・PhaseSeqNo >= 元個数   → 列追加
    ///  ・PhaseSeqNo が生存列と一致 → リネーム/色/フラグ適用
    ///  ・生存列の PhaseSeqNo を入れ替える試み(単調増加でない)は/cards列帰属が壊れるため 400
    /// 削除は既存 Board.DeleteProcessStep(Activities削除+カード下方移動)を降順に適用し、最後に 0..N-1 へ再採番して保存する。
    /// </summary>
    public async Task<ProcessStepUpdateResult> UpdateProcessStepsAsync(ObjectId boardId, List<Repository.ProcessStep> newSteps)
    {
        Board board = await FindBoardAsync(boardId);

        // --- 検証(全変更前に完了させ、失敗時は DB を触らない) ---
        if (newSteps.Count < 3)
            return new ProcessStepUpdateResult { Success = false, Guard = GuardReason.TooFewSteps };

        if (newSteps.Zip(newSteps.Skip(1)).Any(pair => pair.First.PhaseSeqNo >= pair.Second.PhaseSeqNo))
            throw new ArgumentException("process steps must be strictly increasing in PhaseSeqNo (column reorder is not supported in this version)", nameof(newSteps));

        if (newSteps.Count(s => s.IsStartOfCycleTime) > 1 || newSteps.Count(s => s.IsEndOfCycleTime) > 1)
            throw new ArgumentException("at most one start and one end of cycle time are allowed", nameof(newSteps));

        var oldSteps = board.DevProcess.ProcessSteps.OrderBy(s => s.PhaseSeqNo).ToList();
        int oldCount = oldSteps.Count;
        var keptSeqNos = newSteps.Where(s => s.PhaseSeqNo < oldCount).Select(s => s.PhaseSeqNo).ToHashSet();

        var stepsToDelete = oldSteps.Where(s => !keptSeqNos.Contains(s.PhaseSeqNo)).ToList();
        if (stepsToDelete.Any(s => m_CardRepository.ExistCards(board, s)))
            return new ProcessStepUpdateResult { Success = false, Guard = GuardReason.StepInUse };

        // --- 削除(カードが残っていない列のみ。DeleteProcessStep が Activities 削除+カード移動+保存を担う) ---
        foreach (var step in stepsToDelete.OrderByDescending(s => s.PhaseSeqNo))
        {
            await board.DeleteProcessStep(step);
        }

        // --- 生存列への適用(身分別子=元PhaseSeqNoで一致) ---
        var newBySeqNo = newSteps.Where(s => s.PhaseSeqNo < oldCount).ToDictionary(s => s.PhaseSeqNo);
        foreach (var step in board.DevProcess.ProcessSteps)
        {
            if (newBySeqNo.TryGetValue(step.PhaseSeqNo, out var desired))
            {
                ApplyEdits(step, desired);
            }
        }

        // --- 追加(seqNo >= 元個数 のエントリー。追加列の名前は DevProcess.AddProcessStep() の静的既定=EN、
        //     続けて同一 PUT のこのエントリーで ApplyEdits 確定する) ---
        foreach (var desired in newSteps.Where(s => s.PhaseSeqNo >= oldCount))
        {
            var step = board.DevProcess.AddProcessStep();
            ApplyEdits(step, desired);
        }

        // --- 0..N-1 へ再採番(削除で空いた穴を詰める。カードは DeleteProcessStep で既に移動済み。
        //     リスト順 = 生存列の元順 + 追加列順) ---
        var finalSteps = board.DevProcess.ProcessSteps.ToList();
        for (int i = 0; i < finalSteps.Count; i++)
        {
            finalSteps[i].PhaseSeqNo = i;
        }

        board.AdjustProcessCycleTimeSteps();
        UpdateResult update = board.SaveProcessStep();
        return new ProcessStepUpdateResult { Success = true, Update = update };
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
