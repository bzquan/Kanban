using Kanban.Api;
using Kanban.Api.Endpoints;
using Kanban.Repository;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKanbanServices(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// 起動時初期化(テストでは UseSetting で DB_NAME=kanban_test に差し替わるため本番DBに触れない)
await app.Services.GetRequiredService<IDBActivity>().EnsureIndexesAsync();
await app.Services.GetRequiredService<DBUpgrader>().UpgradeDB();

// 静的プロパティ結線(既存WPF DependencyInjector.DI4Model と同じ内容・起動時1回だけ)
Kanban.Model.Board.BoardRepository = app.Services.GetRequiredService<Kanban.Model.IBoardRepository>();
Kanban.Model.Card.CardRepository = app.Services.GetRequiredService<Kanban.Model.ICardRepository>();
Kanban.Model.Activity.ActivityRepository = app.Services.GetRequiredService<Kanban.Model.IActivityRepository>();
Kanban.Model.DevProcess.Localization = app.Services.GetRequiredService<Kanban.Util.ILocalization>();

// SPA同梱(1プロセスデプロイ): vite build の出力先 wwwroot(Task 1, gitignore=台帳R2)。
// /assets/* 等の静的ファイルと、一致なしのディープリンクを index.html へ返す。
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // index.html をブラウザにキャッシュさせない(no-store)。
    // hashed asset への参照を含む index を cache されると、再ビルド後に旧 index が
    // 消えた旧 JS を読みに行ってホワイトアウトする(実障害 2026-11-17)。
    // 実体は常に再取得だが index.html は小さく、assets/* はハッシュ名で安全に cache 可能。
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-store";
        }
    },
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapBoardsEndpoints();
app.MapCardsEndpoints();
app.MapMetricsEndpoints();
app.MapOpenApi();

// 単一フォールバック(台帳 R10):
//  - /api/* の未一致は決して HTML を返さない(404/400 の JSON 意味論を維持)
//  - wwwroot は gitignore のため存在しないクローン/CI がある → Results.File の FileNotFound→500 を
//    避けますリクエスト時に File.Exists でガードし 404 を返す
//  - パスは ContentRootPath 基準(dotnet run の起動CWDに依存しない)
var spaIndexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
Func<HttpContext, IResult> spaFallback = context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
        return Results.NotFound();
    if (!File.Exists(spaIndexPath))
        return Results.NotFound();
    // 静的パスと同じく index は常に再取得(no-store — 再デプロイ白画面対策)
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(spaIndexPath, "text/html");
};
app.MapFallback(spaFallback);

app.Run();

public partial class Program { }
