using Kanban.Util;
using Kanban.ViewModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Kanban
{
    public delegate BoardPage BoardPageFactory(ViewModel.Board board);

    /// <summary>
    /// Interaction logic for BoardListPage.xaml
    /// </summary>
    public partial class BoardListPage : Page
    {
        BoardPageFactory m_BoardPageFactory;
        Func<SystemSetting> m_BackupPageFactory;
        BoardListViewModel m_BoardListViewModel;

        public BoardListPage(BoardPageFactory boardPageFactory, Func<SystemSetting> backupPageFactory, BoardListViewModel boardListViewModel, MetricsChartViewModel metricsChartViewModel)
        {
            InitializeComponent();

            m_BoardPageFactory = boardPageFactory;
            m_BackupPageFactory = backupPageFactory;
            m_BoardListViewModel = boardListViewModel;
            canvas4MetricsCharts.MetricsChartViewModel = metricsChartViewModel;

            m_BoardListViewModel.BoardSelecedEvent += canvas4MetricsCharts.OnBoardSeleced;
            m_BoardListViewModel.MetricChartTypeChangedEvent += canvas4MetricsCharts.OnMetricChartTypeChanged;
            m_BoardListViewModel.ProcessViewModel.CycleTimeDurationChanged += canvas4MetricsCharts.OnCycleTimeDurationChanged;

            scrollViewerOfCanvas.LayoutUpdated += ScrollViewerOfCanvas_LayoutUpdated;

            EventAggregator<GotoBoardPageRequestedArgs>.Instance.Event += OnGotoBoardPageRequested;
            EventAggregator<GotoSystemSettingPageRequestedArg>.Instance.Event += OnGotoSystemSettingPage;
        }

        private void OnDoubleClicked(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = e.Source as ListBoxItem;
            Board board = item.Content as Board;

            GotoBoardPage(board);
        }

        private void GotoBoardPage(Board board)
        {
            if (board == null) return;

            BoardPage page = m_BoardPageFactory(board);
            this.NavigationService.Navigate(page);
            //this.NavigationService.Navigate(m_BoardPageFactory.CreateBoardPage(board));
        }

        private void ScrollViewerOfCanvas_LayoutUpdated(object sender, EventArgs e)
        {
            canvas4MetricsCharts.OnScrollViewSizeChanged(scrollViewerOfCanvas.ActualWidth, scrollViewerOfCanvas.ActualHeight);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = m_BoardListViewModel;
            await m_BoardListViewModel.LoadBoards();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            canvas4MetricsCharts.OnScrollViewSizeChanged(scrollViewerOfCanvas.ActualWidth, scrollViewerOfCanvas.ActualHeight);
        }

        private void OnGotoBoardPageRequested(object sender, GotoBoardPageRequestedArgs arg)
        {
            GotoBoardPage(arg.Board);
        }

        private void OnGotoSystemSettingPage(object sender, GotoSystemSettingPageRequestedArg arg)
        {
            SystemSetting dbBackupPage = m_BackupPageFactory();
            this.NavigationService.Navigate(dbBackupPage);
        }

        private void OnHelp(object sender, RoutedEventArgs e)
        {
            AboutControlView about = new AboutControlView();
            AboutControlViewModel vm = (AboutControlViewModel)about.FindResource("ViewModel");

            vm.ApplicationLogo = Util.Util.DrawingImageFromResource("front_board.png");
            vm.Title = Properties.Resources.Main_Title;
            vm.Description = Properties.Resources.Help_KanbanDescription;
            vm.PublisherLogo = Util.Util.DrawingImageFromResource("FST-logo.png");
            vm.ReleaseNote = LoadReleaseNote();
            vm.Version = ExtractVersionNoFromReleaseNote(vm.ReleaseNote);

            vm.Window.Content = about;
            vm.Window.Show();
        }

        private string LoadReleaseNote()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var templateName = "Kanban.ReleaseNote.ReleaseNote.html";

            using (Stream stream = assembly.GetManifestResourceStream(templateName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private string ExtractVersionNoFromReleaseNote(string releaseNote)
        {
            // Example: <h5>Version 1.0.2 - 2017.03.18</h5>
            Regex versionRegex = new Regex(@"\s*<h5>\s*Version\s*(\w+\.\w+\.\w+).*</h5>");
            Match m = versionRegex.Match(releaseNote);
            if (m.Success)
            {
                return m.Groups[1].ToString();
            }
            return "Unknown Version";
        }
    }
}
