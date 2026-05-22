using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kanban.Properties;

namespace Kanban
{
    public class ViewModelProperties : ViewModel.IViewModelProperties
    {
        public string Metrics_ItemsLabel => Resources.Metrics_ItemsLabel;
        public string Metrics_StoryPointsLabel => Resources.Metrics_StoryPointsLabel;
        public string Metrics_Last6WeeksAveVelocity => Resources.Metrics_Last6WeeksAveVelocity;
        public string Metrics_DateLabel => Resources.Metrics_DateLabel;
        public string Metrics_DistributionLabel => Resources.Metrics_DistributionLabel;
        public string Metrics_AveWIP => Resources.Metrics_AveWIP;
        public string Metrics_DoneCumulative => Resources.Metrics_DoneCumulative;
        public string Metrics_FormatLabelDate => Resources.Metrics_FormatLabelDate;
        public string Metrics_MaxWIP => Resources.Metrics_MaxWIP;
        public string Metrics_OccuredDate => Resources.Metrics_OccuredDate;
        public string Metrics_TotalDate => Resources.Metrics_TotalDate;
        public string Metrics_DurationOfMetrics => Resources.Metrics_DurationOfMetrics;
        public string Metrics_AveThroughput => Resources.Metrics_AveThroughput;
        public string Metrics_DoneItems => Resources.Metrics_DoneItems;
        public string Metrics_EstimatedCycleTime => Resources.Metrics_EstimatedCycleTime;
        public string Metrics_EstimationMuch => Resources.Metrics_EstimationMuch;
        public string Metrics_EstimationProper => Resources.Metrics_EstimationProper;
        public string Metrics_EstimationSmall => Resources.Metrics_EstimationSmall;
        public string Metrics_PerWeek => Resources.Metrics_PerWeek;
        public string Metrics_UnknownWeeks => Resources.Metrics_UnknownWeeks;
        public string Metrics_Weeks => Resources.Metrics_Weeks;
        public string Metrics_AveCycleTime => Resources.Metrics_AveCycleTime;
        public string Metrics_CycleTimeLabel => Resources.Metrics_CycleTimeLabel;
        public string Metrics_DaysPerItem => Resources.Metrics_DaysPerItem;
        public string Metrics_DistributionLabel1 => Resources.Metrics_DistributionLabel1;
        public string Metrics_FormatLabelDay => Resources.Metrics_FormatLabelDay;
        public string Metrics_ItemsOfBugfix => Resources.Metrics_ItemsOfBugfix;
        public string Metrics_ItemsOfBlocked => Resources.Metrics_ItemsOfBlocked;

        public string ToolTip_BoardBack => Resources.ToolTip_BoardBack;
        public string ToolTip_BoardFront => Resources.ToolTip_BoardFront;
        public string ToolTip_MoveToBack => Resources.ToolTip_MoveToBack;
        public string ToolTip_MoveToFront => Resources.ToolTip_MoveToFront;
        public string ToolTip_StoryPoints(Repository.EstimatedWorkEffort estimatedWorkEffort)
        {
            switch (estimatedWorkEffort)
            {
                case Repository.EstimatedWorkEffort.Small:
                    return Resources.ToolTip_StoryPoints_Small;
                case Repository.EstimatedWorkEffort.Medium:
                    return Resources.ToolTip_StoryPoints_Medium;
                case Repository.EstimatedWorkEffort.Large:
                    return Resources.ToolTip_StoryPoints_Large;
                case Repository.EstimatedWorkEffort.LargeExtra:
                    return Resources.ToolTip_StoryPoints_Extra_Large;
                default:
                    return Resources.ToolTip_StoryPoints_Unknown;
            }
        }

        public string EditCard_StateOfHistory => Resources.CardEdit_StateOfHistory;
        public string EditCard_StateChangedOfHistory => Resources.CardEdit_StateChangedOfHistory;
        public string EditCard_StateOfHistoryBlockedOrMerged => Resources.CardEdit_StateOfHistoryBlockedOrMerged;
        public string EditCard_Blocked => Resources.CardEdit_Blocked;
        public string EditCard_MergedIntoMaster => Resources.CardEdit_MergedIntoMaster;
        public string EditCard_MergedIntoMajorBranch => Resources.CardEdit_MergedIntoMajorBranch;
        public string EditCard_MergedIntoMasterAndMajorBranch => Resources.CardEdit_MergedIntoMasterAndMajorBranch;
        public string EditCard_WorkItem => Resources.CardEdit_WorkItem;
        public string EditCard_Description => Resources.CardEdit_Description;
        public string EditCard_TestCases => Resources.CardEdit_TestCases;
        public string EditCard_DesignOverview => Resources.CardEdit_DesignOverview;

        public string DateTime_LongFormatter => Resources.DateTime_LongFormatter;
        public string DateTime_MonthDayFormatter => Resources.DateTime_MonthDayFormatter;
        public string Message_DeleteComfirmQuesion => Resources.Message_DeleteComfirmQuesion;
        public string Message_DeleteComfirmTitle => Resources.Message_DeleteComfirmTitle;
        public string Message_DeleteErrorTitle => Resources.Message_DeleteErrorTitle;
        public string Message_DeleteProcessStepComfirm => Resources.Message_DeleteProcessStepComfirm;
        public string Message_DeleteProcessStepError => Resources.Message_DeleteProcessStepError;
        public string Message_HasUnknownStoryPointsInReleasedItem => Resources.Message_HasUnknownStoryPointsInReleasedItem;
    }
}
