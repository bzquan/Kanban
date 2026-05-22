using Kanban.Util;
using MongoDB.Driver;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Friend1a")]
namespace Kanban.Repository
{
    public class DBClient : IDBClient
    {
        MongoClient m_MongoClient;
        IAppSettings m_AppSettings;

        public DBClient(IAppSettings setting)
        {
            m_AppSettings = setting;
            DBHost = ConfigReader.GetValue("DB_HOST", "localhost:27017");
            DBName = GetCurrentDBName(setting.Database);
            UpgradeCollections();
        }

        // for test only
        public DBClient()
        {
        }

        public MongoClient MongoDBClient
        {
            get
            {
                m_MongoClient = m_MongoClient ?? new MongoClient(ConnectString);
                return m_MongoClient;
            }
        }

        public string DBHost { get; private set; } = "localhost:27017";
        public string DBName { get; set; } = "kanban";
        public IMongoDatabase DB => MongoDBClient.GetDatabase(DBName);

        private string ConnectString => "mongodb://" + DBHost;

        public List<string> GetDatabaseNames()
        {
            List<string> db_names = new List<string>();
            using (var cursor = MongoDBClient.ListDatabases(default(CancellationToken)))
            {
                foreach (var item in cursor.ToEnumerable())
                {
                    string name = item.GetValue("name").ToString();
                    if (name != "local") db_names.Add(name);
                }
            }

            return db_names;
        }

        string GetCurrentDBName(string dbNameByUserSetting)
        {
            string dbNameByConfig = ConfigReader.GetValue("DB_NAME", "kanban");
            if (IsNewDB(dbNameByConfig))
            {
                m_AppSettings.Database = dbNameByConfig;
                return dbNameByConfig;
            }
            else
            {
                return dbNameByUserSetting;
            }
        }

        private bool IsNewDB(string dbNameByConfig)
        {
            List<string> dbNames = GetDatabaseNames();
            return !dbNames.Contains(dbNameByConfig);
        }

        /// <summary>
        /// We need upgrade schema if some names of collection field are changed
        /// when using the NEW version of Kanban application.
        /// </summary>
        public void UpgradeCollections()
        {
            //UpdateCardField(oldName: "IsArchived", newName: "IsOnBackBoard");
            //UpdateCardField(oldName: "ArchivedDate", newName: "OnBackBoardDate");
        }

        private void UpdateCardField(string oldName, string newName)
        {
            var filter = Builders<Card>.Filter.Empty;
            var update = Builders<Card>.Update.Rename(field: oldName, newName: newName);
            CardsCollection.UpdateMany(filter, update);
        }

        private IMongoCollection<Card> CardsCollection => DB.GetCollection<Card>(CollectionNames.CardsCollectionName);
    }
}
