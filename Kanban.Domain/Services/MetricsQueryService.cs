using Kanban.Model;
using MongoDB.Bson;
using Board = Kanban.Model.Board;
using static Kanban.Util.DateTimeUtil;

namespace Kanban.Services;

/// <summary>
/// メトリクス5系統(CFD/CycleTime/Throughput/BugsBlocked/Velocity)の読み取り。
/// 計算は既存 BoardHistoryBuilder → BoardHistory → CycleTimeMetrics/ThroughputMetrics に完全委譲し、
/// 数式は一切再実装しない(WPF Visual*Builder が読んでいる BoardHistory のプロパティをそのまま DTO へ)。
///
/// 並行性: BoardHistory.FromDate/ToDate/Board/DevProcess は静的、CardsActivitiesCache も静的だが
/// Current 差し替え式。MetricsQueryService は SemaphoreSlim(1,1) で「構築→計算」全体を直列化し、
/// 計算専用に new した CardsActivitiesCacheInstance を Current にバインドする(設計書リスク表対応)。
/// データ量は小規模と確認済み(設計書§6)。
/// </summary>
public class MetricsQueryService
{
    private static readonly SemaphoreSlim s_calcLock = new SemaphoreSlim(1, 1);

    // WPF AbstractVisualMetrics: H_SCALE_COUNT=9, ONE_WEEK=7 → FromDate = Now-63日、週幅9本
    private const int Weeks4Metrics = 9;
    private const int OneWeek = 7;

    private readonly IBoardRepository m_BoardRepository;
    private readonly ICardRepository m_CardRepository;
    private readonly IActivityRepository m_ActivityRepository;

    public MetricsQueryService(
        IBoardRepository boardRepository,
        ICardRepository cardRepository,
        IActivityRepository activityRepository)
    {
        m_BoardRepository = boardRepository;
        m_CardRepository = cardRepository;
        m_ActivityRepository = activityRepository;
    }

    // ---- CFD(累积フローダイアグラム) ----
    public async Task<CfdResult> GetCfdAsync(ObjectId boardId)
    {
        await s_calcLock.WaitAsync();
        try
        {
            BoardHistory history = await BuildHistoryAsync(boardId);

            var result = new CfdResult();
            if (history.ProcessHistories.Count == 0)
            {
                result.Summary = new CfdSummary();
                return result;
            }

            result.Days = history.ProcessHistories[0].CardsByDateList
                               .Select(d => d.Date.ToString("yyyy-MM-dd")).ToList();
            result.Series = history.ProcessHistories.Select(p => new CfdSeries
            {
                StepName = p.Process.Name ?? "",
                CumulativeFlowCounts = p.CardsByDateList.Select(d => d.CumulativeFlowCardCount).ToArray(),
            }).ToList();

            WIPInfo wip = history.WIP;   // 既存 BoardHistory.WIP(最後尾列を除いた日次WIP)
            result.Summary = new CfdSummary
            {
                TotalDays = history.TotalDays(),
                MaxWip = wip.MaxWIP,
                AverageWip = wip.AverageWIP,
                MaxWipDate = wip.MaxWIPDate,
                WorkingDays4MaxWip = wip.WorkingDays4MaxWIP,
            };
            return result;
        }
        finally { s_calcLock.Release(); }
    }

    // ---- CycleTime 分布(週次、既存 CycleTimeMetrics) ----
    public async Task<CycleTimeResult> GetCycleTimeAsync(ObjectId boardId)
    {
        await s_calcLock.WaitAsync();
        try
        {
            BoardHistory history = await BuildHistoryAsync(boardId);
            var metrics = new CycleTimeMetrics(history);
            List<DateTime> weekEnds = WeekEndDates();
            metrics.SummarizeByWeekly(weekEnds);   // 既存の週次集計をそのまま呼ぶ

            var result = new CycleTimeResult
            {
                WeekEnds = weekEnds.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            };
            foreach (DateTime weekEnd in weekEnds)
            {
                CycleTimeDistribution dist = metrics.GetCycleTimeDistributionByWeekly(weekEnd);
                result.Weeks.Add(new CycleTimeWeek
                {
                    Buckets = dist.CycleTimePercentage4Steps
                                   .Select(s => new CycleTimeBucket
                                   {
                                       StepName = s.ProcessStep.Name ?? "",
                                       Percentage = s.Percentage,   // 既存 CycleTimePercentage4Step.Percentage
                                   }).ToList(),
                });
                result.AverageCycleTimeDaysList.Add(dist.AverageCycleTime4OneCard);
            }
            result.AverageCycleTimeDays = result.AverageCycleTimeDaysList.Select(v => (double)v).ToArray();
            result.WeeklyAverageCycleTimeDays = metrics.GetWeeklyAverageCycleTime();
            return result;
        }
        finally { s_calcLock.Release(); }
    }

