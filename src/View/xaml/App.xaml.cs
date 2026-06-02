using System.Windows;

namespace Kanban
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        DependencyInjector m_DependencyInjector = new DependencyInjector();

        /// <summary>
        /// Application Entry Point.
        /// 
        /// To add Main function in App class
        /// 1.Right-click App.xaml in the solution explorer
        /// 2.Select Properties Change 'Build Action' to 'Page'
        /// </summary>
        [STAThread]
        public static void Main()
        {
            ShowSplashScreen();

            Kanban.App app = new Kanban.App();
            app.InitializeComponent();
            app.Run();
        }

        private static void ShowSplashScreen()
        {
            var splashScreen = new SplashScreen(ImageName4SplashScreen());
            splashScreen.Show(true);
        }

        private static string ImageName4SplashScreen()
        {
            Random random = new Random();
            int image_index = random.Next(1, 7);
            return String.Format($"images/kanban_splash_window{image_index}.jpg");
        }

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            // WPF has an interesting bug (or "feature" as you want it):
            // YOU CAN`T SHOW ANY MESSAGE BOXES UNTIL YOU OPEN AT LEAST ONE WPF WINDOW.
            // As a workaround we need to open one hidden window at the beginning of application startup.
            // And if our application starts successfully, than we close this "helper" window.
            // WPF Bug Workaround: while we have no WPF window open we can`t show MessageBox.
            Window dummyWindow = SetupDummyWindow();

            try
            {
                m_DependencyInjector.RegisterDependencis();

                var dbUpgrader = m_DependencyInjector.Resolve<Repository.DBUpgrater>();
                await dbUpgrader.UpgrateDB();

                ConfigLocalizations();

                var mainWindow = m_DependencyInjector.Resolve<MainWindow>();
                Application.Current.MainWindow = mainWindow;
                Application.Current.MainWindow.Show();

                TeardownDummyWindow(dummyWindow);
            }
            catch (Exception ex)
            {
                ShowExceptionMessages(ex);
                dummyWindow.Close();
                Shutdown(1);
            }
        }

        private void ConfigLocalizations()
        {
            var appSetting = m_DependencyInjector.Resolve<Util.IAppSettings>();
            Util.Util.SetLanguage(appSetting.Language);
            Util.EnumUtil.CurrentLanguage = appSetting.Language;

            ViewModel.Card.ViewModelProperties = m_DependencyInjector.Resolve<ViewModel.IViewModelProperties>();
        }

        bool IsStartingUp { get; set; } = true;

        Window DummyWindow => new Window()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            WindowStyle = WindowStyle.None,
            Top = 0,
            Left = 0,
            Width = 1,
            Height = 1,
            ShowInTaskbar = false
        };

        Window SetupDummyWindow()
        {
            Window dummyWindow = DummyWindow;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            dummyWindow.Show();

            return dummyWindow;
        }

        void TeardownDummyWindow(Window dummyWindow)
        {
            IsStartingUp = false;
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            dummyWindow.Close();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                if (!IsStartingUp) ShowExceptionMessages(e.Exception);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
        }

        void ShowExceptionMessages(Exception ex)
        {
            string error_msg = String.Format
                ("{0} Error:  {1}\r\n\r\n{2}",
                   ex.Source, ex.Message, ex.StackTrace);
            MessageBox.Show(error_msg, "UnhandledException", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
