using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

using Kanban.ViewModel;
using System.Threading;
using System.Threading.Tasks;
using Kanban.Util;

namespace Kanban
{
    /// <summary>
    /// Interaction logic for BoardPage.xaml
    /// </summary>
    public partial class BoardPage : Page
    {
        static Thickness s_BorderThickness = new Thickness(2);
        double m_CurrentZoomRate = 1.0;

        MainWindow MainWindow { get; }
        Board m_Board;
        List<ProcessStepView> m_ProcessStepView = new List<ProcessStepView>();
        BoardPageViewModel m_BoardPageViewModel;
        IAppSettings m_AppSettings;
        IViewModelProperties m_Properties;
        Model.IActivityRepository m_ActivityRepository;

        public BoardPage(MainWindow mainWindow, Board board, BoardPageViewModel boardPageViewModel, IAppSettings appSettings, IViewModelProperties properties, Model.IActivityRepository activityRepository)
        {
            InitializeComponent();

            MainWindow = mainWindow;
            m_Board = board;
            m_AppSettings = appSettings;
            m_Properties = properties;
            m_ActivityRepository = activityRepository;

            m_BoardPageViewModel = boardPageViewModel;
            boardPageViewModel.Initialize(board);

            boardPage.SizeChanged += OnBoardSizeChanged;
            this.SizeChanged += OnPageSizeChanged;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            InitializeProcessSteps();
            DataContext = m_BoardPageViewModel;
            m_BoardPageViewModel.LoadContents(cardFilter: "");
        }

        private void InitializeProcessSteps()
        {
            ClearBoardPageGrid();
            InitializeBoardProcessStepViews();
        }

        private void ClearBoardPageGrid()
        {
            boardPage.ColumnDefinitions.Clear();
            boardPage.Children.Clear();
        }

        private void InitializeBoardProcessStepViews()
        {
            foreach (Repository.ProcessStep step in m_Board.ProcessSteps)
            {
                AddGridColumnDefinition(step.PhaseSeqNo);
                ProcessStepView processStepView = CreateProcessStepView(step.PhaseSeqNo);
                m_ProcessStepView.Add(processStepView);
                processStepView.Content = m_BoardPageViewModel.GetProcessStepViewModel(step.PhaseSeqNo);
            }
        }

        private void AddGridColumnDefinition(int column)
        {
            ColumnDefinition gridCol = new ColumnDefinition();
            boardPage.ColumnDefinitions.Add(gridCol);

            Border border = new Border { BorderThickness = s_BorderThickness, BorderBrush = Brushes.Blue };
            Grid.SetColumn(border, column);

            boardPage.Children.Add(border);
        }

        private ProcessStepView CreateProcessStepView(int column)
        {
            string processStepDataTemplateName = (column == 0) ? "firstProcessStepDataTemplate"
                                                               : "processStepDataTemplate";
            
            ProcessStepView processStepView = new ProcessStepView(this, processStepDataTemplateName);
            Grid.SetColumn(processStepView, column);
            boardPage.Children.Add(processStepView);

            return processStepView;
        }

        private void OnDoubleClicked(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = e.Source as ListBoxItem;
            Card card = item.Content as Card;
            e.Handled = true;
            ShowEditCardDialog(card);
        }

        void OnEditCard(object sender, RoutedEventArgs e)
        {
            Button cmd = (Button)sender;
            Card card = cmd.Tag as Card;
            ShowEditCardDialog(card);
        }

        private void ShowEditCardDialog(Card card)
        {
            if (card == null) return;

            EditCardDialog editCardDialog = new EditCardDialog(m_Board, card, m_Properties, m_ActivityRepository);
            editCardDialog.Owner = MainWindow;
            editCardDialog.ShowDialog();
        }

        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) => UpdateZoomRate();

        private void OnBoardSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                UpdateZoomRate();
            }
        }

        private void OnZoomButtonClicked(object sender, RoutedEventArgs e)
        {
            IsZoomed = !IsZoomed;
            zoomButton.Content = FindResource(GetZoomButtonResource());
            UpdateZoomRate();
        }

        private void UpdateZoomRate()
        {
            m_CurrentZoomRate = CalcZoomRate();
            AdjustBoardHeightToFillMainWindow(m_CurrentZoomRate);

            boardPage.RenderTransform = new ScaleTransform(m_CurrentZoomRate, m_CurrentZoomRate);
            zoomButton.Content = FindResource(GetZoomButtonResource());
            if (IsZoomed)
            {
                boardPageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                boardPageScrollViewer.ScrollToHorizontalOffset(0);
            }
            else
            {
                boardPageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
        }

        private void AdjustBoardHeightToFillMainWindow(double zoomRate)
        {
            double heightDiff = MainWindow.ActualHeight - this.ActualHeight;
            boardPageScrollViewer.Height = (this.ActualHeight - heightDiff) / zoomRate;
        }

        private double CalcZoomRate()
        {
            return IsZoomed ? boardPageScrollViewer.RenderSize.Width / boardPage.ActualWidth : 1.0;
        }

        private bool IsZoomed
        {
            get { return m_AppSettings.IsBoardZoomed; }
            set { m_AppSettings.IsBoardZoomed = value; }
        }

        /// <summary>
        /// 拡大された時、縮小アイコン(ZoomIn)を、縮小された時、拡大アイコン(ZoomOut)を設定する
        /// </summary>
        /// <returns></returns>
        private string GetZoomButtonResource() => (IsZoomed ? "ZoomIn" : "ZoomOut");
    }
}
