using MongoDB.Bson;
using System.Runtime.CompilerServices;

namespace Kanban.Model;

public class Card : KanbanItem
{
    public static List<int> s_ValidStoryPoints = new List<int> { 0, 1, 2, 3, 5, 8, 13, 20, 40, 100, Repository.Card.UNKNOWN_STORY_POINTS };

    public Repository.Card CardOfRepository { get; set; }

    public Card(ObjectId boardID, WorkState workState, string title) :
        this(new Repository.Card { Title = title, BoardID = boardID, WorkState = new Repository.WorkState { ProcessStepSeqNo = workState.ProcessStepSeqNo, IsWIP = workState.IsWIP } })
    {
    }

    public Card(Repository.Card card) : base(card)
    {
        CardOfRepository = card;
    }

    public ObjectId BoardID
    {
        get => CardOfRepository.BoardID;
        set => CardOfRepository.BoardID = value;
    }

    public Board Board { get; set; }

    public void ReplicateFrom(Card srcCard)
    {
        if (srcCard is null) return;

        Title = srcCard.Title;
        Tag = srcCard.Tag;
        Executors = srcCard.Executors;
        Summary = srcCard.Summary;
        TestCases = srcCard.TestCases;
        DesignOverview = srcCard.DesignOverview;
        CardType = srcCard.CardType;
    }

    public void DeleteFromDB()
    {
        CardRepository.Delete(this);
        Activity.ActivityRepository.DeleteActivitiesOf(this);
    }

    /// <summary>DeleteFromDB の await 可能版(既存同期版はWPF互換のため温存)。</summary>
    public async Task DeleteFromDBAsync()
    {
        await CardRepository.Delete(this);   // DBCards.Delete が Activities も削除する(既存)
        await Activity.ActivityRepository.DeleteActivitiesOf(this);
    }

    /// <summary>
    /// IsBlocked / IsMergedIntoMaster / IsMergedIntoMajorBranch を、既存プロパティsetterと
    /// 同じ CreateWith* + 同一ステップ新Activity 意味論で await 可能にしたもの(setter自体は温存)。
    /// </summary>
    public async Task SetFlagAsync(string flagName, bool value)
    {
        switch (flagName)
        {
            case nameof(IsBlocked):
                if (IsBlocked == value) return;
                CardOfRepository.IsBlocked = value;
                break;
            case nameof(IsMergedIntoMaster):
                if (IsMergedIntoMaster == value) return;
                CardOfRepository.IsMergedIntoMaster = value;
                break;
            case nameof(IsMergedIntoMajorBranch):
                if (IsMergedIntoMajorBranch == value) return;
                CardOfRepository.IsMergedIntoMajorBranch = value;
                break;
            default:
                throw new ArgumentException($"unsupported flag: {flagName}", nameof(flagName));
        }

        WorkState newWorkState = new WorkState
        {
            ProcessStepSeqNo = WorkState.ProcessStepSeqNo,
            IsWIP = WorkState.IsWIP,
            IsBlocked = CardOfRepository.IsBlocked,
            IsMergedIntoMaster = CardOfRepository.IsMergedIntoMaster,
            IsMergedIntoMajorBranch = CardOfRepository.IsMergedIntoMajorBranch,
        };
        await CreateNewActivitiesAsync(newWorkState);   // 同一ステップのsnapshot Activity(setterの既存挙動)

        // setterがやっていた fire-and-forget 書き込みを await 経路で決定論的に確定する
        await CardRepository.Update(this, value, flagName);
        await CardRepository.Update(this, CardOfRepository.WorkState, nameof(WorkState));
        await CardRepository.Update(this, CardOfRepository.StateChangedDate, nameof(StateChangedDate));
    }

    public string Tag
    {
        get => CardOfRepository.Tag;
        set
        {
            CardOfRepository.Tag = value;
            UpdateProperty(value);
        }
    }

    public string Executors
    {
        get => CardOfRepository.Executors;
        set
        {
            CardOfRepository.Executors = value;
            UpdateProperty(value);
        }
    }

    public string Summary
    {
        get => CardOfRepository.Summary;
        set
        {
            CardOfRepository.Summary = value;
            UpdateProperty(value);
        }
    }

