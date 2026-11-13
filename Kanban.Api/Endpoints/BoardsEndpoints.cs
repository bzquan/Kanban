using Kanban.Api.Contracts;
using Kanban.Services;
using MongoDB.Bson;

namespace Kanban.Api.Endpoints;

public record BoardCreateRequest(string? UiLanguage);

public static class BoardsEndpoints
{
    public static IEndpointRouteBuilder MapBoardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards");

        // 一覧(SeqNo昇順)。ボード0枚時は LoadBoards が1枚自動作成(既存WPF動作とのパリティ)。
        group.MapGet("", async (BoardCommandService svc) =>
        {
            var boards = await svc.GetBoardsAsync();
            return Results.Ok(boards.Select(BoardDto.From));
        })
            .Produces<List<BoardDto>>(200);

        // 作成(既定5列・作成時UI言語でローカライズ済み)→ 201
        group.MapPost("", async (BoardCreateRequest req, BoardCommandService svc, IBoardChangeNotifier notifier) =>
        {
            try
            {
                var board = await svc.CreateBoardAsync(req.UiLanguage ?? "en");
                notifier.BoardChanged(board._id);
                return Results.Created($"/api/boards/{board._id}", BoardDto.From(board));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
            .Produces<BoardDto>(201)
            .Produces(400);

        // 削除 → 204(不正id→400、未在→404)
        group.MapDelete("{id}", async (string id, BoardCommandService svc, IBoardChangeNotifier notifier) =>
        {
            if (!ObjectId.TryParse(id, out var boardId))
                return Results.BadRequest(new { error = "invalid board id" });
            try
            {
                await svc.DeleteAsync(boardId);
                notifier.BoardChanged(boardId);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"board {id} not found" });
            }
        })
            .Produces(204)
            .Produces(400)
            .Produces(404);

        // プロセスステップ編集(追加/削除/リネーム)→ 200 + 更新後BoardDto
        // ガード(3件未満/カード残存列の削除)→ 409、不正id→400、未在→404
        group.MapPut("{id}/process-steps", async (string id, List<ProcessStepDto> steps, BoardCommandService svc, IBoardChangeNotifier notifier) =>
        {
            if (!ObjectId.TryParse(id, out var boardId))
                return Results.BadRequest(new { error = "invalid board id" });
            try
            {
                var result = await svc.UpdateProcessStepsAsync(boardId, steps.Select(s => s.ToRepositoryStep()).ToList());
                if (!result.Success)
                {
                    return Results.Json(new { guard = result.Guard?.ToString() }, statusCode: StatusCodes.Status409Conflict);
                }
                notifier.BoardChanged(boardId);
                var board = (await svc.GetBoardsAsync()).FirstOrDefault(b => b._id == boardId);
                return board is null ? Results.NotFound() : Results.Ok(BoardDto.From(board));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"board {id} not found" });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
            .Produces<BoardDto>(200)
            .Produces(400)
            .Produces(404)
            .Produces(409);

        return app;
    }
}
