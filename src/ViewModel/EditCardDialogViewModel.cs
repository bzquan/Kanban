using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Kanban.Util.EnumUtil;

namespace Kanban.ViewModel
{
    public class EditCardDialogViewModel : NotifyPropertyChangedBase
    {
        private Board Board { get; set; }
        private Model.IActivityRepository ActivityRepository { get; set; }
        private IViewModelProperties Properties { get; set; }

        public DelegateCommand<string> UpdateTitleCommand { get; private set; }
        public DelegateCommand<string> UpdateTagCommand { get; private set; }
        public DelegateCommand<string> UpdateExecutorsCommand { get; private set; }

        public EditCardDialogViewModel(Board board, Card card, IViewModelProperties properties, Model.IActivityRepository activityRepository)
        {
            if (board is null)
            {
                throw new ArgumentNullException(nameof(Board));
            }
            if (card is null)
            {
                throw new ArgumentNullException(nameof(Card));
            }

            UpdateTitleCommand = new DelegateCommand<string>(OnTitleChanged);
            UpdateTagCommand = new DelegateCommand<string>(OnTagChanged);
            UpdateExecutorsCommand = new DelegateCommand<string>(OnExecutorsChanged);

            Board = board;
            Card = card;
            Properties = properties;
            ActivityRepository = activityRepository;

            InitializeActivityDataTable();
            RenewActivitiesDataTable(card);
        }

        void InitializeActivityDataTable()
        {
            DataColumn[] activityColumns = {
                new DataColumn(Properties.EditCard_StateOfHistory, typeof(string)),
                new DataColumn(Properties.EditCard_StateChangedOfHistory, typeof(string)),
                new DataColumn(Properties.EditCard_StateOfHistoryBlockedOrMerged, typeof(string)),
            };

            ActivityTable.Columns.AddRange(activityColumns);
        }

        public Card Card { get; set; }

        public DataTable ActivityTable { get; } = new DataTable();

        public string CardType
        {
            get
            {
                return GetEnumDescription(Card.CardType);
            }
            set
            {
                Card.CardType = ToEnumValue<Repository.CardType>(value);
            }
        }

        public DateTime StateChangedDate => Card.StateChangedDate;

        public string EstimatedWorkEffort =>
            GetEnumDescription(Card.EstimatedWorkEffort);

        public List<int> StoryPointsRange => Model.Card.s_ValidStoryPoints;

        public int StoryPoints
        {
            get { return Card.StoryPoints; }
            set
            {
                Card.StoryPoints = value;
                base.OnPropertyChanged(nameof(EstimatedWorkEffort));
            }
        }

        private void OnTitleChanged(string title) => Card.Title = title;
        private void OnTagChanged(string tag) => Card.Tag = tag;
        private void OnExecutorsChanged(string executors) => Card.Executors = executors;

        async void RenewActivitiesDataTable(Card card)
        {
            ActivityTable.Clear();
            List<Model.Activity> activities = await ActivityRepository.GetActivitiesOf(card.CardModel);
            await Application.Current.Dispatcher.BeginInvoke(new Action(() => AddActivitiesToTable(activities))
                    , System.Windows.Threading.DispatcherPriority.Background, null);
        }

        private void AddActivitiesToTable(List<Model.Activity> activities)
        {
            foreach (Model.Activity activity in activities)
            {
                string blockedOrMergedStatus = BlockedOrMergedStatus(activity.WorkState);
                try
                {
                    ActivityTable.Rows.Add(WorkStateName(activity.WorkState),
                                           activity.StateChangedDate.ToString(Properties.DateTime_LongFormatter),
                                           blockedOrMergedStatus);
                }
                catch
                {
                    // do nothing
                }
            }

            string BlockedOrMergedStatus(Model.WorkState workState)
            {
                if (workState.IsBlocked)
                    return Properties.EditCard_Blocked;
                else if (workState.IsMergedIntoMaster && workState.IsMergedIntoMajorBranch)
                    return Properties.EditCard_MergedIntoMasterAndMajorBranch;
                else if (workState.IsMergedIntoMaster)
                    return Properties.EditCard_MergedIntoMaster;
                else if (workState.IsMergedIntoMajorBranch)
                    return Properties.EditCard_MergedIntoMajorBranch;
                else
                    return "";
            }
        }

        string WorkStateName(Model.WorkState workstate)
        {
            List<Repository.ProcessStep> steps = Board.ProcessSteps;
            Repository.ProcessStep step = steps.FirstOrDefault(x => x.PhaseSeqNo == workstate.ProcessStepSeqNo);
            if (step != null)
            {
                string step_state = workstate.IsWIP ? step.WIPTitle : step.DoneTitle;
                return step.Name + "(" + step_state + ")";
            }

            return Util.Util.TraceMessage("Internal Error(workstate.seqNo: " + workstate.ProcessStepSeqNo + ")");
        }
    }
}
