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
app.UseStaticFiles();

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
    context.Request.Path.StartsWithSegments("/api")
        ? Results.NotFound()
        : File.Exists(spaIndexPath)
            ? Results.File(spaIndexPath, "text/html")
            : Results.NotFound();
app.MapFallback(spaFallback);

app.Run();

public partial class Program { }
