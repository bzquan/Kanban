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
    private string? _createdPlaceholder;   // このフィクスチャが作ったファイルだけを削除する
    private string? _createdWwwrootDir;    // 同上、ディレクトリ(缺失時に作った場合のみ)

    [TestInitialize]
    public void Setup()
    {
        TestDatabase.Reset();
        // ホスト起動「前」に wwwroot を用意する: StaticFileMiddleware は起動時に
        // PhysicalFileProvider を実体化するため、wwwroot ディレクトリ缺失だと
        // CreateClient()(ホスト起動)が DirectoryNotFoundException で落ちる(レビュー指摘・実証済)。
        ProvisionSpaBeforeHost();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Setup が途中で失敗した場合(_client/_app 未代入)でも NRE にしない
        if (_createdPlaceholder is not null)
        {
            try { File.Delete(_createdPlaceholder); } catch (IOException) { }
            _createdPlaceholder = null;
        }
        if (_createdWwwrootDir is not null)
        {
            try
            {
                if (Directory.Exists(_createdWwwrootDir) && Directory.GetFileSystemEntries(_createdWwwrootDir).Length == 0)
                    Directory.Delete(_createdWwwrootDir);
            }
            catch (IOException) { }
            _createdWwwrootDir = null;
        }
        if (_client is not null) _client.Dispose();
        if (_app is not null) _app.Dispose();
    }

    /// <summary>ホスト起動前に、ホストと同じになるディレクトリ(= Kanban.Api/wwwroot)へ
    /// ディレクトリ+プレースホルダをプロビジョンする。存在するものは触らない(exists-don't-clobber)。
    /// 起動前なので host から ContentRootPath を引けない → テストアセンブリから
    /// 上へ辿ってリポジトリルートの Kanban.Api を特定する(WebApplicationFactory が決める
    /// content root と同じ場所になる)。</summary>
    private void ProvisionSpaBeforeHost()
    {
        var wwwroot = Path.Combine(LocateRepoRoot(), "Kanban.Api", "wwwroot");
        if (!Directory.Exists(wwwroot))
        {
            Directory.CreateDirectory(wwwroot);
            _createdWwwrootDir = wwwroot;
        }
        var index = Path.Combine(wwwroot, "index.html");
        if (!File.Exists(index))
        {
            File.WriteAllText(index, PlaceholderHtml);
            _createdPlaceholder = index;
        }
    }

    private static string LocateRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent!)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Kanban.Api", "Kanban.Api.csproj")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException($"Kanban.Api project not found above {AppContext.BaseDirectory}");
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

    // 再デプロイ時のホワイトアウト防止: index.html はブローザ側で cached させてはならない
    // ( hashed asset の入った index を cache されると、再ビルド後の旧 index → 旧JS 404 → 白画面 )
    [TestMethod]
    public async Task Get_Root_IndexHtmlIsNeverCached()
    {
        var resp = await _client.GetAsync("/");
        await AssertHtmlAsync(resp);
        AssertNoStore(resp);
    }

    [TestMethod]
    public async Task Get_DeepLink_IndexHtmlIsNeverCached()
    {
        var resp = await _client.GetAsync("/boards/xyz");
        await AssertHtmlAsync(resp);
        AssertNoStore(resp);
    }

    private static void AssertNoStore(HttpResponseMessage resp)
    {
        Assert.IsTrue(resp.Headers.TryGetValues("Cache-Control", out var values),
            "index.html responses must carry Cache-Control (prevents stale-index whiteout after redeploy)");
        StringAssert.Contains(string.Join(",", values!), "no-store",
            $"index.html must be no-store, was '{string.Join(",", values!)}'");
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
        // 実行クラッシュ残骸の上書きを避けるため一意サフィックス(Minor 2)
        var backup = index + $".r10-backup-{Guid.NewGuid():N}";
        var moved = false;
        try
        {
            if (File.Exists(index)) { File.Move(index, backup); moved = true; }
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
