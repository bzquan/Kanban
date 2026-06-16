using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Kanban.Repository
{
    public interface IDBClient
    {
        MongoClient MongoDBClient { get; }
        string DBHost { get; }
        string DBName { get; set; }
        IMongoDatabase DB { get; }
        List<string> GetDatabaseNames();
        void UpgradeCollections();
    }
}
