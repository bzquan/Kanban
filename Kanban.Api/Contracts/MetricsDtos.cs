using Kanban.Services;

namespace Kanban.Api.Contracts;

public class CfdSeriesDto
{
    public string StepName { get; set; } = "";
    public int[] CumulativeFlowCounts { get; set; } = Array.Empty<int>();
}

public class CfdSummaryDto
{
    public int TotalDays { get; set; }
    public int MaxWip { get; set; }
    public double AverageWip { get; set; }
    public DateTime MaxWipDate { get; set; }
    public int WorkingDays4MaxWip { get; set; }
}

public class CfdDto
{
    public List<string> Days { get; set; } = new();
    public List<CfdSeriesDto> Series { get; set; } = new();
    public CfdSummaryDto Summary { get; set; } = new();
}

public class CycleTimeBucketDto
{
    public string StepName { get; set; } = "";
    public int Percentage { get; set; }
}

public class CycleTimeWeekDto
{
    public List<CycleTimeBucketDto> Buckets { get; set; } = new();
}

public class CycleTimeDto
{
    public List<string> WeekEnds { get; set; } = new();
    public List<CycleTimeWeekDto> Weeks { get; set; } = new();
    public double[] AverageCycleTimeDays { get; set; } = Array.Empty<double>();
    public int WeeklyAverageCycleTimeDays { get; set; }
}

public class WorkPerformanceCountsDto
{
    public int AheadOfScheduleCount { get; set; }
    public int OnTimeCount { get; set; }
    public int DelayedCount { get; set; }
}

public class ThroughputSummaryDto
{
    public double RealAverageThroughputPerWeek { get; set; }
    public double EstimatedCycleTimeWeeks { get; set; } = -1.0;
}

public class ThroughputDto
{
    public List<string> WeekEnds { get; set; } = new();
    public int[] ItemCounts { get; set; } = Array.Empty<int>();
    public List<WorkPerformanceCountsDto> Performance { get; set; } = new();
    public ThroughputSummaryDto Summary { get; set; } = new();
}

public class BugsBlockedDto
{
    public List<string> Days { get; set; } = new();
    public int[] BugFixCounts { get; set; } = Array.Empty<int>();
    public int[] BlockedCounts { get; set; } = Array.Empty<int>();
}

public class VelocityDto
{
    public List<string> WeekEnds { get; set; } = new();
    public int[] StoryPoints { get; set; } = Array.Empty<int>();
    public bool[] HasUnknownStoryPoints { get; set; } = Array.Empty<bool>();
    public double AverageVelocity4Every2Weeks { get; set; }
}

/// <summary>Domain 計算結果 → 計画規定の REST DTO への形だけの写像(数式なし)。</summary>
public static class MetricsDtoMapper
{
    public static CfdDto From(CfdResult r) => new()
    {
        Days = r.Days,
        Series = r.Series.Select(s => new CfdSeriesDto
        {
            StepName = s.StepName,
            CumulativeFlowCounts = s.CumulativeFlowCounts,
        }).ToList(),
        Summary = new CfdSummaryDto
        {
            TotalDays = r.Summary.TotalDays,
            MaxWip = r.Summary.MaxWip,
            AverageWip = r.Summary.AverageWip,
            MaxWipDate = r.Summary.MaxWipDate,
            WorkingDays4MaxWip = r.Summary.WorkingDays4MaxWip,
        },
    };

    public static CycleTimeDto From(CycleTimeResult r) => new()
    {
        WeekEnds = r.WeekEnds,
        Weeks = r.Weeks.Select(w => new CycleTimeWeekDto
        {
            Buckets = w.Buckets.Select(b => new CycleTimeBucketDto
            {
                StepName = b.StepName,
                Percentage = b.Percentage,
            }).ToList(),
        }).ToList(),
        AverageCycleTimeDays = r.AverageCycleTimeDays,
        WeeklyAverageCycleTimeDays = r.WeeklyAverageCycleTimeDays,
    };

    public static ThroughputDto From(ThroughputResult r) => new()
    {
        WeekEnds = r.WeekEnds,
        ItemCounts = r.ItemCounts,
        Performance = r.Performance.Select(p => new WorkPerformanceCountsDto
        {
            AheadOfScheduleCount = p.AheadOfScheduleCount,
            OnTimeCount = p.OnTimeCount,
            DelayedCount = p.DelayedCount,
        }).ToList(),
        Summary = new ThroughputSummaryDto
        {
            RealAverageThroughputPerWeek = r.Summary.RealAverageThroughputPerWeek,
            EstimatedCycleTimeWeeks = r.Summary.EstimatedCycleTimeWeeks,
        },
    };

    public static BugsBlockedDto From(BugsBlockedResult r) => new()
    {
        Days = r.Days,
        BugFixCounts = r.BugFixCounts,
        BlockedCounts = r.BlockedCounts,
    };

    public static VelocityDto From(VelocityResult r) => new()
    {
        WeekEnds = r.WeekEnds,
        StoryPoints = r.StoryPoints,
        HasUnknownStoryPoints = r.HasUnknownStoryPoints,
        AverageVelocity4Every2Weeks = r.AverageVelocity4Every2Weeks,
    };
}
