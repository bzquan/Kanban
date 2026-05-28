using Kanban.Model;
using Kanban.Repository;
using Kanban.Util;
using MongoDB.Driver.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Kanban.ViewModel;

public class SystemSettingViewModel : NotifyPropertyChangedBase
{
    private DBBackupFactory m_DBBackupFactory;
    private ObservableCollection<string> m_DatabaseNames = new ObservableCollection<string>();

    private IAppSettings m_AppSettings;
    private IDBClient m_DBClient;
    private string m_CurrentDBName;
    private bool? m_HaveDBBackupTools;

    private IDBBackup m_DBBackup;
    private DelegateCommandNoArg m_DumpDBCommand;
    private DelegateCommandNoArg m_RestoreDBCommand;

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
        var dbNames = m_DBClient.GetDatabaseNames()
            .Where(dbName => dbName != ("admin") && dbName != ("config"))
            .ToList();

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
        try
        {
            var processStartInfo = MakeProcessStartInfo();
            Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to restart the Kanban: {ex.Message}";
            MessageBox.Show(errorMessage,
                            "Change language",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private static ProcessStartInfo MakeProcessStartInfo()
    {
        string assemblyPath = Application.ResourceAssembly.Location;
        string[] cmdArgs = Environment.GetCommandLineArgs();
        string arguments = string.Join(" ", cmdArgs.Skip(1).Select(a => $"\"{a}\""));

        if (assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo("dotnet", $"\"{assemblyPath}\" {arguments}")
            {
                UseShellExecute = false,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
        }
        else
        {
            return new ProcessStartInfo(assemblyPath)
            {
                UseShellExecute = true,
                Arguments = arguments,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
        }

    }

    private void OnDatabaseDropped(object sender, DatabaseDroppedArg arg)
    {
        m_DatabaseNames.Remove(arg.DatabaseName);
    }

    public ObservableCollection<string> DatabaseNames => m_DatabaseNames;

    public string CurrentDatabase
    {
        get => m_CurrentDBName;
        set
        {
            if ((value != null) && (m_CurrentDBName != value))
            {
                MessageBoxResult result = MessageBox.Show(
                                               "You need to restart the application to make new database effective",
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