    // ---- Throughput(週次完了数と内訳、既存 ThroughputMetrics) ----
    public async Task<ThroughputResult> GetThroughputAsync(ObjectId boardId)
    {
        await s_calcLock.WaitAsync();
        try
        {
            BoardHistory history = await BuildHistoryAsync(boardId);
            var result = new ThroughputResult();
            List<DateTime> weekEnds = WeekEndDates();
            result.WeekEnds = weekEnds.Select(d => d.ToString("yyyy-MM-dd")).ToList();

            if (history.ProcessHistories.Count == 0)
            {
                result.ItemCounts = new int[weekEnds.Count];
                result.Performance = weekEnds.Select(_ => new WorkPerformanceCounts()).ToList();
                result.Summary = new ThroughputSummary { EstimatedCycleTimeWeeks = -1.0 };
                return result;
            }

            var metrics = new ThroughputMetrics(history);
            if (metrics.IsValid())
            {
                foreach (DateTime weekEnd in weekEnds)
                {
                    CardCompletionPerformance perf = metrics.ThroughputByWeek(weekEnd);   // 既存
                    result.ItemCountsList.Add(perf.ItemCount);
                    result.Performance.Add(new WorkPerformanceCounts
                    {
                        AheadOfScheduleCount = perf.AheadOfScheduleCount,
                        OnTimeCount = perf.OnTimeCount,
                        DelayedCount = perf.DelayedCount,
                    });
                }
                result.ItemCounts = result.ItemCountsList.ToArray();
            }
            else
            {
                result.ItemCounts = new int[weekEnds.Count];
                result.Performance = weekEnds.Select(_ => new WorkPerformanceCounts()).ToList();
            }

            double avg = metrics.RealAverageThroughputPerWeek;   // 既存 ThroughputPerWeek(最終列)
            result.Summary = new ThroughputSummary
            {
                RealAverageThroughputPerWeek = avg,
                EstimatedCycleTimeWeeks = avg > 0.0 ? metrics.CardCountInWIP / avg : -1.0,  // 既存 VisualThroughputBuilder の式
            };
            return result;
        }
        finally { s_calcLock.Release(); }
    }

    // ---- Bugs & Blocked(日次2系列、既存 VisualBugFixAndBlockedBuilder と同じ選定) ----
    public async Task<BugsBlockedResult> GetBugsBlockedAsync(ObjectId boardId)
    {
        await s_calcLock.WaitAsync();
        try
        {
            BoardHistory history = await BuildHistoryAsync(boardId);

            var result = new BugsBlockedResult();
            var days = Enumerable.Range(0, history.TotalDays())
                                 .Select(i => BoardHistory.FromDate.Date.AddDays(i))
                                 .ToList();
            result.Days = days.Select(d => d.ToString("yyyy-MM-dd")).ToList();

            var bugFixCounts = new int[days.Count];
            var blockedCounts = new int[days.Count];
            for (int i = 0; i < days.Count; i++)
            {
                DateTime day = days[i];
                List<Card> cards = history.AllCardsByDate(day);   // 既存(内部で CardsActivitiesCache=Current を読む)
                bugFixCounts[i] = cards.Count(x => x.CardType == Repository.CardType.BugFix);
                blockedCounts[i] = cards.Count(x => IsBlockedOn(x, day));   // 既存 builder と同一の選定ロジック
            }
            result.BugFixCounts = bugFixCounts;
            result.BlockedCounts = blockedCounts;
            return result;
        }
        finally { s_calcLock.Release(); }
    }

    // ---- Velocity(週次リリース済み StoryPoints、既存 BoardHistoryBuilder.BuildVelocity) ----
    public async Task<VelocityResult> GetVelocityAsync(ObjectId boardId)
    {
        await s_calcLock.WaitAsync();
        try
        {
            BoardHistory history = await BuildHistoryAsync(boardId);
            return new VelocityResult
            {
                WeekEnds = history.Velocity.Select(v => v.Date.ToString("yyyy-MM-dd")).ToList(),
                StoryPoints = history.Velocity.Select(v => v.Velocity).ToArray(),
                HasUnknownStoryPoints = history.Velocity.Select(v => v.HasUnknownStoryPoints).ToArray(),
                AverageVelocity4Every2Weeks = history.AverageVelocity4Every2Weeks,
            };
        }
        finally { s_calcLock.Release(); }
    }

    // ---- 共通 ----

