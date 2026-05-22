using Kanban.Model;
using Kanban.Repository;
using Kanban.Util;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Kanban.ViewModel
{
    public class SystemSettingViewModel : NotifyPropertyChangedBase
    {
        DBBackupFactory m_DBBackupFactory;
        ObservableCollection<string> m_DatabaseNames = new ObservableCollection<string>();

        IAppSettings m_AppSettings;
        IDBClient m_DBClient;
        string m_CurrentDBName;
        bool? m_HaveDBBackupTools;

        IDBBackup m_DBBackup;
        DelegateCommandNoArg m_DumpDBCommand;
        DelegateCommandNoArg m_RestoreDBCommand;

        public SystemSettingViewModel(IAppSettings appSettings, IDBClient dbClient, DBBackupFactory dbBackupFactory)
        {
            m_AppSettings = appSettings;
            m_DBBackupFactory = dbBackupFactory;
            m_DBClient = dbClient;
            LoadDatabaseNames();
            m_CurrentDBName = dbClient.DBName;

            m_DumpDBCommand = new DelegateCommandNoArg(DumpDB, CanDumpDB);
            m_RestoreDBCommand = new DelegateCommandNoArg(RestoreDB, CanRestoreDB);

            EventAggregator<DatabaseDroppedArg>.Instance.Event += OnDatabaseDropped;
        }

        public ICommand DumpDBCommand => m_DumpDBCommand;
        public ICommand RestoreDBCommand => m_RestoreDBCommand;

        private void LoadDatabaseNames()
        {
            List<string> dbNames = m_DBClient.GetDatabaseNames();
            m_DatabaseNames.Clear();
            dbNames.ForEach(x => m_DatabaseNames.Add(x));
        }

        public IProcessExecutorClient ProcessExecutorClient
        {
            set
            {
                m_DBBackup = m_DBBackupFactory(value);
            }
        }

        public string CurrentLanguage
        {
            get
            {
                return EnumUtil.GetEnumDescription(m_AppSettings.Language);
            }
            set
            {
                Languages language = EnumUtil.ToEnumValue<Languages>(value);
                if (m_AppSettings.Language != language)
                {
                    m_AppSettings.Language = language;
                    Util.Util.SetLanguage(m_AppSettings.Language);

                    MessageBoxResult result = MessageBox.Show(
                                                   "You need to restart the application to make new language effective",
                                                   "Change language",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        RestartApplication();
                    }
                }
            }
        }

        private static void RestartApplication()
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void OnDatabaseDropped(object sender, DatabaseDroppedArg arg)
        {
            m_DatabaseNames.Remove(arg.DatabaseName);
        }

        public ObservableCollection<string> DatabaseNames => m_DatabaseNames;

        public string CurrentDatabase
        {
            get { return m_CurrentDBName; }
            set
            {
                if ((value != null) && (m_CurrentDBName != value))
                {
                    MessageBoxResult result = MessageBox.Show(
                                                   "You need to restart the application to make new databse effective",
                                                   "Change database",
                                                   MessageBoxButton.YesNo,
                                                   MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        m_AppSettings.Database = value;
                        RestartApplication();
                    }
                }
                base.OnPropertyChanged();
            }
        }

        public string DBBackupFolder
        {
            get { return m_AppSettings.DBBackupFolder; }
            set
            {
                m_AppSettings.DBBackupFolder = value.Trim();
                base.OnPropertyChanged();
                m_DumpDBCommand.RaiseCanExecuteChanged();
                m_RestoreDBCommand.RaiseCanExecuteChanged();
            }
        }

        public DBPriority4Restore DBPriority4Restore
        {
            get { return m_AppSettings.DBPriority4Restore; }
            set
            {
                m_AppSettings.DBPriority4Restore = value;
                base.OnPropertyChanged();
            }
        }

        public string DBPriority4Restore_NoPriority => EnumUtil.GetEnumDescription(DBPriority4Restore.NoPriority);
        public string DBPriority4Restore_CurrentDB => EnumUtil.GetEnumDescription(DBPriority4Restore.CurrentDB);
        public string DBPriority4Restore_ToBeRestoredDB => EnumUtil.GetEnumDescription(DBPriority4Restore.ToBeRestoredDB);

        private bool CanDumpDB()
        {
            return HaveDBBackupTools() && IsDBBackupFolderValid();
        }

        private bool CanRestoreDB()
        {
            return HaveDBBackupTools() && IsDBRestoreFolderExist();
        }

        private bool HaveDBBackupTools()
        {
            m_HaveDBBackupTools = m_HaveDBBackupTools ?? m_DBBackup.HaveDBBackupTools();
            return m_HaveDBBackupTools.Value;
        }

        private bool IsDBBackupFolderValid() => (DBBackupFolder?.Length > 0);

        private bool IsDBRestoreFolderExist() => Directory.Exists(DBBackupFolder);

        private void DumpDB() => m_DBBackup.DumpDB(DBBackupFolder);

        private void RestoreDB() => m_DBBackup.RestoreDB(DBBackupFolder, DBPriority4Restore);
    }
}
