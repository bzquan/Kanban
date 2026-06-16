using Kanban.Util;
using Kanban.ViewModel;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kanban
{
    /// <summary>
    /// Interaction logic for DBDumpRestorePage.xaml
    /// </summary>
    public partial class SystemSetting : Page, Model.IProcessExecutorClient
    {
        Repository.IDBClient m_DBClient;
        private SystemSettingViewModel m_DBDumpRestoreViewModel;
        private DropDBDialogViewModel m_DropDBDialogViewModel;

        public SystemSetting(Repository.IDBClient dbClient, SystemSettingViewModel viewModel, DropDBDialogViewModel dropDBDialogViewModel)
        {
            InitializeComponent();
            EnableDropDBButton();

            m_DropDBDialogViewModel = dropDBDialogViewModel;
            m_DBClient = dbClient;
            SetupViewModel(viewModel);

            this.DataContext = m_DBDumpRestoreViewModel;
        }

        private void EnableDropDBButton()
        {
            dropDBButton.IsEnabled = ConfigReader.GetValue("CanDropDatabase", false);
        }

        private void SetupViewModel(SystemSettingViewModel viewModel)
        {
            Util.EnumUtil.InitComboBoxByEnum<Util.Languages>(languagesComboBox);

            m_DBDumpRestoreViewModel = viewModel;
            m_DBDumpRestoreViewModel.ProcessExecutorClient = this;
        }

        private void OnSelectBackupFolder(object sender, RoutedEventArgs e)
        {
            // Create the dialog instance
            var folderBrowserDialog = new OpenFolderDialog
            {
                Title = "Select backup folder",
                InitialDirectory = m_DBDumpRestoreViewModel.DBBackupFolder,
                Multiselect = false // Set to true if you want users to pick multiple folders
            };

            if (folderBrowserDialog.ShowDialog() == true)
            {
                m_DBDumpRestoreViewModel.DBBackupFolder = folderBrowserDialog.FolderName;
            }
        }

        /// Implementation of IProcessExecutorClient
        public AutoResetEvent AutoResetEvent { get; private set; } = new AutoResetEvent(false);

        public void OnStarted()
        {
            Application.Current.MainWindow.Cursor = Cursors.Wait;
            InfoTextBox.Clear();
            terminateProcess.IsEnabled = true;
            terminateProcess.Content = FindResource("Stop");
        }

        public void OnMessage(string message)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                InfoTextBox.AppendText(message + "\n");
            });
        }

        public void OnCompleted()
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                ProcessCompleted();
            });
        }

        private void ProcessCompleted()
        {
            Application.Current.MainWindow.Cursor = Cursors.Arrow;
            terminateProcess.IsEnabled = false;
            terminateProcess.Content = FindResource("Stop_inactive");

            m_DBClient.UpgradeCollections();
        }

        public event EventHandler TerminateRequested = delegate { };

        private void OnStopProcess(object sender, RoutedEventArgs e)
        {
            TerminateRequested(this, EventArgs.Empty);
        }

        private void OnDropDatabase(object sender, RoutedEventArgs e)
        {
            DropDBDialog dialog = new DropDBDialog(m_DropDBDialogViewModel);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowDialog();
        }
    }
}