    /// <summary>既存 WPF の既定ウィンドウ: 今日23:59:59 まで / 9週間前(FromDate4Metrics 実値)。</summary>
    private async Task<BoardHistory> BuildHistoryAsync(ObjectId boardId)
    {
        Board board = (await m_BoardRepository.LoadBoards()).FirstOrDefault(b => b._id == boardId)
                      ?? throw new KeyNotFoundException($"board {boardId} not found");

        DateTime now = DateTime.Now;
        DateTime from = now.AddDays(-OneWeek * Weeks4Metrics);
        DateTime to = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);

        // 計算専用キャッシュに差し替え(s_calcLock で直列化されているため他リクエストと競合しない)
        CardsActivitiesCache.Current = new CardsActivitiesCacheInstance();

        var builder = new BoardHistoryBuilder(board, from, to, m_BoardRepository, m_CardRepository, m_ActivityRepository);
        return await builder.GetBoardHistory();
    }

    private static List<DateTime> WeekEndDates()
        => Enumerable.Range(1, Weeks4Metrics)
                     .Select(i => BoardHistory.FromDate.Date.AddDays(OneWeek * i))
                     .ToList();

    // VisualBugFixAndBlockedBuilder.IsBocked と同一(描画部分を落とした選定ロジック)
    private static bool IsBlockedOn(Card card, DateTime day)
    {
        List<Activity> activities = CardsActivitiesCache.GetActivities(card);
        Activity blockedOnActivity = activities.LastOrDefault(x => x.WorkState.IsBlocked &&
                                                                   x.StateChangedDate.Date <= day);
        if (blockedOnActivity == null) return false;

        Activity unblockedOnActivity = activities.FirstOrDefault(x => x.WorkState.IsBlocked == false &&
                                                                      x.StateChangedDate > blockedOnActivity.StateChangedDate &&
                                                                      IsStateChangedDateBeforeDay(x.StateChangedDate, day));

        return (unblockedOnActivity == null);
    }

    private static bool IsStateChangedDateBeforeDay(DateTime stateChangedDate, DateTime day)
    {
        if (day == DateTime.Now.Date)
        {
            return stateChangedDate < DateTime.Now;
        }
        else
        {
            return stateChangedDate.Date < day;
        }
    }
}

// ---- サービス返り値(DTO への写像は Kanban.Api の Contracts/MetricsDtos が担当) ----

public class CfdSeries
{
    public string StepName { get; set; } = "";
    public int[] CumulativeFlowCounts { get; set; } = Array.Empty<int>();
}

public class CfdSummary
{
    public int TotalDays { get; set; }
    public int MaxWip { get; set; }
    public double AverageWip { get; set; }
    public DateTime MaxWipDate { get; set; }
    public int WorkingDays4MaxWip { get; set; }
}

public class CfdResult
{
    public List<string> Days { get; set; } = new();
    public List<CfdSeries> Series { get; set; } = new();
    public CfdSummary Summary { get; set; } = new();
}

public class CycleTimeBucket
{
    public string StepName { get; set; } = "";
    public int Percentage { get; set; }
}

public class CycleTimeWeek
{
    public List<CycleTimeBucket> Buckets { get; set; } = new();
}

public class CycleTimeResult
{
    public List<string> WeekEnds { get; set; } = new();
    public List<CycleTimeWeek> Weeks { get; set; } = new();
    public List<int> AverageCycleTimeDaysList { get; set; } = new();
    public double[] AverageCycleTimeDays { get; set; } = Array.Empty<double>();
    public int WeeklyAverageCycleTimeDays { get; set; }
}

public class WorkPerformanceCounts
{
    public int AheadOfScheduleCount { get; set; }
    public int OnTimeCount { get; set; }
    public int DelayedCount { get; set; }
}

public class ThroughputSummary
{
    public double RealAverageThroughputPerWeek { get; set; }
    public double EstimatedCycleTimeWeeks { get; set; } = -1.0;
}

public class ThroughputResult
{
    public List<string> WeekEnds { get; set; } = new();
    public List<int> ItemCountsList { get; set; } = new();
    public int[] ItemCounts { get; set; } = Array.Empty<int>();
    public List<WorkPerformanceCounts> Performance { get; set; } = new();
    public ThroughputSummary Summary { get; set; } = new();
}

public class BugsBlockedResult
{
    public List<string> Days { get; set; } = new();
    public int[] BugFixCounts { get; set; } = Array.Empty<int>();
    public int[] BlockedCounts { get; set; } = Array.Empty<int>();
}

public class VelocityResult
{
    public List<string> WeekEnds { get; set; } = new();
    public int[] StoryPoints { get; set; } = Array.Empty<int>();
    public bool[] HasUnknownStoryPoints { get; set; } = Array.Empty<bool>();
    public double AverageVelocity4Every2Weeks { get; set; }
}
