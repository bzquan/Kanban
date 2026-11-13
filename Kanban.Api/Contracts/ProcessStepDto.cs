namespace Kanban.Api.Contracts;

public class ProcessStepDto
{
    public int PhaseSeqNo { get; set; }
    public string? LabelColor { get; set; }
    public string? Name { get; set; }
    public string? WIPTitle { get; set; }
    public string? DoneTitle { get; set; }
    public bool IsStartOfCycleTime { get; set; }
    public bool IsEndOfCycleTime { get; set; }

    public static ProcessStepDto From(Kanban.Repository.ProcessStep step) => new()
    {
        PhaseSeqNo = step.PhaseSeqNo,
        LabelColor = step.LabelColor,
        Name = step.Name,
        WIPTitle = step.WIPTitle,
        DoneTitle = step.DoneTitle,
        IsStartOfCycleTime = step.IsStartOfCycleTime,
        IsEndOfCycleTime = step.IsEndOfCycleTime,
    };

    public Kanban.Repository.ProcessStep ToRepositoryStep() => new()
    {
        PhaseSeqNo = PhaseSeqNo,
        LabelColor = LabelColor,
        Name = Name,
        WIPTitle = WIPTitle,
        DoneTitle = DoneTitle,
        IsStartOfCycleTime = IsStartOfCycleTime,
        IsEndOfCycleTime = IsEndOfCycleTime,
    };
}
