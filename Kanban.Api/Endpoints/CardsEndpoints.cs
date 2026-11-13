using Kanban.Api.Contracts;
using MongoDB.Bson;

namespace Kanban.Api.Endpoints;

public static class CardsEndpoints
{
    /// <summary>
    /// カード読み取り。照会ロジック(フィルタ分割・リリース日抽出・180日cut-off・最終列Limit)は
    /// 既存 CardRepository.GetCards → DBCards.GetCards にそのまま委譲する(サーバー側で新規書かない)。
    /// </summary>
    public static IEndpointRouteBuilder MapCardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards");

        group.MapGet("/{boardId}/cards", async (
            string boardId,
            int? processStepSeqNo,
            bool? isWip,
            string? filter,
            string? onBoard,
            Kanban.Model.IBoardRepository boardRepository,
            Kanban.Model.ICardRepository cardRepository) =>
        {
            if (!ObjectId.TryParse(boardId, out var id))
                return Results.BadRequest(new { error = "invalid board id" });

            var board = (await boardRepository.LoadBoards()).FirstOrDefault(b => b._id == id);
            if (board is null) return Results.NotFound();

            if (!TryParseOnBoardFilter(onBoard, out var onBoardFilter))
                return Results.BadRequest(new { error = "onBoard must be front|back|all" });

            var workState = new Kanban.Model.WorkState
            {
                ProcessStepSeqNo = processStepSeqNo ?? 0,
                IsWIP = isWip ?? true,
            };

            var cards = await cardRepository.GetCards(board, workState, filter ?? "", onBoardFilter);
            return Results.Ok(cards.Select(CardDtoMapper.From).ToList());
        })
            .Produces<List<CardDto>>(200)
            .Produces(400)
            .Produces(404);

        // ---- 書き込み系は CardCommandService 経由のみ(Activity監査・SeqNo不変条件の唯一の実装元) ----

        app.MapPost("/api/cards", async (
            CardCreateRequest request,
            Kanban.Services.CardCommandService service,
            Kanban.Services.IBoardChangeNotifier notifier) =>
        {
            if (!ObjectId.TryParse(request.BoardId, out var boardId))
                return Results.BadRequest(new { error = "invalid board id" });
            try
            {
                var card = await service.CreateCardAsync(boardId, request.ProcessStepSeqNo, request.IsWip);
                notifier.BoardChanged(card.BoardID);
                return Results.Created($"/api/cards/{card._id}", CardDtoMapper.From(card));
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(new { error = "process step out of range" }); }
        })
            .Produces<CardDto>(201)
            .Produces(400)
            .Produces(404);

        app.MapPatch("/api/cards/{id}", async (
            string id,
            Kanban.Services.CardPatch patch,
            Kanban.Services.CardCommandService service,
            Kanban.Repository.IDBCard dbCards,
            Kanban.Services.IBoardChangeNotifier notifier) =>
        {
            if (!ObjectId.TryParse(id, out var cardId))
                return Results.BadRequest(new { error = "invalid card id" });
            try
            {
                await service.UpdateFieldsAsync(cardId, patch);
                var repoCard = await dbCards.GetCardById(cardId);
                if (repoCard is null) return Results.NotFound();
                notifier.BoardChanged(repoCard.BoardID);
                return Results.Ok(CardDtoMapper.From(new Kanban.Model.Card(repoCard)));
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "invalid field value" }); }
        })
            .Produces<CardDto>(200)
            .Produces(400)
            .Produces(404);

        app.MapPatch("/api/cards/{id}/move", async (
            string id,
            Kanban.Services.MoveCardRequest request,
            Kanban.Services.CardCommandService service,
            Kanban.Repository.IDBCard dbCards,
            Kanban.Services.IBoardChangeNotifier notifier) =>
        {
            if (!ObjectId.TryParse(id, out var cardId))
                return Results.BadRequest(new { error = "invalid card id" });
            var preCard = await dbCards.GetCardById(cardId);
            if (preCard is null) return Results.NotFound();
            try
            {
                await service.MoveCardAsync(cardId, request);
                notifier.BoardChanged(preCard.BoardID);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(new { error = "process step out of range" }); }
        })
            .Produces(204)
            .Produces(400)
            .Produces(404);

        app.MapDelete("/api/cards/{id}", async (
            string id,
            Kanban.Services.CardCommandService service,
            Kanban.Repository.IDBCard dbCards,
            Kanban.Services.IBoardChangeNotifier notifier) =>
        {
            if (!ObjectId.TryParse(id, out var cardId))
                return Results.BadRequest(new { error = "invalid card id" });
            var preCard = await dbCards.GetCardById(cardId);
            if (preCard is null) return Results.NotFound();
            try
            {
                await service.DeleteCardAsync(cardId);
                notifier.BoardChanged(preCard.BoardID);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        })
            .Produces(204)
            .Produces(400)
            .Produces(404);

        return app;
    }

    private static bool TryParseOnBoardFilter(string? value, out Kanban.Repository.OnBoardFilter filter)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "":
            case "front": filter = Kanban.Repository.OnBoardFilter.OnFrontBoard; return true;
            case "back": filter = Kanban.Repository.OnBoardFilter.OnBackBoard; return true;
            case "all": filter = Kanban.Repository.OnBoardFilter.All; return true;
            default: filter = default; return false;
        }
    }
}
