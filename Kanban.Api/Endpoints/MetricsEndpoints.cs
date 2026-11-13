using Kanban.Api.Contracts;
using Kanban.Services;
using MongoDB.Bson;

namespace Kanban.Api.Endpoints;

public static class MetricsEndpoints
{
    /// <summary>メトリクス5系統。boardId 不正→400、ボード未在→404(BoardHistory の計算自体は MetricsQueryService に委譲)。</summary>
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards");

        group.MapGet("/{boardId}/metrics/cfd", async (string boardId, MetricsQueryService service) =>
            await HandleAsync(service, boardId, (s, id) => s.GetCfdAsync(id), MetricsDtoMapper.From))
            .Produces<CfdDto>(200).Produces(400).Produces(404);

        group.MapGet("/{boardId}/metrics/cycle-time", async (string boardId, MetricsQueryService service) =>
            await HandleAsync(service, boardId, (s, id) => s.GetCycleTimeAsync(id), MetricsDtoMapper.From))
            .Produces<CycleTimeDto>(200).Produces(400).Produces(404);

        group.MapGet("/{boardId}/metrics/throughput", async (string boardId, MetricsQueryService service) =>
            await HandleAsync(service, boardId, (s, id) => s.GetThroughputAsync(id), MetricsDtoMapper.From))
            .Produces<ThroughputDto>(200).Produces(400).Produces(404);

        group.MapGet("/{boardId}/metrics/bugs-blocked", async (string boardId, MetricsQueryService service) =>
            await HandleAsync(service, boardId, (s, id) => s.GetBugsBlockedAsync(id), MetricsDtoMapper.From))
            .Produces<BugsBlockedDto>(200).Produces(400).Produces(404);

        group.MapGet("/{boardId}/metrics/velocity", async (string boardId, MetricsQueryService service) =>
            await HandleAsync(service, boardId, (s, id) => s.GetVelocityAsync(id), MetricsDtoMapper.From))
            .Produces<VelocityDto>(200).Produces(400).Produces(404);

        return app;
    }

    private static async Task<IResult> HandleAsync<TResult, TDto>(
        MetricsQueryService service,
        string boardId,
        Func<MetricsQueryService, ObjectId, Task<TResult>> action,
        Func<TResult, TDto> map)
    {
        if (!ObjectId.TryParse(boardId, out var id))
            return Results.BadRequest(new { error = "invalid board id" });
        try
        {
            return Results.Ok(map(await action(service, id)));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
