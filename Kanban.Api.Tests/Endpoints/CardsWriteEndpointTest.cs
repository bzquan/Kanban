using System.Net;
using System.Net.Http.Json;
using Kanban.Api;
using Kanban.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;

namespace Kanban.Api.Tests.Endpoints;

[TestClass]
public class CardsWriteEndpointTest
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;
    private ObjectId _boardId;

    [TestInitialize]
    public async Task Setup()
    {
        TestDatabase.Reset();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost);
            b.UseSetting("Kanban:DB_NAME", TestDatabase.DBName);
        });
        _client = _app.CreateClient();

        var resp = await _client.PostAsJsonAsync("/api/boards", new { uiLanguage = "en" });
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        _boardId = ObjectId.Parse((await resp.Content.ReadFromJsonAsync<BoardDto>())!.Id);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _app.Dispose();
    }

    private Task<HttpResponseMessage> CreateCardAsync(int step, bool wip) =>
        _client.PostAsJsonAsync("/api/cards", new { boardId = _boardId.ToString(), processStepSeqNo = step, isWip = wip });

    private async Task<List<CardDto>> GetColumnAsync(int step, bool wip)
    {
        var resp = await _client.GetAsync($"/api/boards/{_boardId}/cards?processStepSeqNo={step}&isWip={(wip ? "true" : "false")}");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<List<CardDto>>())!;
    }

    [TestMethod]
    public async Task Post_Move_Patch_Delete_Roundtrip()
    {
        // POST → 201 + 列(0,WIP)に見える
        var createResp = await CreateCardAsync(step: 0, wip: true);
        Assert.AreEqual(HttpStatusCode.Created, createResp.StatusCode);
        var card = (await createResp.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.AreEqual(0, card.SeqNo);
        Assert.HasCount(1, await GetColumnAsync(0, true));

        // PATCH move → 列(2,Done)へ
        var moveResp = await _client.PatchAsJsonAsync(
            $"/api/cards/{card.Id}/move", new { processStepSeqNo = 2, isWip = false, index = 0 });
        Assert.IsTrue(moveResp.IsSuccessStatusCode, $"move: {(int)moveResp.StatusCode}");
        Assert.HasCount(0, await GetColumnAsync(0, true));
        var moved = await GetColumnAsync(2, false);
        Assert.HasCount(1, moved);
        Assert.AreEqual(card.Id, moved[0].Id);

        // PATCH edit → Title/StoryPoints/Tag
        var patchResp = await _client.PatchAsJsonAsync(
            $"/api/cards/{card.Id}", new { title = "Edited Title", storyPoints = 5, tag = "qa" });
        Assert.AreEqual(HttpStatusCode.OK, patchResp.StatusCode);
        var patched = (await patchResp.Content.ReadFromJsonAsync<CardDto>())!;
        Assert.AreEqual("Edited Title", patched.Title);
        Assert.AreEqual(5, patched.StoryPoints);

        // DELETE → 204、列は空
        var deleteResp = await _client.DeleteAsync($"/api/cards/{card.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResp.StatusCode);
        Assert.HasCount(0, await GetColumnAsync(2, false));
    }

    [TestMethod]
    public async Task Post_InvalidBoardId_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/cards",
            new { boardId = "not-an-objectid", processStepSeqNo = 0, isWip = true });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Post_UnknownBoard_Returns404()
    {
        var resp = await _client.PostAsJsonAsync("/api/cards",
            new { boardId = ObjectId.GenerateNewId().ToString(), processStepSeqNo = 0, isWip = true });
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Review Focus 3: 範囲外ステップ → 400
    [TestMethod]
    public async Task Post_StepOutOfRange_Returns400()
    {
        var resp = await CreateCardAsync(step: 99, wip: true);
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Move_StepOutOfRange_Returns400()
    {
        var card = (await (await CreateCardAsync(0, true)).Content.ReadFromJsonAsync<CardDto>())!;
        var resp = await _client.PatchAsJsonAsync(
            $"/api/cards/{card.Id}/move", new { processStepSeqNo = 42, isWip = true, index = 0 });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Move_InvalidCardId_Returns400()
    {
        var resp = await _client.PatchAsJsonAsync(
            "/api/cards/xyz/move", new { processStepSeqNo = 1, isWip = true, index = 0 });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Patch_UnknownCard_Returns404()
    {
        var resp = await _client.PatchAsJsonAsync($"/api/cards/{ObjectId.GenerateNewId()}", new { title = "x" });
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Delete_UnknownCard_Returns404()
    {
        var resp = await _client.DeleteAsync($"/api/cards/{ObjectId.GenerateNewId()}");
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
