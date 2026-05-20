using System.ComponentModel;

namespace Kanban.Repository
{
    public enum EstimatedWorkEffort
    {
        [Description("English:Small(within 2 days)|日本語:小(二日以内完了)|中文:小(两天内完成)")]
        Small,

        [Description("English:Medium(within 1 week)|日本語:中(一週間以内完了)|中文:中(一周内完成)")]
        Medium,

        [Description("English:Large(within 2 weeks)|日本語:大(二週間以内完了)|中文:大(两周内完成)")]
        Large,

        [Description("English:Extra Large(more than 2 weeks)|日本語:超大+(二週間以上)|中文:超大+(两周以上)")]
        LargeExtra,

        [Description("English:Unknown|日本語:未定|中文:未定")]
        Unknown
    }
}
