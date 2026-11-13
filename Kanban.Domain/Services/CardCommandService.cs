using Kanban.Model;
using Kanban.Repository;
using MongoDB.Bson;
using Board = Kanban.Model.Board;
using WorkState = Kanban.Model.WorkState;

namespace Kanban.Services;

public class MoveCardRequest
{
    public int ProcessStepSeqNo { get; set; }
    public bool IsWip { get; set; }
    public int Index { get; set; }
}

/// <summary>PATCH 用 nullable フィールドパッチ(null 以外のみ適用)。</summary>
public class CardPatch
{
    public string? Title { get; set; }
    public string? Tag { get; set; }
    public string? Executors { get; set; }
    public string? Summary { get; set; }
    public string? TestCases { get; set; }
    public string? DesignOverview { get; set; }
    public string? CardType { get; set; }
    public int? StoryPoints { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public bool? IsBlocked { get; set; }
    public bool? IsMergedIntoMaster { get; set; }
    public bool? IsMergedIntoMajorBranch { get; set; }
    public bool? IsScheduledToRelease { get; set; }
}

/// <summary>
/// カード書き込みの不変条件(Activity監査証跡・列内SeqNo 0..N-1)を実装する唯一の場所。
/// 移植元: WPF WIPDoneViewModel.OnCollectionChanged(UpdateCardSeqNo/AddActivityToCard)。
/// 中間Activity合成は既存 Model.Card.CreateActivities4IntermediateProcesses、
/// 同一列判定は既存 Model.WorkState.Equals(ProcessStepSeqNo+IsWIP)を使う。
/// </summary>
public class CardCommandService
{
    private readonly ICardFactory m_CardFactory;
    private readonly ICardRepository m_CardRepository;
    private readonly IActivityRepository m_ActivityRepository;
    private readonly IDBCard m_DBCards;
    private readonly IBoardRepository m_BoardRepository;

    public CardCommandService(
        ICardFactory cardFactory,
        ICardRepository cardRepository,
        IActivityRepository activityRepository,
        IDBCard dbCards,
        IBoardRepository boardRepository)
    {
        m_CardFactory = cardFactory;
        m_CardRepository = cardRepository;
        m_ActivityRepository = activityRepository;
        m_DBCards = dbCards;
        m_BoardRepository = boardRepository;
    }

    /// <summary>
    /// 既存ファクトリで插入後、その列(前面)の現存カード数を末尾 SeqNo として確定する。
    /// 既存 AdjustCardSeqNo が次回読取時に正規化するため、並行作成は一時的に重複しうる(設計上の許容)。
    /// </summary>
    public async Task<Model.Card> CreateCardAsync(ObjectId boardId, int processStepSeqNo, bool isWip)
    {
        Board board = await FindBoardAsync(boardId);
        GuardStep(board, processStepSeqNo);

        WorkState workState = new WorkState { ProcessStepSeqNo = processStepSeqNo, IsWIP = isWip };
        int tailSeqNo = (await m_CardRepository.GetCards(board, workState, "", OnBoardFilter.OnFrontBoard)).Count;

        Model.Card card = await m_CardFactory.CreateCard(boardId, workState, null!);
        await m_CardRepository.Update(card, tailSeqNo, nameof(Model.Card.SeqNo));
        return card;
    }

    public async Task MoveCardAsync(ObjectId cardId, MoveCardRequest req)
    {
        Repository.Card repoCard = await FindRepoCardAsync(cardId);
        Board board = await FindBoardAsync(repoCard.BoardID);
        GuardStep(board, req.ProcessStepSeqNo);

        Model.Card card = new Model.Card(repoCard);
        WorkState current = card.WorkState;
        WorkState target = new WorkState { ProcessStepSeqNo = req.ProcessStepSeqNo, IsWIP = req.IsWip };

        if (current != target)   // Model.WorkState.Equals: ProcessStepSeqNo+IsWIP のみ(既存と同じ)
        {
            // 移動元列から除去して再採番
            var sourceList = await m_CardRepository.GetCards(board, current, "", OnBoardFilter.OnFrontBoard);
            sourceList.RemoveAll(c => c._id == cardId);
            await RenumberAsync(sourceList);

            // 移動先列へ index=clamp(0..Count) で挿入して再採番
            var destList = await m_CardRepository.GetCards(board, target, "", OnBoardFilter.OnFrontBoard);
            destList.Insert(Math.Clamp(req.Index, 0, destList.Count), card);
            await RenumberAsync(destList);

            // 中間Activity合成(既存Model) + WorkState/StateChangedDate を await 経路で確定
            await card.CreateNewActivitiesAsync(target);
            await m_CardRepository.Update(card, repoCard.WorkState, nameof(Model.Card.WorkState));
            await m_CardRepository.Update(card, repoCard.StateChangedDate, nameof(Model.Card.StateChangedDate));

            if (repoCard.IsOnBackBoard)
            {
                // バックボードカードの別列への move = バックボード解除を兼ねる(WPF の front/back トグル off と同じ意味論)。
                // 旗を切替えないと前面読み取り(IsOnBackBoard=false 条件)から消え、背面では列が食い違う
                // 「どちらにも見えないカード」になる(WPFは作れない状態。最終レビュー指摘)。
                card.IsOnBackBoard = false;
                await m_CardRepository.Update(card, false, nameof(Model.Card.IsOnBackBoard));
            }
        }
        else
        {
            // 同一列内 reorder: SeqNo のみ変更・Activity なし(既存WPFと同じ)
            var list = await m_CardRepository.GetCards(board, current, "", OnBoardFilter.OnFrontBoard);
            int from = list.FindIndex(c => c._id == cardId);
            if (from < 0) return;   // その列に居ない(バックボード等)→ 何もしない
            list.RemoveAt(from);
            list.Insert(Math.Clamp(req.Index, 0, list.Count), card);
            await RenumberAsync(list);
        }
    }

