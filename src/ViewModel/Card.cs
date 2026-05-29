using Kanban.Repository;
using Kanban.Util;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static Kanban.Model.CardMetrics;
using static Kanban.Util.CircleNumImages;

namespace Kanban.ViewModel;

public class Card : NotifyPropertyChangedBase
{
    private static readonly Brush s_BlueBrush;
    private static readonly Brush s_YellowBrush;
    private DelegateCommandNoArg m_SwitchFrontBackCommand;
    private DelegateCommandNoArg m_DeleteCardCommand;
    private WorkedDaysImages m_WorkedDays = new WorkedDaysImages();

    class WorkedDaysImages
    {
        public int WorkedDays { get; set; } = -1;
        public Repository.EstimatedWorkEffort EstimatedWorkEffort { get; set; }
        public DrawingImage WorkedDaysProgressImage { get; set; }
        public DrawingImage WorkedDaysImage { get; set; }

        public bool NeedRecreate(Repository.EstimatedWorkEffort newEstimatedWorkEffort)
        {
            if ((WorkedDays < 0) || (WorkedDaysProgressImage == null)) return true;

            return (EstimatedWorkEffort != newEstimatedWorkEffort);
        }
    }

    static Card()
    {
        s_BlueBrush = new SolidColorBrush(Colors.Blue);
        s_BlueBrush.Freeze();

        s_YellowBrush = new SolidColorBrush(Colors.Yellow);
        s_YellowBrush.Freeze();
    }

    public Model.Card CardModel { get; set; }

    public static IViewModelProperties ViewModelProperties { get; set; }

    public Card(Model.Card modelCard, bool showDetailedCard)
    {
        CardModel = modelCard;
        ShowDetailedCard = showDetailedCard;
        m_SwitchFrontBackCommand = new DelegateCommandNoArg(OnSwitchFrontBackRequested);
        m_DeleteCardCommand = new DelegateCommandNoArg(OnDeleteCardRequested);

        EventAggregator<LatestScheduledReleaseDateChangedArg>.Instance.Event += OnLatestScheduledReleaseDateChanged;
    }

    public ICommand SwitchFrontBackCommand => m_SwitchFrontBackCommand;
    public ICommand DeleteCardCommand => m_DeleteCardCommand;

    public void RefreshCardContentsInfo()
    {
        base.OnPropertyChanged(nameof(CardContentsInfo));
    }

    public string CardContentsInfo
    {
        get
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ViewModelProperties.EditCard_WorkItem)
              .Append(Title);

            if (Summary?.Length > 0)
            {
                sb.AppendLine()
                  .AppendLine()
                  .AppendLine(ViewModelProperties.EditCard_Description)
                  .Append(Summary);
            }
            if (TestCases?.Length > 0)
            {
                sb.AppendLine()
                  .AppendLine()
                  .AppendLine(ViewModelProperties.EditCard_TestCases)
                  .Append(TestCases);
            }
            if (DesignOverview?.Length > 0)
            {
                sb.AppendLine()
                  .AppendLine()
                  .AppendLine(ViewModelProperties.EditCard_DesignOverview)
                  .Append(DesignOverview);
            }


