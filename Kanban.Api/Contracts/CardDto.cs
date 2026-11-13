namespace Kanban.Api.Contracts;

public class WorkStateDto
{
    public int ProcessStepSeqNo { get; set; }
    public bool IsWIP { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsMergedIntoMaster { get; set; }
    public bool IsMergedIntoMajorBranch { get; set; }
}

/// <summary>カード読み取り/書き込み共通DTO(Task 5以降も共通使用)。</summary>
public class CardDto
{
    public string Id { get; set; } = "";
    public int SeqNo { get; set; }
    public string Title { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Executors { get; set; } = "";
    public string Summary { get; set; } = "";
    public string TestCases { get; set; } = "";
    public string DesignOverview { get; set; } = "";
    public string CardType { get; set; } = "";
    public int StoryPoints { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime StateChangedDate { get; set; }
    public DateTime ReleaseDate { get; set; }
    public bool IsOnBackBoard { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsMergedIntoMaster { get; set; }
    public bool IsMergedIntoMajorBranch { get; set; }
    public bool IsScheduledToRelease { get; set; }
    public WorkStateDto WorkState { get; set; } = new();
}

public static class CardDtoMapper
{
    public static CardDto From(Kanban.Model.Card card) => new()
    {
        Id = card._id.ToString(),
        SeqNo = card.SeqNo,
        Title = card.Title,
        Tag = card.Tag,
        Executors = card.Executors,
        Summary = card.Summary,
        TestCases = card.TestCases,
        DesignOverview = card.DesignOverview,
        CardType = card.CardType.ToString(),
        StoryPoints = card.StoryPoints,
        CreateDate = card.CreateDate,
        StateChangedDate = card.StateChangedDate,
        ReleaseDate = card.ReleaseDate,
        IsOnBackBoard = card.IsOnBackBoard,
        IsBlocked = card.IsBlocked,
        IsMergedIntoMaster = card.IsMergedIntoMaster,
        IsMergedIntoMajorBranch = card.IsMergedIntoMajorBranch,
        IsScheduledToRelease = card.IsScheduledToRelease,
        WorkState = new WorkStateDto
        {
            ProcessStepSeqNo = card.WorkState.ProcessStepSeqNo,
            IsWIP = card.WorkState.IsWIP,
            IsBlocked = card.WorkState.IsBlocked,
            IsMergedIntoMaster = card.WorkState.IsMergedIntoMaster,
            IsMergedIntoMajorBranch = card.WorkState.IsMergedIntoMajorBranch,
        },
    };
}
