using Kanban.Util;
using System.Windows.Input;

namespace Kanban.ViewModel
{
    public class BoardPageViewModel : NotifyPropertyChangedBase
    {
        private static DateTime? LatestScheduledReleaseDate { get; set; } = null;

        private ProcessStepViewModelFactory ProcessStepViewModelFactory { get; }
        private List<ProcessStepViewModel> ProcessStepViewModels { get; } = new List<ProcessStepViewModel>();
        private IAppSettings AppSettings { get; }
        private IViewModelProperties Properties { get; }

        public Board Board { get; set; }
        public ICommand FilterCardsCommand { get; private set; }
        public DelegateCommandNoArg AddCardCommand { get; private set; }
        public ICommand ReloadBoardCommand { get; private set; }

        public BoardPageViewModel(ProcessStepViewModelFactory processStepViewModelFactory, IAppSettings appSettings, IViewModelProperties properties)
        {
            ProcessStepViewModelFactory = processStepViewModelFactory;
            AppSettings = appSettings;
            Properties = properties;

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
                ProcessStepViewModel processStepViewModel = ProcessStepViewModelFactory(board, step);
                ProcessStepViewModels.Add(processStepViewModel);
            }

            AppSettings.LastUsedBoardID = board._id.ToString();
        }

        public static bool IsLatestScheduledReleaseDate(DateTime dateTime)
        {
            return (LatestScheduledReleaseDate?.Year == dateTime.Year) &&
                   (LatestScheduledReleaseDate?.Month == dateTime.Month) &&
                   (LatestScheduledReleaseDate?.Day == dateTime.Day);
        }

        private string CurrentFilter
        {
            get => field;
            set
            {
                field = value;
                if (IsFiltering())
                    EventAggregator<DragAndDropLib.DisallowDragAndDropRequestArg>.Instance.Publish(this, new DragAndDropLib.DisallowDragAndDropRequestArg());
                else
                    EventAggregator<DragAndDropLib.AllowDragAndDropRequestArg>.Instance.Publish(this, new DragAndDropLib.AllowDragAndDropRequestArg());
            }
        } = "";

        private bool IsFiltering() => (CurrentFilter?.Trim()?.Length > 0);

        public void LoadContents(string cardFilter)
        {
            CurrentFilter = cardFilter;
            ProcessStepViewModels.ForEach(x => x.LoadContents(cardFilter, loadCardsOnBackBoard: !ShowFrontBoard));
        }

        public bool ShowFrontBoard
        {
            get => field;
            set
            {
                if (field == value) return;

                field = value;
                OnPropertyChanged();
                LoadContents(CurrentFilter);
                OnPropertyChanged(nameof(BoardFrontBackImageUri));
                OnPropertyChanged(nameof(BoardFrontBackImageToolTip));
                AddCardCommand.RaiseCanExecuteChanged();
            }
        } = true;

        public bool ShowDetailedCards
        {
            get => field;
            set
            {
                if (field == value) return;

                Board.ShowDetailedCards = value;
                field = value;
                OnPropertyChanged();

                LoadContents(CurrentFilter);
                AddCardCommand.RaiseCanExecuteChanged();
            }
        } = true;

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
            get { return ShowFrontBoard ? Properties.ToolTip_BoardFront : Properties.ToolTip_BoardBack; }
        }

        public ProcessStepViewModel GetProcessStepViewModel(int seqNo)
        {
            return ProcessStepViewModels.First(x => x.ProcessStep.PhaseSeqNo == seqNo);
        }

        public void ReplicateCard(Card srcCard)
        {
            ProcessStepViewModels[0].ReplicateCard(srcCard);
        }

        private Repository.ProcessStep GetStepByName(string value)
        {
            return Board.ProcessSteps.First(x => x.Name == value);
        }

        private void OnAddCard()
        {
            ProcessStepViewModels[0].AddNewCard();
            EventAggregator<NewCardAddedArg>.Instance.Publish(this, new NewCardAddedArg());
        }

        private bool CanAddCard() => ShowFrontBoard;

        private void OnCardsOfBoardRetrieved(object sender, CardsOfBoardRetrievedArgs arg)
        {
            if ((ProcessStepViewModels.Last().ProcessStep.PhaseSeqNo == arg.ProcessStepSeqNo))
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
            for (int i = 0; i < ProcessStepViewModels.Count - 1; i++)
            {
                scheduledReleaseDates.UnionWith(ProcessStepViewModels[i].GetScheduledReleaseDates());
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
