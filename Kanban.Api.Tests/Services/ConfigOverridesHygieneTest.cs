using System.Net;
using Kanban.Api;
using Kanban.Util;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kanban.Api.Tests.Services;

/// <summary>
/// ConfigOverrides.Values はプロセス全体共有の静的辞書。WebApplicationFactory のホスト起動が
/// AddKanbanServices 経由で書き込む値が、ホストを起動しない後続テスト(Service系)へ漏れると
/// DBCards/BoardHistoryBuilder の挙動がテスト順に依存して静かに変わる。
/// TestDatabase.Reset() が起動残骸を必ず空にすること(最終レビュー指摘)。
/// </summary>
[TestClass]
public class ConfigOverridesHygieneTest
{
    [TestMethod]
    public async Task Reset_ClearsConfigOverridesLeftByHostBoot()
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
            b.UseSetting("Kanban:LimitNum4QueringLastProcess", "3");
        });
        var resp = await app.CreateClient().GetAsync("/api/health");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        // 前提: ホスト起動が overrides を書き込んでいること
        Assert.IsTrue(ConfigOverrides.Values.ContainsKey("LimitNum4QueringLastProcess"),
                      "precondition: host boot must have written the override");

        TestDatabase.Reset();

        Assert.HasCount(0, ConfigOverrides.Values,
                        "TestDatabase.Reset must clear process-global ConfigOverrides (order-independence)");
    }
}
