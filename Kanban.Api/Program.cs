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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapBoardsEndpoints();
app.MapCardsEndpoints();
app.MapMetricsEndpoints();
app.MapOpenApi();

app.Run();

public partial class Program { }
