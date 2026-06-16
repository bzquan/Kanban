using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Kanban
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Util.IAppSettings m_AppSettings;

        public MainWindow(BoardListPage boardListPage, Util.IAppSettings appSettings)
        {
            InitializeComponent();
            InitWindowSize(appSettings);

            m_AppSettings = appSettings;
            KanbanFrame.Content = boardListPage;
        }

        private void InitWindowSize(Util.IAppSettings appSettings)
        {
            Width = appSettings.MainWindowSize.Width;
            Height = appSettings.MainWindowSize.Height;

            if (appSettings.IsMainWindowStateMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            m_AppSettings.IsMainWindowStateMaximized = (WindowState == WindowState.Maximized);
            if (WindowState == WindowState.Normal)
            {
                m_AppSettings.MainWindowSize = new Size(Width, Height);
            }
            Properties.Settings.Default.Save();
            m_AppSettings.Save();
        }
    }
}
