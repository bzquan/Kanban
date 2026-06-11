using Kanban.Util;
using System.Windows.Input;

namespace Kanban.ViewModel
{
    public class BoardPageViewModel : NotifyPropertyChangedBase
    {
        private static DateTime? LatestScheduledReleaseDate { get; set; } = null;

        private bool m_ShowFrontBoard = true;
        private bool m_ShowCardDetails = true;
        private string m_CurrentFilter = "";
        private ProcessStepViewModelFactory m_ProcessStepViewModelFactory;
        private List<ProcessStepViewModel> m_ProcessStepViewModel = new List<ProcessStepViewModel>();
        private IAppSettings m_AppSettings;
        private IViewModelProperties m_Properties;

        public Board Board { get; set; }
        public ICommand FilterCardsCommand { get; private set; }
        public DelegateCommandNoArg AddCardCommand { get; private set; }
        public ICommand ReloadBoardCommand { get; private set; }

        public BoardPageViewModel(ProcessStepViewModelFactory processStepViewModelFactory, IAppSettings appSettings, IViewModelProperties properties)
        {
            m_ProcessStepViewModelFactory = processStepViewModelFactory;
            m_AppSettings = appSettings;
            m_Properties = properties;

            FilterCardsCommand = new DelegateCommand<string>(LoadContents);
            AddCardCommand = new DelegateCommandNoArg(OnAddCard, CanAddCard);
            ReloadBoardCommand = new DelegateCommand<string>(LoadContents);

            EventAggregator<CardsOfBoardRetrievedArgs>.Instance.Event += OnCardsOfBoardRetrieved;
        }

        public void Initialize(Board board)
        {
            Board = board;
            foreach (Repository.ProcessStep step in board.ProcessSteps)
            {
                ProcessStepViewModel processStepViewModel = m_ProcessStepViewModelFactory(board, step);
                m_ProcessStepViewModel.Add(processStepViewModel);
            }

            m_AppSettings.LastUsedBoardID = board._id.ToString();
        }

        public static bool IsLatestScheduledReleaseDate(DateTime dateTime)
        {
            return (LatestScheduledReleaseDate?.Year == dateTime.Year) &&
                   (LatestScheduledReleaseDate?.Month == dateTime.Month) &&
                   (LatestScheduledReleaseDate?.Day == dateTime.Day);
        }

        private string CurrentFilter
        {
            get { return m_CurrentFilter; }
            set
            {
                m_CurrentFilter = value;

                if (IsFiltering())
                    EventAggregator<DragAndDropLib.DisallowDragAndDropRequestArg>.Instance.Publish(this, new DragAndDropLib.DisallowDragAndDropRequestArg());
                else
                    EventAggregator<DragAndDropLib.AllowDragAndDropRequestArg>.Instance.Publish(this, new DragAndDropLib.AllowDragAndDropRequestArg());
            }
        }

        private bool IsFiltering() => (m_CurrentFilter?.Trim()?.Length > 0);

        public void LoadContents(string cardFilter)
        {
            CurrentFilter = cardFilter;
            m_ProcessStepViewModel.ForEach(x => x.LoadContents(cardFilter, loadCardsOnBackBoard: !ShowFrontBoard));
        }

        public bool ShowFrontBoard
        {
            get { return m_ShowFrontBoard; }
            set
            {
                if (m_ShowFrontBoard == value) return;

                m_ShowFrontBoard = value;
                OnPropertyChanged();
                LoadContents(CurrentFilter);
                OnPropertyChanged(nameof(BoardFrontBackImageUri));
                OnPropertyChanged(nameof(BoardFrontBackImageToolTip));
                AddCardCommand.RaiseCanExecuteChanged();
            }
        }

        public bool ShowDetailedCards
        {
            get { return m_ShowCardDetails; }

            set
            {
                if (m_ShowCardDetails == value) return;

                Board.ShowDetailedCards = value;
                m_ShowCardDetails = value;
                OnPropertyChanged();

                LoadContents(CurrentFilter);
                AddCardCommand.RaiseCanExecuteChanged();
            }
        }

        public string BoardFrontBackImageUri
        {
            get
            {
                string imageName = ShowFrontBoard ? "front_board.png" : "back_board.png";
                return Util.Util.PackImageURI(imageName);
            }
        }

        public string BoardFrontBackImageToolTip
        {
            get { return ShowFrontBoard ? m_Properties.ToolTip_BoardFront : m_Properties.ToolTip_BoardBack; }
        }

        public ProcessStepViewModel GetProcessStepViewModel(int seqNo)
        {
            return m_ProcessStepViewModel.First(x => x.ProcessStep.PhaseSeqNo == seqNo);
        }

        public void ReplicateCard(Card srcCard)
        {
            m_ProcessStepViewModel[0].ReplicateCard(srcCard);
        }

        private Repository.ProcessStep GetStepByName(string value)
        {
            return Board.ProcessSteps.First(x => x.Name == value);
        }

        private void OnAddCard()
        {
            m_ProcessStepViewModel[0].AddNewCard();
            EventAggregator<NewCardAddedArg>.Instance.Publish(this, new NewCardAddedArg());
        }

        private bool CanAddCard() => ShowFrontBoard;

        private void OnCardsOfBoardRetrieved(object sender, CardsOfBoardRetrievedArgs arg)
        {
            if ((m_ProcessStepViewModel.Last().ProcessStep.PhaseSeqNo == arg.ProcessStepSeqNo))
            {
                PublishLatestScheduledReleaseDate();
            }
        }

        private void PublishLatestScheduledReleaseDate()
        {
            var latestDate = GetFutureLatestScheduledReleaseDate();

            if (latestDate.HasValue && !IsLatestScheduledReleaseDate(latestDate.Value))
            {
                LatestScheduledReleaseDate = latestDate;
                EventAggregator<LatestScheduledReleaseDateChangedArg>.Instance.Publish(this, new LatestScheduledReleaseDateChangedArg());
            }
        }

        private DateTime? GetFutureLatestScheduledReleaseDate()
        {
            var scheduledReleaseDates = new SortedSet<DateTime>();
            // 最後尾の処理Step以外からリリース予定日を抽出する
            for (int i = 0; i < m_ProcessStepViewModel.Count - 1; i++)
            {
                scheduledReleaseDates.UnionWith(m_ProcessStepViewModel[i].GetScheduledReleaseDates());
            }

            DateTime now = DateTime.Now;
            foreach (var dateTime in scheduledReleaseDates)
            {
                bool isFuture = (dateTime.Year >= now.Year) &&
                    (dateTime.Month >= now.Month) &&
                    (dateTime.Day >= now.Day);

                if (isFuture) return dateTime;
            }

            return null;
        }
    }
}