    /// <summary>null 以外のフィールドのみ適用。blocked/merged系はSetFlagAsync setter経路(新Activity)。</summary>
    public async Task UpdateFieldsAsync(ObjectId cardId, CardPatch patch)
    {
        Repository.Card repoCard = await FindRepoCardAsync(cardId);
        Model.Card card = new Model.Card(repoCard);

        if (patch.Title is not null) await SetFieldAsync(card, repoCard, patch.Title, nameof(repoCard.Title), repoCard => repoCard.Title = patch.Title);
        if (patch.Tag is not null) await SetFieldAsync(card, repoCard, patch.Tag, nameof(repoCard.Tag), repoCard => repoCard.Tag = patch.Tag);
        if (patch.Executors is not null) await SetFieldAsync(card, repoCard, patch.Executors, nameof(repoCard.Executors), repoCard => repoCard.Executors = patch.Executors);
        if (patch.Summary is not null) await SetFieldAsync(card, repoCard, patch.Summary, nameof(repoCard.Summary), repoCard => repoCard.Summary = patch.Summary);
        if (patch.TestCases is not null) await SetFieldAsync(card, repoCard, patch.TestCases, nameof(repoCard.TestCases), repoCard => repoCard.TestCases = patch.TestCases);
        if (patch.DesignOverview is not null) await SetFieldAsync(card, repoCard, patch.DesignOverview, nameof(repoCard.DesignOverview), repoCard => repoCard.DesignOverview = patch.DesignOverview);
        if (patch.StoryPoints is int storyPoints) await SetFieldAsync(card, repoCard, storyPoints, nameof(repoCard.StoryPoints), repoCard => repoCard.StoryPoints = storyPoints);
        if (patch.ReleaseDate is DateTime releaseDate) await SetFieldAsync(card, repoCard, releaseDate, nameof(repoCard.ReleaseDate), repoCard => repoCard.ReleaseDate = releaseDate);
        if (patch.IsScheduledToRelease is bool scheduled) await SetFieldAsync(card, repoCard, scheduled, nameof(repoCard.IsScheduledToRelease), repoCard => repoCard.IsScheduledToRelease = scheduled);

        if (patch.CardType is not null)
        {
            if (!Enum.TryParse(patch.CardType, ignoreCase: true, out Repository.CardType cardType))
                throw new ArgumentException($"unknown CardType: {patch.CardType}", nameof(patch));
            await SetFieldAsync(card, repoCard, cardType, nameof(repoCard.CardType), repoCard => repoCard.CardType = cardType);
        }

        if (patch.IsBlocked is bool blocked) await card.SetFlagAsync(nameof(Model.Card.IsBlocked), blocked);
        if (patch.IsMergedIntoMaster is bool mergedMaster) await card.SetFlagAsync(nameof(Model.Card.IsMergedIntoMaster), mergedMaster);
        if (patch.IsMergedIntoMajorBranch is bool mergedMajor) await card.SetFlagAsync(nameof(Model.Card.IsMergedIntoMajorBranch), mergedMajor);
    }

    public async Task DeleteCardAsync(ObjectId cardId)
    {
        Repository.Card repoCard = await FindRepoCardAsync(cardId);
        Board board = await FindBoardAsync(repoCard.BoardID);
        WorkState sourceState = new WorkState(repoCard.WorkState);

        await new Model.Card(repoCard).DeleteFromDBAsync();

        // 移動元列のSeqNoを再圧縮(既存 AdjustCardSeqNo 相当を削除直後も明示)
        var remaining = await m_CardRepository.GetCards(board, sourceState, "", OnBoardFilter.OnFrontBoard);
        await RenumberAsync(remaining);
    }

    private async Task SetFieldAsync<T>(Model.Card card, Repository.Card repoCard, T value, string propertyName, Action<Repository.Card> assign)
    {
        assign(repoCard);   // インスタンス側も同期させる(プロパティsetterの fire-and-forget は使わない)
        await m_CardRepository.Update(card, value, propertyName);
    }

    private async Task RenumberAsync(List<Model.Card> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].CardOfRepository.SeqNo != i)
            {
                list[i].CardOfRepository.SeqNo = i;
                await m_CardRepository.Update(list[i], i, nameof(Model.Card.SeqNo));
            }
        }
    }

    private static void GuardStep(Board board, int step)
    {
        if (step < 0 || !board.DevProcess.ProcessSteps.Any(s => s.PhaseSeqNo == step))
            throw new ArgumentOutOfRangeException(nameof(step), $"process step {step} does not exist on board {board._id}");
    }

    private async Task<Repository.Card> FindRepoCardAsync(ObjectId cardId) =>
        await m_DBCards.GetCardById(cardId) ?? throw new KeyNotFoundException($"card {cardId} not found");

    private async Task<Board> FindBoardAsync(ObjectId boardId)
    {
        List<Board> boards = await m_BoardRepository.LoadBoards();
        return boards.FirstOrDefault(b => b._id == boardId)
            ?? throw new KeyNotFoundException($"board {boardId} not found");
    }
}
