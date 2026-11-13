using System.Net;
using System.Net.Http.Json;
using Kanban.Api;
using Kanban.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;

namespace Kanban.Api.Tests.Endpoints;

[TestClass]
public class MetricsEndpointTest
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;
    private Kanban.Model.Board _board = null!;

    [TestInitialize]
    public async Task Setup()
    {
        TestDatabase.Reset();
        TestDatabase.WireModel(TestDatabase.NewClient());
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient();

        // ボードはAPI経由(実クライアントと同じ)→ TestDatabase 側で同一DBのインスタンス解決
        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage = "en" });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        var createdId = ObjectId.Parse((await resp.Content.ReadFromJsonAsync<BoardDto>())!.Id);
        _board = (await TestDatabase.NewBoardCommandService().GetBoardsAsync()).Single(b => b._id == createdId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _app.Dispose();
    }

    [TestMethod]
    public async Task AllFiveMetricsEndpoints_Return200_OnEmptyBoard()
    {
        foreach (var path in new[] { "cfd", "cycle-time", "throughput", "bugs-blocked", "velocity" })
        {
            var resp = await _client.GetAsync($"/api/boards/{_board._id}/metrics/{path}");
            Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, path);
        }

        var raw = await (await _client.GetAsync($"/api/boards/{_board._id}/metrics/cfd")).Content.ReadAsStringAsync();
        StringAssert.Contains(raw, "\"cumulativeFlowCounts\""); // camelCase
    }

    [TestMethod]
    public async Task MetricsEndpoints_InvalidBoardId_Return400()
    {
        foreach (var path in new[] { "cfd", "cycle-time", "throughput", "bugs-blocked", "velocity" })
        {
            var resp = await _client.GetAsync($"/api/boards/not-valid/metrics/{path}");
            Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode, path);
        }
    }

    [TestMethod]
    public async Task MetricsEndpoints_UnknownBoard_Return404()
    {
        foreach (var path in new[] { "cfd", "cycle-time", "throughput", "bugs-blocked", "velocity" })
        {
            var resp = await _client.GetAsync($"/api/boards/{ObjectId.GenerateNewId()}/metrics/{path}");
            Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, path);
        }
    }

    [TestMethod]
    public async Task Velocity_OverHttp_ReflectsSeededRelease()
    {
        await TestDatabase.SeedDoneCard(_board, storyPoints: 5);

        var resp = await _client.GetAsync($"/api/boards/{_board._id}/metrics/velocity");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var velocity = (await resp.Content.ReadFromJsonAsync<VelocityDto>())!;
        Assert.AreEqual(5, velocity.StoryPoints[^1]);
    }
}
