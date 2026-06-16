using System;

using MongoDB.Bson;
using System.Threading.Tasks;

namespace Kanban.Model
{
    public class Activity
    {
        public Repository.Activity ActivityOfRepository { get; set; }

        public Activity()
        {
            ActivityOfRepository = new Repository.Activity();
        }

        public ObjectId BoardID
        {
            get { return ActivityOfRepository.BoardID; }
            set { ActivityOfRepository.BoardID = value; }
        }

        public ObjectId CardID
        {
            get { return ActivityOfRepository.CardID; }
            set { ActivityOfRepository.CardID = value; }
        }

        public WorkState WorkState
        {
            get { return new WorkState(ActivityOfRepository.WorkState); }
            set
            {
                ActivityOfRepository.WorkState = new Repository.WorkState { ProcessStepSeqNo = value.ProcessStepSeqNo, IsWIP = value.IsWIP, IsBlocked = value.IsBlocked, IsMergedIntoMaster = value.IsMergedIntoMaster, IsMergedIntoMajorBranch = value.IsMergedIntoMajorBranch };
            }
        }

        public DateTime StateChangedDate
        {
            get { return ActivityOfRepository.StateChangedDate; }
            set { ActivityOfRepository.StateChangedDate = value; }
        }

        public async Task Save()
        {
            await ActivityRepository.Insert(this);
        }

        public static IActivityRepository ActivityRepository { get; set; }
    }
}
