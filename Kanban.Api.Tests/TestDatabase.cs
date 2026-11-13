using Kanban.Infrastructure;
using Kanban.Repository;
using Kanban.Services;
using MongoDB.Bson;
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

    // ---- Model 静的結線 / サービス構築ヘルパー(Task 4) ----
    // 既存 Model 層の静的プロパティ(Board.Card 等)を kanban_test 向け実装に差し替える。

    private static DBActivities? s_activities;
    private static DBCards? s_cards;
    private static DBBoards? s_boards;
    private static BoardRepository? s_boardRepository;
    private static CardRepository? s_cardRepository;
    private static ActivityRepository? s_activityRepository;
    private static readonly JsonLocalizationFactory s_localizationFactory = new();

    public static void WireModel(DBClient client)
    {
        s_activities = new DBActivities(client);
        s_cards = new DBCards(client, s_activities);
        s_boards = new DBBoards(client, s_cards, s_activities);
        s_boardRepository = new BoardRepository(s_boards, new BoardFactory(s_boards, English()), s_activities);
        s_cardRepository = new CardRepository(s_cards);
        s_activityRepository = new ActivityRepository(s_activities);

        Kanban.Model.Board.BoardRepository = s_boardRepository;
        Kanban.Model.Card.CardRepository = s_cardRepository;
        Kanban.Model.Activity.ActivityRepository = s_activityRepository;
        Kanban.Model.DevProcess.Localization = English();
    }

    public static BoardCommandService NewBoardCommandService()
    {
        EnsureWired();
        return new BoardCommandService(
            new BoardFactory(s_boards!, English()),
            s_boardRepository!,
            s_boards!,
            s_cardRepository!,
            s_localizationFactory);
    }

    public static CardCommandService NewCardCommandService()
    {
        EnsureWired();
        return new CardCommandService(
            new CardFactory(s_cards!, s_activities!, English()),
            s_cardRepository!,
            s_activityRepository!,
            s_cards!,
            s_boardRepository!);
    }

    /// <summary>列(前面)カードを SeqNo 圧込みで取得する(Task 6 の SeqNo 不変条件アサート用)。</summary>
    public static async Task<List<Kanban.Model.Card>> GetColumnCards(Kanban.Model.Board board, int step, bool wip)
    {
        EnsureWired();
        return await s_cardRepository!.GetCards(
            board,
            new Kanban.Model.WorkState { ProcessStepSeqNo = step, IsWIP = wip },
            "",
            Kanban.Repository.OnBoardFilter.All);
    }

    /// <summary>カードの Activities を StateChangedDate 昇順で返す(Task 6/7 の監査証跡アサート用)。</summary>
    public static async Task<List<Kanban.Repository.Activity>> GetActivities(ObjectId cardId)
    {
        EnsureWired();
        return await s_activities!.GetActivitiesOf(new List<ObjectId> { cardId });
    }

    private static Kanban.Util.ILocalization English() => s_localizationFactory.Create(Kanban.Util.Languages.English);

    private static void EnsureWired()
    {
        if (s_boards is null)
            throw new InvalidOperationException("TestDatabase.WireModel must be called before using this helper");
    }
}
