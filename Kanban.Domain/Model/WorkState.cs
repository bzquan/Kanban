using System;
using System.Text;

namespace Kanban.Model
{
    public class WorkState
    {
        public int ProcessStepSeqNo { get; set; } = 0;
        public bool IsWIP { get; set; } = true;
        public bool IsBlocked { get; set; } = false;
        public bool IsMergedIntoMaster { get; set; } = false;
        public bool IsMergedIntoMajorBranch { get; set; } = false;

        public WorkState() { }

        public WorkState(Repository.WorkState workState)
        {
            ProcessStepSeqNo = workState.ProcessStepSeqNo;
            IsWIP = workState.IsWIP;
            IsBlocked = workState.IsBlocked;
            IsMergedIntoMaster = workState.IsMergedIntoMaster;
            IsMergedIntoMajorBranch = workState.IsMergedIntoMajorBranch;
        }

        public WorkState CreateWithBlocked(bool isBlocked)
        {
            WorkState newWorkState = (WorkState)MemberwiseClone();
            newWorkState.IsBlocked = isBlocked;
            return newWorkState;
        }

        public WorkState CreateWithMergedIntoMaster(bool isMergedIntoMaster)
        {
            WorkState newWorkState = (WorkState)MemberwiseClone();
            newWorkState.IsMergedIntoMaster = isMergedIntoMaster;
            return newWorkState;
        }

        public WorkState CreateWithMergedIntoMajorBranch(bool isMergedIntoMajorBranch)
        {
            WorkState newWorkState = (WorkState)MemberwiseClone();
            newWorkState.IsMergedIntoMajorBranch = isMergedIntoMajorBranch;
            return newWorkState;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder
                .Append("ProcessStepSeqNo: " + ProcessStepSeqNo)
                .Append(", ")
                .Append("IsWIP: " + IsWIP);

            return builder.ToString();
        }

        public static bool operator == (WorkState workState1, WorkState workState2)
        {
            return ReferenceEquals(workState1, workState2) ||
                  (!ReferenceEquals(workState1, null) && workState1.Equals(workState2));
        }

        public static bool operator !=(WorkState workState1, WorkState workState2)
        {
            return !(workState1 == workState2);
        }

        public bool Equals(WorkState other)
        {
            return !ReferenceEquals(other, null) &&
              ProcessStepSeqNo == other.ProcessStepSeqNo &&
              IsWIP == other.IsWIP;
        }
        public override bool Equals(object obj) => Equals(obj as WorkState);
        public override int GetHashCode()
        {
            return this.ProcessStepSeqNo;
        }
    }
}
