using Kanban.Util;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace Kanban.ViewModel
{
    public delegate void CardsCountChangedHander(int cardCounts);

    public class WIPDoneViewModel : NotifyPropertyChangedBase
    {
        private ObservableCollection<Card> m_Cards = new ObservableCollection<Card>();

        private Card m_selectedCard;
        private string m_CardFilter;
        private bool m_LoadCardsOnBackBoard;
        private CardFactory m_CardFactory;
        private IViewModelProperties m_Properties;
        private Model.ICardRepository m_CardRepository;

        public WIPDoneViewModel(
            Board board,
            Model.WorkState cardWorkState,
            string cardFilter,
            bool loadCardsOnBackBoard,
            CardFactory cardFactory,
            IViewModelProperties properties,
            Model.ICardRepository cardRepository)
        {
            Board = board;
            CardWorkState = cardWorkState;
            m_CardFilter = cardFilter;
            m_LoadCardsOnBackBoard = loadCardsOnBackBoard;
            m_CardFactory = cardFactory;
            m_Properties = properties;
            m_CardRepository = cardRepository;

            m_Cards.CollectionChanged += OnCollectionChanged;
            EventAggregator<CardSelectedArgs>.Instance.Event += OnCardSelected;
            EventAggregator<SwitchFrontBackBoardRequestedArgs>.Instance.Event += OnSwitchFrontBackBoardRequested;
            EventAggregator<DeleteCardRequestedArgs>.Instance.Event += OnDeleteCardRequested;
        }

        Board Board { get; set; }

        public event CardsCountChangedHander CardsCountChanged;

        public ObservableCollection<Card> Cards
        {
            get
            {
                RetrieveCards();
                return m_Cards;
            }
        }

        public int TotalPointsOfCards => m_Cards.Sum(x => x.StoryPoints <= 100 ? x.StoryPoints : 0);

        private async void RetrieveCards()
        {
            DisableCardCollectionChangedEvent();

            List<Card> cards = await RetrieveCardsFromBoard();
            m_Cards.Clear();
            cards.ForEach(x => m_Cards.Add(x));

            EnableCardCollectionChangedEvent();
            RaiseCardsCountChangedEvent();
            EventAggregator<CardsOfBoardRetrievedArgs>.Instance.Publish(this, new CardsOfBoardRetrievedArgs(CardWorkState.ProcessStepSeqNo));
        }

        private void DisableCardCollectionChangedEvent() => m_Cards.CollectionChanged -= OnCollectionChanged;
        private void EnableCardCollectionChangedEvent() => m_Cards.CollectionChanged += OnCollectionChanged;
        private void RaiseCardsCountChangedEvent() => CardsCountChanged?.Invoke(m_Cards.Count);

        private async Task<List<Card>> RetrieveCardsFromBoard()
        {
            Repository.OnBoardFilter onBoardFilter = m_LoadCardsOnBackBoard ? Repository.OnBoardFilter.OnBackBoard : Repository.OnBoardFilter.OnFrontBoard;
            var modelCards = await m_CardRepository.GetCards(Board.BoardModel, CardWorkState, m_CardFilter, onBoardFilter);
            List<Card> cards = new List<Card>();
            foreach (Model.Card card in modelCards)
            {
                cards.Add(new Card(card, Board.ShowDetailedCards));
            }
            return cards;
        }

        public Model.WorkState CardWorkState { get; set; }

        public async void AddNewCard()
        {
            Card card = await m_CardFactory(Board, CardWorkState);
            m_Cards.Add(card);
        }

        /// <summary>
        /// 選択されたCard一つのみにするため、Current Step以外のStepでは、Card選択を解除する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnCardSelected(object sender, CardSelectedArgs arg)
        {
            if (sender != this)
            {
                SelectedCard = null;
            }
        }

        public void SelectCard(int index)
        {
            if (index < m_Cards.Count)
            {
                SelectedCard = m_Cards[index];
            }
            else if (index > 0)
            {
                SelectedCard = m_Cards[index - 1];
            }
            else
            {
                SelectedCard = null;
            }
        }

        public Card SelectedCard
        {
            get { return m_selectedCard; }
            set
            {
                if (SetProperty(ref m_selectedCard, value) && (m_selectedCard != null))
                {
                    CardSelectedArgs arg = new CardSelectedArgs(m_selectedCard);
                    EventAggregator<CardSelectedArgs>.Instance.Publish(this, arg);
                }
            }
        }

        private void OnSwitchFrontBackBoardRequested(object sender, SwitchFrontBackBoardRequestedArgs arg)
        {
            Card card = arg.CardToSwitch;
            if (!IsManagedByThisViewModel(card)) return;

            m_Cards.Remove(card);
            card.IsOnBackBoard = !card.IsOnBackBoard;
            SelectedCard = null;
        }

        private void OnDeleteCardRequested(object sender, DeleteCardRequestedArgs arg)
        {
            Card card = arg.CardToDelete;
            if (!IsManagedByThisViewModel(card)) return;

            string confirmMsg = String.Format(m_Properties.Message_DeleteComfirmQuesion, card.Title.Truncate(30));
            MessageBoxResult result = MessageBox.Show(confirmMsg,
                                                      m_Properties.Message_DeleteComfirmTitle,
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                m_Cards.Remove(card);
                card.DeleteFromDB();
                SelectedCard = null;
            }
        }

        private bool IsManagedByThisViewModel(Card card) => m_Cards.Contains(card);

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateCardSeqNo();
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddActivityToCard(e.NewItems);
                    break;
                default:
                    break;
            }
            NotifyCardCollectionChanged2View();
        }

        private void AddActivityToCard(System.Collections.IList newItems)
        {
            foreach (Card card in newItems)
            {
                if (card.WorkState != CardWorkState)
                {
                    card.CreateNewActivities(CardWorkState);
                }
            }
        }

        private void NotifyCardCollectionChanged2View()
        {
            RaiseCardsCountChangedEvent();
        }

        private void UpdateCardSeqNo()
        {
            int seq_no = 0;
            foreach (Card card in m_Cards)
            {
                if (card.SeqNo != seq_no)
                {
                    card.SeqNo = seq_no;
                }
                seq_no++;
            }
        }
    }
}
