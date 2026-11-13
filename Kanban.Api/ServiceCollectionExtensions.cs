using Kanban.Infrastructure;
using Kanban.Repository;
using Kanban.Util;

namespace Kanban.Api;

public class KanbanApiOptions
{
    public string DBHost { get; set; } = "localhost:27017";
    public string DBName { get; set; } = "kanban";
    public bool CumulateCardsOnBackBoard { get; set; } = true;
    public int LimitNum4QueringLastProcess { get; set; } = 20;
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKanbanServices(this IServiceCollection services, IConfiguration config)
    {
        // appsettings の Kanban セクション(DB_HOST/DB_NAME キーはアンダースコア表記なので名前の一致に頼らず手動バインド)
        var options = new KanbanApiOptions
        {
            DBHost = config["Kanban:DB_HOST"] ?? "localhost:27017",
            DBName = config["Kanban:DB_NAME"] ?? "kanban",
            CumulateCardsOnBackBoard = bool.TryParse(config["Kanban:CumulateCardsOnBackBoard"], out var cumulate) ? cumulate : true,
            LimitNum4QueringLastProcess = int.TryParse(config["Kanban:LimitNum4QueringLastProcess"], out var limit) ? limit : 20,
        };
        services.AddSingleton(options);

        // DBCards.LimitNum4QueringLastProcess / BoardHistoryBuilder.CumulateCardsOnBackBoard は ConfigReader 経由のまま。
        // サーバー側はこの2値だけ ConfigOverrides に書き込む(呼び出し側コードは無変更)。WPFは Values 空のまま=従来動作。
        ConfigOverrides.Values["CumulateCardsOnBackBoard"] = options.CumulateCardsOnBackBoard.ToString().ToLowerInvariant();
        ConfigOverrides.Values["LimitNum4QueringLastProcess"] = options.LimitNum4QueringLastProcess.ToString();

        services.AddSingleton<IDBClient>(_ => new DBClient(options.DBHost, options.DBName));
        services.AddSingleton<IDBActivity>(sp => new DBActivities(sp.GetRequiredService<IDBClient>()));
        services.AddSingleton<IDBCard>(sp => new DBCards(sp.GetRequiredService<IDBClient>(), sp.GetRequiredService<IDBActivity>()));
        services.AddSingleton<IDBBoard>(sp => new DBBoards(sp.GetRequiredService<IDBClient>(), sp.GetRequiredService<IDBCard>(), sp.GetRequiredService<IDBActivity>()));
        services.AddSingleton(sp => new DBVersion(sp.GetRequiredService<IDBClient>()));
        services.AddSingleton(sp => new DBUpgrader(sp.GetRequiredService<DBVersion>(), sp.GetRequiredService<IDBCard>()));

        // Model.DevProcess.Localization は EN 固定結線。リクエストごとの言語は Task 4 の BoardCommandService が明示引数で受ける。
        var localization = new ServerLocalization();
        services.AddSingleton<ILocalization>(localization);

        services.AddSingleton<Kanban.Model.IBoardFactory>(sp => new BoardFactory(sp.GetRequiredService<IDBBoard>(), localization));
        services.AddSingleton<Kanban.Model.ICardFactory>(sp => new CardFactory(sp.GetRequiredService<IDBCard>(), sp.GetRequiredService<IDBActivity>(), localization));
        services.AddSingleton<Kanban.Model.IBoardRepository, BoardRepository>();
        services.AddSingleton<Kanban.Model.ICardRepository, CardRepository>();
        services.AddSingleton<Kanban.Model.IActivityRepository, ActivityRepository>();

        // Services(Task 4〜)
        services.AddSingleton<Kanban.Services.ILocalizationFactory, Kanban.Services.JsonLocalizationFactory>();
        services.AddSingleton<Kanban.Services.BoardCommandService>();

        return services;
    }
}
