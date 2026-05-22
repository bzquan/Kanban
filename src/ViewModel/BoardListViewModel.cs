using Kanban.Util;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static Kanban.Util.Util;

namespace Kanban.ViewModel
{
    public class BoardListViewModel : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Pattern of Dialogs and Modal Popups
        /// http://www.codeproject.com/Articles/36745/Showing-Dialogs-When-Using-the-MVVM-Pattern
        /// </summary>
        //private MvvmDialogs.IDialogService m_DialogService;

        private Model.IBoardRepository m_BoardRepository;
        private Model.ICardRepository m_CardRepository;
        private Model.IBoardFactory m_BoardFactory;
        private ObservableCollection<Board> m_Boards;
        private Board m_selectedBoard;
        private IAppSettings m_AppSettings;
        private IViewModelProperties m_Properties;


        public ICommand GotoSystemSettingCommand => new DelegateCommandNoArg(GotoSystemSettingPage);
        public ICommand AddBoardCommand => new DelegateCommandNoArg(AddBoard);
        public ICommand RefreshCommand => new DelegateCommandNoArg(Refresh);
        // RaiseCanExecuteChangedの正常発行のため、ViewにこのCommandを提供する
        public DelegateCommandNoArg AddProcessStepCommand { get; private set; }

        public event Action<BoardSelectedArg> BoardSelecedEvent;
        public event Action<MetricChartType> MetricChartTypeChangedEvent;

        public ProcessViewModel ProcessViewModel { get; private set; }

        // Only for view design mode
        public BoardListViewModel()
        {
        }

        public BoardListViewModel(
            //MvvmDialogs.IDialogService dialogService,
            Model.IBoardRepository boardRepository,
            Model.ICardRepository cardRepository,
            Model.IBoardFactory boardFactory,
            IAppSettings appSettings,
            IViewModelProperties properties)
        {
            //m_DialogService = dialogService;
            m_BoardRepository = boardRepository;
            m_CardRepository = cardRepository;
            m_BoardFactory = boardFactory;
            m_AppSettings = appSettings;
            m_Properties = properties;

            EventAggregator<DeleteBoardRequestedArgs>.Instance.Event += OnDeleteBoardRequested;

            ProcessViewModel = new ProcessViewModel(parentViewModel: this, properties: properties, boardRepository: m_BoardRepository, cardRepository: m_CardRepository);
            // Delegate to ProcessViewModel
            // 理由: BoardListViewからProcessViewModelのAddProcessStepCommandを直接にBindingした場合、
            // CommandのCanExecuteChangedイベントにViewが設定されないので、CanExecuteChangedイベントを発行できない
            // 根本原因は不明。
            AddProcessStepCommand = ProcessViewModel.AddProcessStepCommand;
        }

        private async void AddBoard()
        {
            //Model.Board boardModel = new Model.Board();
            //await m_BoardRepository.Insert(boardModel);
            Model.Board boardModel = await m_BoardFactory.CreateBoard();
            Board board = new Board(boardModel);
            m_Boards.Insert(0, board);
            SelectBoard(0);
        }

        private void OnDeleteBoardRequested(object sender, DeleteBoardRequestedArgs arg)
        {
            Board board = arg.Board;
            string confirmMsg = String.Format(m_Properties.Message_DeleteComfirmQuesion, board.Title.Truncate(30));
            MessageBoxResult result = MessageBox.Show(confirmMsg,
                                                      m_Properties.Message_DeleteComfirmTitle,
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int index = m_Boards.IndexOf(board);
                m_Boards.Remove(board);
                m_BoardRepository.Delete(board.BoardModel);
                SelectBoard(index);
            }
        }

        public ObservableCollection<Board> BoardList => m_Boards;

        public async Task<ObservableCollection<Board>> LoadBoards()
        {
            List<Model.Board> modelBoards = await m_BoardRepository.LoadBoards();
            List<Board> boards = ToViewModelBoards(modelBoards);
            CreateObservableBoards(boards);

            SelectedBoard = GetDefaultBoard();
            base.OnPropertyChanged(nameof(BoardList));
            return m_Boards;
        }

