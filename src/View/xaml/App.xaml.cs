using System.Windows;

namespace Kanban;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    DependencyInjector DependencyInjector { get; } = new DependencyInjector();

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

        app.RegisterDependencies();
        app.ConfigLocalizations();

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
        return $"images/kanban_splash_window{image_index}.jpg";
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
            var dbUpgrader = DependencyInjector.Resolve<Repository.DBUpgrater>();
            await dbUpgrader.UpgrateDB();

            var mainWindow = DependencyInjector.Resolve<MainWindow>();
            Application.Current.MainWindow = mainWindow;
            Application.Current.MainWindow.Show();

            TeardownDummyWindow(dummyWindow);
        }
        catch (Exception ex)
        {
            ShowExceptionMessages(dummyWindow, ex);
            dummyWindow.Close();
            Shutdown(1);
        }
    }

    internal void RegisterDependencies()
    {
        DependencyInjector.RegisterDependencies();
    }

    internal void ConfigLocalizations()
    {
        var appSetting = DependencyInjector.Resolve<Util.IAppSettings>();
        Util.Util.SetLanguage(appSetting.Language);
        Util.EnumUtil.CurrentLanguage = appSetting.Language;

        ViewModel.Card.ViewModelProperties = DependencyInjector.Resolve<ViewModel.IViewModelProperties>();
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
            if (!IsStartingUp) ShowExceptionMessages(Application.Current.MainWindow, e.Exception);
        }
    }

    void ShowExceptionMessages(Window owner, Exception ex)
    {
        var error_msg = $"{ex.Source} Error:  {ex.Message}\r\n\r\n{ex.StackTrace}";
        MessageBox.Show(owner, error_msg, "UnhandledException", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
    }
}