    public string TestCases
    {
        get => CardOfRepository.TestCases;
        set
        {
            CardOfRepository.TestCases = value;
            UpdateProperty(value);
        }
    }

    public string DesignOverview
    {
        get => CardOfRepository.DesignOverview;
        set
        {
            CardOfRepository.DesignOverview = value;
            UpdateProperty(value);
        }
    }

    public Repository.CardType CardType
    {
        get => CardOfRepository.CardType;
        set
        {
            CardOfRepository.CardType = value;
            UpdateProperty(value);
        }
    }

    public DateTime ReleaseDate
    {
        get => CardOfRepository.ReleaseDate;
        set
        {
            CardOfRepository.ReleaseDate = value;
            UpdateProperty(value);
        }
    }

    public Repository.EstimatedWorkEffort EstimatedWorkEffort
        => StoryPoints switch
        {
            0 or 1 or 2 => Repository.EstimatedWorkEffort.Small,
            3 or 5 => Repository.EstimatedWorkEffort.Medium,
            8 or 13 => Repository.EstimatedWorkEffort.Large,
            20 or 40 or 100 => Repository.EstimatedWorkEffort.LargeExtra,
            _ => Repository.EstimatedWorkEffort.Unknown,
        };

    public int StoryPoints
    {
        get => CardOfRepository.StoryPoints;
        set
        {
            CardOfRepository.StoryPoints = value;
            UpdateProperty(value);
        }
    }

    public WorkState WorkState
    {
        get => new WorkState(CardOfRepository.WorkState);
        set
        {
            CardOfRepository.WorkState = new Repository.WorkState { ProcessStepSeqNo = value.ProcessStepSeqNo, IsWIP = value.IsWIP, IsBlocked = value.IsBlocked, IsMergedIntoMaster = value.IsMergedIntoMaster, IsMergedIntoMajorBranch = value.IsMergedIntoMajorBranch };
            UpdateProperty(value);
        }
    }

    public DateTime CreateDate
    {
        get => CardOfRepository.CreateDate;
        set
        {
            CardOfRepository.CreateDate = value;
            UpdateProperty(value);
        }
    }

    public DateTime StateChangedDate
    {
        get => CardOfRepository.StateChangedDate;
        set
        {
            CardOfRepository.StateChangedDate = value;
            UpdateProperty(value);
        }
    }

    public bool IsOnBackBoard
    {
        get => CardOfRepository.IsOnBackBoard;
        set
        {
            UpdateProperty(value);
            if (value)
            {
                UpdateOnBackBoardDate();
            }
        }
    }

    public bool IsBlocked
    {
        get => CardOfRepository.IsBlocked;
        set
        {
            if (IsBlocked != value)
            {
                WorkState = WorkState.CreateWithBlocked(value);
                CardOfRepository.IsBlocked = value;
                UpdateProperty(value);
            }
        }
    }

    public bool IsMergedIntoMaster
    {
        get => CardOfRepository.IsMergedIntoMaster;
        set
        {
            if (IsMergedIntoMaster != value)
            {
                WorkState = WorkState.CreateWithMergedIntoMaster(value);
                CardOfRepository.IsMergedIntoMaster = value;
                UpdateProperty(value);
            }
        }
    }

    public bool IsMergedIntoMajorBranch
    {
        get => CardOfRepository.IsMergedIntoMajorBranch;
        set
        {
            if (IsMergedIntoMajorBranch != value)
            {
                WorkState = WorkState.CreateWithMergedIntoMajorBranch(value);
                CardOfRepository.IsMergedIntoMajorBranch = value;
                UpdateProperty(value);
            }
        }
    }

    public bool IsScheduledToRelease
    {
        get => CardOfRepository.IsScheduledToRelease;
        set
        {
            if (IsScheduledToRelease != value)
            {
                CardOfRepository.IsScheduledToRelease = value;
                UpdateProperty(value);
            }
        }
    }

    private void UpdateOnBackBoardDate()
    {
        CardOfRepository.OnBackBoardDate = DateTime.Now;
        Update(CardOfRepository.OnBackBoardDate, nameof(CardOfRepository.OnBackBoardDate));
    }