        private Board GetDefaultBoard()
        {
            string lastUsedBoardID = m_AppSettings.LastUsedBoardID;
            Board lastUsedBoard = m_Boards.FirstOrDefault(x => x._id.ToString() == lastUsedBoardID);
            return lastUsedBoard ?? m_Boards[0];
        }

        public MetricChartType MetricChartShowType
        {
            get { return m_AppSettings.LastUsedMetricChartType; }
            set
            {
                m_AppSettings.LastUsedMetricChartType = value;
                OnPropertyChanged();
                MetricChartTypeChangedEvent?.Invoke(value);
            }
        }

        public string MetricChartType_CFD => EnumUtil.GetEnumDescription(MetricChartType.CumulativeFlowDiagram);
        public string MetricChartType_CycleTimeDistribution => EnumUtil.GetEnumDescription(MetricChartType.CycleTimeDistribution);
        public string MetricChartType_Throughput => EnumUtil.GetEnumDescription(MetricChartType.Throughput);
        public string MetricChartType_BugsAndBlocked => EnumUtil.GetEnumDescription(MetricChartType.BugsAndBlocked);
        public string MetricChartType_TeamVelocity => EnumUtil.GetEnumDescription(MetricChartType.TeamVelocity);

        private List<Board> ToViewModelBoards(List<Model.Board> modelBoards)
        {
            List<Board> boards = new List<Board>();
            modelBoards.ForEach(board => boards.Add(new Board(board)));
            return boards;
        }

        private void CreateObservableBoards(List<Board> boards)
        {
            DisableBoardCollectionChangedEvent();
            m_Boards = new ObservableCollection<Board>(boards);
            EnableBoardCollectionChangedEvent();
        }

        private void DisableBoardCollectionChangedEvent()
            => m_Boards.IfNotNull(x => x.CollectionChanged -= new NotifyCollectionChangedEventHandler(OnCollectionChanged));
        private void EnableBoardCollectionChangedEvent()
            => m_Boards.CollectionChanged += new NotifyCollectionChangedEventHandler(OnCollectionChanged);

        public void SelectBoard(int index)
        {
            if (index < m_Boards.Count)
            {
                SelectedBoard = m_Boards[index];
            }
            else if (index > 0)
            {
                SelectedBoard = m_Boards[index - 1];
            }
        }

        public Board SelectedBoard
        {
            get { return m_selectedBoard; }
            set
            {
                if (SetProperty(ref m_selectedBoard, value))
                {
                    BoardSelecedEvent?.Invoke(new BoardSelectedArg(value));
                    ProcessViewModel.Board = SelectedBoard;
                    if (SelectedBoard != null)
                    {
                        m_AppSettings.LastUsedBoardID = SelectedBoard._id.ToString();
                    }
                }

                AddProcessStepCommand.RaiseCanExecuteChanged();
            }
        }

        public void Add(Board board)
        {
            m_Boards.Insert(0, board);
            SelectBoard(0);
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateBoardSeqNo();

            if (e.OldItems != null)
            {
            }

            if (e.NewItems != null)
            {
            }
        }

        public FontFamily KanbanPracticeFontFamily
        {
            get
            {
                switch (m_AppSettings.Language)
                {
                    case Languages.Chinese:
                        return new FontFamily("Kaiti");
                    case Languages.English:
                        return Util.HandWriteFontFamily.Bradley_Hand_ITC_FontFamily;
                    case Languages.Japanese:
                        return new FontFamily("MS Mincho");
                }

                return Util.HandWriteFontFamily.Bradley_Hand_ITC_FontFamily;
            }
        }

        private void UpdateBoardSeqNo()
        {
            int seq_no = 0;
            foreach (Board board in m_Boards)
            {
                board.SeqNo = seq_no++;
            }
        }

        private void GotoSystemSettingPage()
        {
            GotoSystemSettingPageRequestedArg arg = new GotoSystemSettingPageRequestedArg();
            EventAggregator<GotoSystemSettingPageRequestedArg>.Instance.Publish(this, arg);
        }

        public ImageSource GotoSystemSettingPageImage => ImageFromResource("admin.png");

        private async void Refresh()
        {
            await LoadBoards();
        }
    }
}
