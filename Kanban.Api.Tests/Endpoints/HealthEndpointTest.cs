using System.Net;
using Kanban.Api;
using Kanban.Util;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Kanban.Api.Tests.Endpoints;

[TestClass]
public class HealthEndpointTest
{
    // テストは本番DBに触れないよう Kanban:DB_HOST/DB_NAME を kanban_test へ差し替えて起動する
    private static WebApplicationFactory<Program> CreateApp(Action<IWebHostBuilder>? extra = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
            extra?.Invoke(b);
        });
    }

    [TestMethod]
    public async Task GetHealthReturns200()
    {
        using var app = CreateApp();
        var resp = await app.CreateClient().GetAsync("/api/health");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    // appsettings の2フラグが AddKanbanServices → ConfigOverrides 経由で ConfigReader に上書きされること
    // (DBCards.LimitNum4QueringLastProcess / BoardHistoryBuilder.CumulateCardsOnBackBoard は ConfigReader 経由のまま)
    [TestMethod]
    public async Task AppsettingsFlagsOverrideConfigReader()
    {
        using var app = CreateApp(b =>
        {
            b.UseSetting("Kanban:LimitNum4QueringLastProcess", "3");
            b.UseSetting("Kanban:CumulateCardsOnBackBoard", "false");
        });
        // ホスト起動(=AddKanbanServices実行)を確実にする
        var resp = await app.CreateClient().GetAsync("/api/health");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        Assert.AreEqual(3, ConfigReader.GetValue("LimitNum4QueringLastProcess", 20));
        Assert.IsFalse(ConfigReader.GetValue("CumulateCardsOnBackBoard", true));
    }
}
