using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using System.Runtime.CompilerServices;

namespace Kanban.Model
{
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

        public void DeleteFromDB()
        {
            CardRepository.Delete(this);
            Activity.ActivityRepository.DeleteActivitiesOf(this);
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
        {
            get
            {
                switch (StoryPoints)
                {
                    case 0:
                    case 1:
                    case 2:
                        return Repository.EstimatedWorkEffort.Small;
                    case 3:
                    case 5:
                        return Repository.EstimatedWorkEffort.Medium;
                    case 8:
                    case 13:
                        return Repository.EstimatedWorkEffort.Large;
                    case 20:
                    case 40:
                    case 100:
                        return Repository.EstimatedWorkEffort.LargeExtra;
                    default:
                        return Repository.EstimatedWorkEffort.Unknown;
                }
            }
        }

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

        public async void CreateNewActivities(WorkState workState)
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
}