            return sb.ToString();
        }
    }

    public DrawingImage CardPriority
    {
        get
        {
            string seqNo = (CardModel.SeqNo + 1).ToString();
            return GetCircleNumImage(text: seqNo, background: Colors.LightCyan, font_color: Colors.Blue);
        }
    }

    public int SeqNo
    {
        get { return CardModel.SeqNo; }
        set
        {
            // SeqNo画像の送信のため、SeqNoImageの更新を通知する
            SetProperty(CardModel, value, nameof(CardModel.SeqNo), nameof(CardPriority));
        }
    }

    public string Title
    {
        get { return CardModel.Title; }
        set { SetProperty(CardModel, value); }
    }

    public string Tag
    {
        get { return CardModel.Tag; }
        set { SetProperty(CardModel, value); }
    }

    public bool ShowDetailedCard { get; set; }

    public string Executors
    {
        get { return CardModel.Executors; }
        set { SetProperty(CardModel, value); }
    }

    public string Summary
    {
        get { return CardModel.Summary; }
        set { SetProperty(CardModel, value); }
    }

    public Model.WorkState WorkState
    {
        get { return CardModel.WorkState; }
        set { CardModel.WorkState = value; }
    }

    public string TestCases
    {
        get { return CardModel.TestCases; }
        set { CardModel.TestCases = value; }
    }

    public string DesignOverview
    {
        get { return CardModel.DesignOverview; }
        set { CardModel.DesignOverview = value; }
    }

    public DateTime CreateDate
    {
        get { return CardModel.CreateDate; }
        set { SetProperty(CardModel, value); }
    }

    public DateTime StateChangedDate
    {
        get { return CardModel.StateChangedDate; }
        set { SetProperty(CardModel, value); }
    }

    public DateTime ReleaseDate
    {
        get { return CardModel.ReleaseDate; }
        set
        {
            SetProperty(CardModel, value);
            // 画面更新のため、CardTitleBackgroundColorの変更を通知する。
            base.OnPropertyChanged(nameof(CardTagBackgroundColor));
        }
    }

    public Brush ReleaseDateForeground
    {
        get => CardType switch
        {
            CardType.Feature => s_YellowBrush,
            _ => s_BlueBrush
        };
    }

    public Repository.CardType CardType
    {
        get { return CardModel.CardType; }
        set
        {
            // 画面更新のため、下記属性の更新を通知する
            SetProperty(CardModel, value, nameof(CardModel.CardType), nameof(CardBackgroundColor));
            base.OnPropertyChanged(nameof(CardTypeImageUri));
            base.OnPropertyChanged(nameof(ToolTipOfCardType));
            base.OnPropertyChanged(nameof(ReleaseDateForeground));
        }
    }

    public string CardTypeImageUri
    {
        get { return CardTypePresentation.CardTypeIconUri(CardType); }
    }

    public string ToolTipOfCardType
    {
        get { return CardTypePresentation.CardTypeToolTip(CardType); }
    }

    public Repository.EstimatedWorkEffort EstimatedWorkEffort
    {
        get { return CardModel.EstimatedWorkEffort; }
    }

    public int StoryPoints
    {
        get { return CardModel.StoryPoints; }
        set
        {
            SetProperty(CardModel, value);
            base.OnPropertyChanged(nameof(CardStoryPoints));
        }
    }

    public DrawingImage CardStoryPoints
    {
        get
        {
            var colors = CardStoryPointsColor();
            return GetStoryPointSImage(text: StoryPoints2String, background: colors.Item1, font_color: colors.Item2);
        }
    }

    private string StoryPoints2String => IsUnknownStoryPoints ? "?" : StoryPoints.ToString();
    private bool IsUnknownStoryPoints => (StoryPoints == Repository.Card.UNKNOWN_STORY_POINTS);

    private Tuple<Color, Color> CardStoryPointsColor()
    {
        switch (EstimatedWorkEffort)
        {
            case Repository.EstimatedWorkEffort.Small:
                return Tuple.Create(Colors.Yellow, Colors.Blue);
            case Repository.EstimatedWorkEffort.Medium:
                return Tuple.Create(Colors.Lime, Colors.Blue);
            case Repository.EstimatedWorkEffort.Large:
                return Tuple.Create(Colors.Orange, Colors.Blue);
            case Repository.EstimatedWorkEffort.LargeExtra:
                return Tuple.Create(Colors.Red, Colors.Blue);
            default:
                return Tuple.Create(Colors.Red, Colors.White);
        }
    }

    public Color CardBackgroundColor
    {
        get { return CardTypePresentation.CardBackgroundColor(CardType); }
    }

    public string ToolTipOfStoryPoints
    {
        get { return ViewModelProperties.ToolTip_StoryPoints(CardModel.EstimatedWorkEffort); }
    }

    public bool IsBlocked
    {
        get { return CardModel.IsBlocked; }
        set
        {
            if (IsBlocked != value)
            {
                SetProperty(CardModel, value);
                CreateNewActivities(WorkState);
                // 画面更新のため、CardTitleBackgroundColorの変更を通知する。
                base.OnPropertyChanged(nameof(CardTitleBackgroundColor));
            }
        }
    }

    public bool IsMergedIntoMaster
    {
        get { return CardModel.IsMergedIntoMaster; }
        set
        {
            if (IsMergedIntoMaster != value)
            {
                SetProperty(CardModel, value);
                CreateNewActivities(WorkState);
                // 画面更新のため、CardTitleBackgroundColorの変更を通知する。
                base.OnPropertyChanged(nameof(CardTitleBackgroundColor));
                base.OnPropertyChanged(nameof(IsMergeCompleted));
            }
        }
    }

    public bool IsMergedIntoMajorBranch
    {
        get { return CardModel.IsMergedIntoMajorBranch; }
        set
        {
            if (IsMergedIntoMajorBranch != value)
            {
                SetProperty(CardModel, value);
                CreateNewActivities(WorkState);
                // 画面更新のため、CardTitleBackgroundColorの変更を通知する。
                base.OnPropertyChanged(nameof(CardTitleBackgroundColor));
                base.OnPropertyChanged(nameof(IsMergeCompleted));
            }
        }
    }

    public bool IsMergeCompleted => IsMergedIntoMaster && IsMergedIntoMajorBranch;

    public bool IsOnBackBoard
    {
        get { return CardModel.IsOnBackBoard; }
        set
        {
            SetProperty(CardModel, value);
            // 画面更新のため、FrontBackImageUriとToolTipFrontBackの変更を通知する。
            base.OnPropertyChanged(nameof(FrontBackImageUri));
            base.OnPropertyChanged(nameof(ToolTipFrontBack));
        }
    }

    public string FrontBackImageUri
    {
        get
        {
            string image_name = IsOnBackBoard ? "back_board.png" : "front_board.png";
            return Util.Util.PackImageURI(image_name);
        }
    }

    public string ToolTipFrontBack
    {
        get
        {
            return IsOnBackBoard ? ViewModelProperties.ToolTip_MoveToFront : ViewModelProperties.ToolTip_MoveToBack;
        }
    }

    public DrawingImage ProcessCycleTimeDaysImage
    {
        get
        {
            CreateWorkedDaysImages();
            return m_WorkedDays.WorkedDaysImage;
        }
    }

    public DrawingImage ProcessCycleTimeImage
    {
        get
        {
            CreateWorkedDaysImages();
            return m_WorkedDays.WorkedDaysProgressImage;
        }
    }

    private async void CreateWorkedDaysImages()
    {
        if (!m_WorkedDays.NeedRecreate(EstimatedWorkEffort)) return;

        if (m_WorkedDays.WorkedDays < 0)
        {
            m_WorkedDays.WorkedDays = await CardModel.GetWorkedDays();
        }

        await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {

            m_WorkedDays.EstimatedWorkEffort = EstimatedWorkEffort;
            m_WorkedDays.WorkedDaysImage = GetCircleNumImage(text: m_WorkedDays.WorkedDays.ToString(), background: Colors.Yellow, font_color: Colors.Blue);
            m_WorkedDays.WorkedDaysProgressImage = WorkedDaysProgressImageCache.GetWorkedDaysImage(CardModel, m_WorkedDays.WorkedDays);

            OnPropertyChanged(nameof(ProcessCycleTimeDaysImage));
            OnPropertyChanged(nameof(ProcessCycleTimeImage));
        }));
    }

    public Color CardTitleBackgroundColor
    {
        get
        {
            if (IsBlocked)
                return Colors.DeepPink;
            else if (IsMergedIntoMaster && IsMergedIntoMajorBranch)
                return Colors.Lime;
            else if (IsMergedIntoMaster)
                return Colors.Green;
            else if (IsMergedIntoMajorBranch)
                return Colors.Cyan;
            else
                return Colors.BurlyWood;
        }
    }

    public bool IsScheduledToRelease
    {
        get => CardModel.IsScheduledToRelease;
        set
        {
            if (IsScheduledToRelease != value)
            {
                SetProperty(CardModel, value);
                // 画面更新のため、CardTagBackgroundColorの変更を通知する。
                base.OnPropertyChanged(nameof(CardTagBackgroundColor));
            }
        }
    }

    public Color CardTagBackgroundColor
    {
        get
        {
            if (IsScheduledToRelease)
                return BoardPageViewModel.IsLatestScheduledReleaseDate(ReleaseDate) ? Colors.Yellow : Colors.Orange;
            else
                return Colors.SkyBlue;
        }
    }

    private void OnLatestScheduledReleaseDateChanged(object sender, LatestScheduledReleaseDateChangedArg arg)
    {
        if (IsScheduledToRelease)
        {
            // 画面更新のため、CardTagBackgroundColorの変更を通知する。
            base.OnPropertyChanged(nameof(CardTagBackgroundColor));
        }
    }

    public void CreateNewActivities(Model.WorkState workState) => CardModel.CreateNewActivities(workState);

    public void DeleteFromDB() => CardModel.DeleteFromDB();

    private void OnSwitchFrontBackRequested()
    {
        SwitchFrontBackBoardRequestedArgs arg = new SwitchFrontBackBoardRequestedArgs(this);
        EventAggregator<SwitchFrontBackBoardRequestedArgs>.Instance.Publish(this, arg);
    }

    private void OnDeleteCardRequested()
    {
        DeleteCardRequestedArgs arg = new DeleteCardRequestedArgs(this);
        EventAggregator<DeleteCardRequestedArgs>.Instance.Publish(this, arg);
    }
}