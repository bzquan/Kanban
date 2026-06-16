using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    public interface IViewModelProperties
    {
        string Metrics_ItemsLabel { get; }
        string Metrics_StoryPointsLabel { get; }
        string Metrics_Last6WeeksAveVelocity { get; }
        string Metrics_DateLabel { get; }
        string Metrics_DistributionLabel { get; }
        string Metrics_AveWIP { get; }
        string Metrics_DoneCumulative { get; }
        string Metrics_FormatLabelDate { get; }
        string Metrics_MaxWIP { get; }
        string Metrics_OccuredDate { get; }
        string Metrics_TotalDate { get; }
        string Metrics_DurationOfMetrics { get; }
        string Metrics_AveThroughput { get; }
        string Metrics_DoneItems { get; }
        string Metrics_EstimatedCycleTime { get; }
        string Metrics_EstimationMuch { get; }
        string Metrics_EstimationProper { get; }
        string Metrics_EstimationSmall { get; }
        string Metrics_PerWeek { get; }
        string Metrics_UnknownWeeks { get; }
        string Metrics_Weeks { get; }
        string Metrics_AveCycleTime { get; }
        string Metrics_CycleTimeLabel { get; }
        string Metrics_DaysPerItem { get; }
        string Metrics_DistributionLabel1 { get; }
        string Metrics_FormatLabelDay { get; }
        string Metrics_ItemsOfBugfix { get; }
        string Metrics_ItemsOfBlocked { get; }

        string ToolTip_BoardBack { get; }
        string ToolTip_BoardFront { get; }
        string ToolTip_MoveToBack { get; }
        string ToolTip_MoveToFront { get; }
        string ToolTip_StoryPoints(Repository.EstimatedWorkEffort estimatedWorkEffort);

        string EditCard_StateOfHistory { get; }
        string EditCard_StateChangedOfHistory { get; }
        string EditCard_StateOfHistoryBlockedOrMerged { get; }
        string EditCard_Blocked { get; }
        string EditCard_MergedIntoMaster { get; }
        string EditCard_MergedIntoMajorBranch { get; }
        string EditCard_MergedIntoMasterAndMajorBranch { get; }
        string EditCard_WorkItem { get; }
        string EditCard_Description { get; }
        string EditCard_TestCases { get; }
        string EditCard_DesignOverview { get; }

        string DateTime_LongFormatter { get; }
        string DateTime_MonthDayFormatter { get; }

        string Message_DeleteComfirmQuesion { get; }
        string Message_DeleteComfirmTitle { get; }
        string Message_DeleteErrorTitle { get; }
        string Message_DeleteProcessStepComfirm { get; }
        string Message_DeleteProcessStepError { get; }
        string Message_HasUnknownStoryPointsInReleasedItem { get; }
    }
}
