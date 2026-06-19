using MongoDB.Driver;

namespace Kanban.Repository;

public interface IDBClient
{
    MongoClient MongoDBClient { get; }
    string DBHost { get; }
    string DBName { get; set; }
    IMongoDatabase DB { get; }
    List<string> GetDatabaseNames();
    void UpgradeCollections();
}