    public DateTime WorkingChangeDateTime { get; set; } = DateTime.Now; // Activity時系列の制御用

    public void CreateNewActivities(WorkState workState)
    {
        // WPF互換: 元からのUIスレッド fire-and-forget  semantics を維持する。
        // 同期待ち(GetAwaiter().GetResult())はWPFのSynchronizationContextとデッドロックする
        // 恐れがあるため避ける(計画Task 6 Note の執行者判断・台帳 Ruling 参照)。
        // API側は必ず CreateNewActivitiesAsync を await する。
        _ = CreateNewActivitiesAsync(workState);
    }

    public async Task CreateNewActivitiesAsync(WorkState workState)
    {
        WorkingChangeDateTime = DateTime.Now;
        WorkState newWorkState = new WorkState
        {
            ProcessStepSeqNo = workState.ProcessStepSeqNo,
            IsWIP = workState.IsWIP,
            IsBlocked = workState.IsBlocked,
            IsMergedIntoMaster = workState.IsMergedIntoMaster,
            IsMergedIntoMajorBranch = workState.IsMergedIntoMajorBranch
        };
        if (this.WorkState.ProcessStepSeqNo >= workState.ProcessStepSeqNo)
        {
            newWorkState = await CreateNewActivity(workState.ProcessStepSeqNo, workState.IsWIP, newWorkState.IsBlocked, newWorkState.IsMergedIntoMaster, newWorkState.IsMergedIntoMajorBranch);
        }
        else
        {
            if (this.WorkState.IsWIP)
            {
                newWorkState = await CreateNewActivity(this.WorkState.ProcessStepSeqNo, isWIP: false, isBlocked: newWorkState.IsBlocked, isMergedIntoMaster: IsMergedIntoMaster, isMergedIntoMajorBranch: IsMergedIntoMajorBranch);
            }
            newWorkState = await CreateActivities4IntermediateProcesses(workState) ?? newWorkState;
        }

        this.WorkState = newWorkState;
        this.StateChangedDate = WorkingChangeDateTime.AddMilliseconds(5);
    }

    private async Task<WorkState> CreateNewActivity(int seqNo, bool isWIP, bool isBlocked, bool isMergedIntoMaster, bool isMergedIntoMajorBranch)
    {
        WorkState newWorkState = new WorkState
        {
            ProcessStepSeqNo = seqNo,
            IsWIP = isWIP,
            IsBlocked = isBlocked,
            IsMergedIntoMaster = isMergedIntoMaster,
            IsMergedIntoMajorBranch = isMergedIntoMajorBranch
        };
        WorkingChangeDateTime = WorkingChangeDateTime.AddMilliseconds(5);
        Activity activity_Done_temp = new Activity { BoardID = BoardID, CardID = _id, WorkState = newWorkState, StateChangedDate = WorkingChangeDateTime };
        await activity_Done_temp.Save();
        return newWorkState;
    }

    private async Task<WorkState> CreateActivities4IntermediateProcesses(WorkState workState)
    {
        WorkState newWorkState = null;
        for (int seqNo = this.WorkState.ProcessStepSeqNo + 1; seqNo <= workState.ProcessStepSeqNo; seqNo++)
        {
            newWorkState = await CreateNewActivity(seqNo, isWIP: true, isBlocked: IsBlocked, isMergedIntoMaster: IsMergedIntoMaster, isMergedIntoMajorBranch: IsMergedIntoMajorBranch);
            if ((seqNo < workState.ProcessStepSeqNo) || !workState.IsWIP)
            {
                newWorkState = await CreateNewActivity(seqNo, isWIP: false, isBlocked: IsBlocked, isMergedIntoMaster: IsMergedIntoMaster, isMergedIntoMajorBranch: IsMergedIntoMajorBranch);
            }
        }

        return newWorkState;
    }

    public static ICardRepository CardRepository { get; set; }

    protected override async void Update<T>(T propertyValue, [CallerMemberName] string propertyName = "")
    {
        await CardRepository.Update<T>(this, propertyValue, propertyName);
    }
}
