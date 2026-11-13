using System.Net;
using Kanban.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Api.Tests.Endpoints;

/// <summary>
/// Task 12: SPA フォールバック(1プロセスデプロイ)。
/// wwwroot は gitignore(台帳 R2)のため fresh clone/CI では index.html が存在しない。
/// このフィクスチャは缺失時のみプレースホルダを自分でプロビジョンし teardown で削除する
/// (実ビルドの有無どちらでも成立 — 台帳 R10)。
/// 保証: ディープリンク(/boards/xyz)→ index.html、ただし /api/* ミスは決して HTML を返さない。
/// </summary>
[TestClass]
public class SpaFallbackTest
{
    private const string PlaceholderHtml =
        "<!doctype html><html lang=\"en\"><head><title>SPA fallback test placeholder</title></head><body>placeholder</body></html>";

    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;
    private string? _createdPlaceholder; // このフィクスチャが作ったファイルだけを削除する

    [TestInitialize]
    public void Setup()
    {
        TestDatabase.Reset();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient(); // ホスト起動(= ContentRootPath 確定)
        _createdPlaceholder = ProvisionSpaIndex(_app);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_createdPlaceholder is not null)
        {
            try { File.Delete(_createdPlaceholder); } catch (IOException) { }
            _createdPlaceholder = null;
        }
        _client.Dispose();
        _app.Dispose();
    }

    /// <summary>ホスト起動済みの factory から ContentRoot/wwwroot/index.html を確認し、
    /// 缺失時のみプレースホルダを作ってそのパスを返す(実ビルドがあれば触らず null)。</summary>
    private static string? ProvisionSpaIndex(WebApplicationFactory<Program> factory)
    {
        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        var wwwroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var index = Path.Combine(wwwroot, "index.html");
        if (File.Exists(index)) return null;
        Directory.CreateDirectory(wwwroot);
        File.WriteAllText(index, PlaceholderHtml);
        return index;
    }

    private static async Task AssertHtmlAsync(HttpResponseMessage resp)
    {
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(resp.Content.Headers.ContentType?.MediaType ?? "", "text/html");
        StringAssert.Contains((await resp.Content.ReadAsStringAsync()).ToLowerInvariant(), "<!doctype html");
    }

    private static async Task AssertNotHtmlAsync(HttpResponseMessage resp)
    {
        var mediaType = resp.Content.Headers.ContentType?.MediaType ?? "";
        Assert.DoesNotContain("html", mediaType, StringComparison.OrdinalIgnoreCase,
            $"Content-Type must never be html, was '{mediaType}'");
        var body = (await resp.Content.ReadAsStringAsync()).ToLowerInvariant();
        Assert.DoesNotContain("<!doctype html", body,
            $"body must never be index.html, was: {body[..Math.Min(120, body.Length)]}");
    }

    [TestMethod]
    public async Task Get_Root_ServesIndexHtml()
    {
        var resp = await _client.GetAsync("/");
        await AssertHtmlAsync(resp);
    }

    [TestMethod]
    public async Task Get_DeepLink_BoardsXyz_ServesIndexHtml()
    {
        var resp = await _client.GetAsync("/boards/xyz");
        await AssertHtmlAsync(resp);
    }

    // /api/* ミス: index.html フォールバックしてはならない(404 は 404 のまま、HTML混入なし)
    [TestMethod]
    public async Task Get_UnknownApiRoute_Returns404NotHtml()
    {
        var resp = await _client.GetAsync("/api/definitely-not-a-route");
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        await AssertNotHtmlAsync(resp);
    }

    // 仕様上の「不正id → 400 非HTML」保証。GET /api/boards/{id} ルートは存在しないため
    // GET /api/boards/notanid は unmatched → 404(いずれにせよ非HTMLであることが保証)。
    // (brief Step1 の 400 期待はルート実在前提の記述 — 実ルートの400は下の DELETE で検証)
    [TestMethod]
    public async Task Get_BoardsInvalidId_NeverHtml()
    {
        var resp = await _client.GetAsync("/api/boards/notanid");
        await AssertNotHtmlAsync(resp);
    }

    // 実ルート(BoardsEndpoints)の 400 経路がフォールバックに吞まれず JSON 400 のこと
    // (BoardsEndpointTest.Delete_InvalidId_Returns400 と同経路のフォールバック導入回帰)
    [TestMethod]
    public async Task Delete_BoardsInvalidId_Returns400NotHtml()
    {
        var resp = await _client.DeleteAsync("/api/boards/notanid");
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        await AssertNotHtmlAsync(resp);
    }

    // 台帳 R10 の回帰ガード: wwwroot/index.html が存在しない(fresh clone/CI — 台帳 R2 で gitignore)
    // 状態でフォールバックが Results.File の FileNotFoundException→500 ではなく 404 を返すこと。
    // ※ index.html を一時的に退避する(このクラスのみが wwwroot を触る・DoNotParallelize)。
    [TestMethod]
    public async Task Get_RootWithoutIndexHtml_Returns404Not500()
    {
        var env = _app.Services.GetRequiredService<IWebHostEnvironment>();
        var wwwroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var index = Path.Combine(wwwroot, "index.html");
        var backup = index + ".r10-backup";
        var moved = false;
        try
        {
            if (File.Exists(index)) { File.Move(index, backup, overwrite: true); moved = true; }
            Assert.IsFalse(File.Exists(index), "test precondition: index.html must be absent here");

            using var fresh = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
                b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
            });
            var resp = await fresh.CreateClient().GetAsync("/");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, "no index.html → 404, never 500 (R10)");
            await AssertNotHtmlAsync(resp);
        }
        finally
        {
            if (moved) File.Move(backup, index, overwrite: true);
        }
    }
}
