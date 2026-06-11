using Kanban.Model;
using Kanban.Repository;
using Kanban.Util;
using Microsoft.Win32;
using MongoDB.Driver.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Kanban.ViewModel;

public class SystemSettingViewModel : NotifyPropertyChangedBase
{
    private DBBackupFactory DBBackupFactory { get; }
    private ObservableCollection<string> m_DatabaseNames = new ObservableCollection<string>();

    private IAppSettings AppSettings { get; }
    private IDBClient DBClient { get; }
    private string m_CurrentDBName;
    private bool? m_HaveDBBackupTools;
    private IDBBackup DBBackup { get; set; }

    private DelegateCommandNoArg m_DumpDBCommand;
    private DelegateCommandNoArg m_RestoreDBCommand;

    public SystemSettingViewModel(IAppSettings appSettings, IDBClient dbClient, DBBackupFactory dbBackupFactory)
    {
        AppSettings = appSettings;
        DBBackupFactory = dbBackupFactory;
        DBClient = dbClient;
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
        var dbNames = DBClient.GetDatabaseNames()
            .Where(dbName => dbName != ("admin") && dbName != ("config"))
            .ToList();

        m_DatabaseNames.Clear();
        dbNames.ForEach(x => m_DatabaseNames.Add(x));
    }

    public IProcessExecutorClient ProcessExecutorClient
    {
        set
        {
            DBBackup = DBBackupFactory(value);
        }
    }

    public string CurrentLanguage
    {
        get => EnumUtil.GetEnumDescription(AppSettings.Language);
        set
        {
            Languages language = EnumUtil.ToEnumValue<Languages>(value);
            if (AppSettings.Language != language)
            {
                AppSettings.Language = language;
                Util.Util.SetLanguage(AppSettings.Language);

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
            assemblyPath = assemblyPath.Replace(".dll", ".exe", StringComparison.OrdinalIgnoreCase);
        }

        return new ProcessStartInfo(assemblyPath)
        {
            UseShellExecute = true,
            Arguments = arguments,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };
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
                    AppSettings.Database = value;
                    RestartApplication();
                }
            }
            base.OnPropertyChanged();
        }
    }

    public string DBBackupFolder
    {
        get => AppSettings.DBBackupFolder;
        set
        {
            AppSettings.DBBackupFolder = value.Trim();
            base.OnPropertyChanged();
            m_DumpDBCommand.RaiseCanExecuteChanged();
            m_RestoreDBCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanDumpDB() => HaveDBBackupTools() && IsDBBackupFolderValid();

    private bool CanRestoreDB() => HaveDBBackupTools();

    private bool HaveDBBackupTools()
    {
        m_HaveDBBackupTools = m_HaveDBBackupTools ?? DBBackup.HaveDBBackupTools();
        return m_HaveDBBackupTools.Value;
    }

    private bool IsDBBackupFolderValid() => (DBBackupFolder?.Length > 0);

    private void DumpDB() => DBBackup.DumpDB(DBBackupFolder);

    private void RestoreDB()
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select the folder where Kanban database backed up",
            InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            Multiselect = false
        };

        if (folderDialog.ShowDialog() == true)
        {
            string selectedFolder = folderDialog.FolderName;

            DBBackupFolder = folderDialog.FolderName;
            DBBackup.RestoreDB(DBBackupFolder);
        }
    }
}
