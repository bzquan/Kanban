using System.ComponentModel;

namespace Kanban.Repository
{
    public enum CardType
    {
        [Description("English:Feature|日本語:新規機能|中文:新功能")]
        Feature,

        [Description("English:Improvement|日本語:改善|中文:改进")]
        Improvement,

        [Description("English:Bug fix|日本語:バグ改修|中文:修改Bug")]
        BugFix,

        [Description("English:Investigate deffect|日本語:障害調査|中文:调查故障")]
        DefectInvestigation,

        [Description("English:Technical debt|日本語:技術債|中文:技术债")]
        TechnicalDebt,

        [Description("English:Cancelled item|日本語:取消|中文:被取消的条目")]
        Cancelled,

        [Description("English:Evaluation|日本語:評価|中文:评估")]
        Evaluation,

        [Description("English:Test design|日本語:試験設計|中文:测试设计")]
        TestDesign,

        [Description("English:Task|日本語:タスク|中文:细条目")]
        Task,
    }
}
