using Kanban.Repository;
using Kanban.Util;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Kanban.ViewModel
{
    public class DataBase
    {
        public DataBase(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }

    public class DropDBDialogViewModel : NotifyPropertyChangedBase
    {
        IDBClient m_DBClient;
        DelegateCommandNoArg m_DropDBCommand;

        public DropDBDialogViewModel(IDBClient dbClient)
        {
            m_DBClient = dbClient;
            m_DropDBCommand = new DelegateCommandNoArg(DropDB);

            DBList = new ObservableCollection<CheckedListItem<DataBase>>();
        }

        public void LoadDatabaseName()
        {
            List<string> databaseNames = m_DBClient.GetDatabaseNames();
            databaseNames.Remove(m_DBClient.DBName);

            DBList.Clear();
            foreach (string name in databaseNames)
            {
                DBList.Add(new CheckedListItem<DataBase>(new DataBase(name)));
            }

            base.OnPropertyChanged(nameof(DBList));
        }

        public ObservableCollection<CheckedListItem<DataBase>> DBList { get; private set; }

        public ICommand DropDBCommand => m_DropDBCommand;

        private async void DropDB()
        {
            int deleteCount = 0;
            List<string> databases = GetSelectedDatabases();

            foreach (string dbName in databases)
            {
                string msg = "Do you really drop the database(" + dbName + ")?";
                MessageBoxResult result = MessageBox.Show(
                                               msg,
                                               "Drop Database",
                                               MessageBoxButton.YesNo,
                                               MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await m_DBClient.MongoDBClient.DropDatabaseAsync(dbName);
                    RemoveFromCollection(dbName);
                    RaiseDatabaseDroppedEvent(dbName);
                    deleteCount++;
                }
            }

            MessageBox.Show(
                deleteCount.ToString() + " databases have been dropped.",
                "Drop Database result",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RaiseDatabaseDroppedEvent(string dbName)
        {
            DatabaseDroppedArg arg = new DatabaseDroppedArg(dbName);
            EventAggregator<DatabaseDroppedArg>.Instance.Publish(this, arg);
        }

        private List<string> GetSelectedDatabases()
        {
            List<string> databases = new List<string>();
            foreach (CheckedListItem<DataBase> db in DBList)
            {
                if (db.IsChecked) databases.Add(db.Item.Name);
            }

            return databases;
        }

        private void RemoveFromCollection(string dbName)
        {
            DBList.Remove(DBList.Single(s => s.Item.Name == dbName));
        }
    }
}
