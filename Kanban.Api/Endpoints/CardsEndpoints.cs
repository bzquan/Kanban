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
        });

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
