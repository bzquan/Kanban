namespace Kanban.Util;

// WPF非依存のUtil.csメソッドのみをコピーしたもの(Model/Repository層から呼ばれる)。
// 元: Kanban.Util.Util(WPF側)。呼び出し元: EstimatedWorkEffortExtension/BoardHistory/CycleTimeMetrics/CycleTimeDistribution
public static class DomainUtil
{
    public static string? PackImageURI(string image_name)
    {
        if (image_name != null)
            return "pack://application:,,,/Kanban;component/images/" + image_name;
        else
            return null;
    }

    public static void IfNotNull<T>(this T obj, Action<T> action) where T : class
    {
        if (obj != null) action(obj);
    }

    public static TR? IfNotNull<T, TR>(this T obj, Func<T, TR> func) where T : class
    {
        return (obj != null) ? func(obj) : default(TR);
    }

    public static int RoundUpByDivision(int total, int part)
    {
        if (part == 0) return 0;
        return (total + part - 1) / part;
    }
}
