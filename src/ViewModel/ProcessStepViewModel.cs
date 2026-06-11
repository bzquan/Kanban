using System.Windows.Media;
using static Kanban.Util.CircleNumImages;

namespace Kanban.ViewModel
{
    public class ProcessStepViewModel : NotifyPropertyChangedBase
    {
        WIPDoneViewModelFactory m_BoardsViewModelFactory;

        Board m_Board;
        WIPDoneViewModel m_WIPCards;
        WIPDoneViewModel m_DoneCards;
        int m_WIPCardsCount = 0;
        int m_DoneCardsCount = 0;

        public ProcessStepViewModel(Board board, Repository.ProcessStep step, WIPDoneViewModelFactory boardsViewModelFactory)
        {
            m_Board = board;
            ProcessStep = step;
            m_BoardsViewModelFactory = boardsViewModelFactory;
        }

        public Repository.ProcessStep ProcessStep { get; set; }

        public string LabelColorName
        {
            get { return ProcessStep.LabelColor; }
        }

        public string StepName
        {
            get { return ProcessStep.Name; }
        }

        public string WIPTitle
        {
            get { return ProcessStep.WIPTitle; }
        }

        public DrawingImage WIPCardsCountImage =>
            GetCircleNumImage(text: WIPCardsCount.ToString(), background: Colors.Yellow, font_color: Colors.Blue);

        public DrawingImage WIPCardTotalPointsImage
        {
            get
            {
                var totalPoints = m_WIPCards.TotalPointsOfCards;
                return GetStoryPointSImage(text: totalPoints.ToString(), background: Colors.LightCyan, font_color: Colors.Blue);
            }
        }

        private int WIPCardsCount
        {
            get { return m_WIPCardsCount; }
            set
            {
                m_WIPCardsCount = value;
                base.OnPropertyChanged(nameof(WIPCardsCountImage));
                base.OnPropertyChanged(nameof(WIPCardTotalPointsImage));
            }
        }

        public string DoneTitle
        {
            get { return ProcessStep.DoneTitle; }
        }

        public DrawingImage DoneCardsCountImage =>
            GetCircleNumImage(text: DoneCardsCount.ToString(), background: Colors.Yellow, font_color: Colors.Blue);

        public DrawingImage DoneCardTotalPointsImage
        {
            get
            {
                var totalPoints = m_DoneCards.TotalPointsOfCards;
                return GetStoryPointSImage(text: totalPoints.ToString(), background: Colors.LightCyan, font_color: Colors.Blue);
            }
        }

        private int DoneCardsCount
        {
            get { return m_DoneCardsCount; }
            set
            {
                m_DoneCardsCount = value;
                base.OnPropertyChanged(nameof(DoneCardsCountImage));
                base.OnPropertyChanged(nameof(DoneCardTotalPointsImage));
            }
        }

        public WIPDoneViewModel WIPCards
        {
            get { return m_WIPCards; }
            private set { SetProperty(ref m_WIPCards, value); }
        }

        public WIPDoneViewModel DoneCards
        {
            get { return m_DoneCards; }
            private set { SetProperty(ref m_DoneCards, value); }
        }

        public HashSet<DateTime> GetScheduledReleaseDates()
        {
            var result = new HashSet<DateTime>();
            result.UnionWith(GetScheduledReleaseDates(WIPCards));
            result.UnionWith(GetScheduledReleaseDates(DoneCards));

            return result;
        }

        private HashSet<DateTime> GetScheduledReleaseDates(WIPDoneViewModel wipDoneViewModel)
        {
            var result = new HashSet<DateTime>();
            foreach (var card in wipDoneViewModel.Cards)
            {
                if (card.IsScheduledToRelease)
                {
                    var dateTime = new DateTime(card.ReleaseDate.Year, card.ReleaseDate.Month, card.ReleaseDate.Day);
                    result.Add(dateTime);
                }
            }
            return result;
        }

        /// <summary>
        /// WIPとDoneのカードを読み込む。
        /// 絞り込み条件が変更された時、本関数が呼ばれるので、その都度該当ViewModelを再生成する。
        /// </summary>
        /// <param name="cardFilter"></param>
        /// <param name="loadCardsOnBackBoard"></param>
        public void LoadContents(string cardFilter, bool loadCardsOnBackBoard)
        {
            WIPCards = m_BoardsViewModelFactory(m_Board, new Model.WorkState { ProcessStepSeqNo = ProcessStep.PhaseSeqNo, IsWIP = true }, cardFilter, loadCardsOnBackBoard);
            WIPCards.CardsCountChanged += OnWIPCardsCountChanged;
            DoneCards = m_BoardsViewModelFactory(m_Board, new Model.WorkState { ProcessStepSeqNo = ProcessStep.PhaseSeqNo, IsWIP = false }, cardFilter, loadCardsOnBackBoard);
            DoneCards.CardsCountChanged += OnDoneCardsCountChanged;
        }

        private void OnWIPCardsCountChanged(int cardCounts) => WIPCardsCount = cardCounts;
        private void OnDoneCardsCountChanged(int cardCounts) => DoneCardsCount = cardCounts;

        public void AddNewCard()
        {
            WIPCards.AddNewCard();
        }

        public void ReplicateCard(Card srcCard)
        {
            WIPCards.ReplicateCard(srcCard);
        }
    }
}
