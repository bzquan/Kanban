namespace Kanban.Api.Contracts;

public class BoardDto
{
    public string Id { get; set; } = "";
    public int SeqNo { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Developers { get; set; } = "";
    public List<ProcessStepDto> ProcessSteps { get; set; } = new();

    public static BoardDto From(Kanban.Model.Board board) => new()
    {
        Id = board._id.ToString(),
        SeqNo = board.SeqNo,
        Title = board.Title,
        Description = board.Description,
        Developers = board.Developers,
        ProcessSteps = board.DevProcess.ProcessSteps
                                .OrderBy(s => s.PhaseSeqNo)
                                .Select(ProcessStepDto.From)
                                .ToList(),
    };
}
