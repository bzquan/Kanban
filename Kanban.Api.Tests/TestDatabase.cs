using Kanban.Repository;
using MongoDB.Driver;

namespace Kanban.Api.Tests;

/// <summary>
/// 自動テスト専用DB kanban_test への接続フィクスチャ。
/// 接続・DropしてよいDBは kanban_test のみ(設計書§4.7・計画 Global Constraints)。
/// </summary>
public static class TestDatabase
{
    public const string TestDBName = "kanban_test";

    public static string DBHost => Environment.GetEnvironmentVariable("KANBAN_DB_HOST") ?? "localhost:27017";
    public static string DBName => TestDBName;

    public static DBClient NewClient() => new DBClient(DBHost, DBName);

    /// <summary>kanban_test を Drop して作り直す(Drop対象は kanban_test のみ)。</summary>
    public static void Reset()
    {
        var mongo = new MongoClient("mongodb://" + DBHost);
        mongo.DropDatabase(DBName);
        var activities = new DBActivities(NewClient());
        activities.EnsureIndexesAsync().GetAwaiter().GetResult();
    }
}
