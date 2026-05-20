using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kanban.Util
{
    public static class KanbanDefinitions
    {
        public static readonly string BoardCollectionName = "Boards";
        public static readonly string CardsCollectionName = "Cards";
        public static readonly string ActivitiesCollectionName = "Activities";
        public static readonly string MongoDumpTool = "mongodump.exe";
        public static readonly string MongoRestoreTool = "mongorestore.exe";
        public static readonly List<string> LabelColorNames = new List<string> { "Aqua", "DarkOrange", "LimeGreen", "CornflowerBlue", "DarkSeaGreen", "Gold", "Bisque", "SkyBlue" };
    }

    public enum Languages
    {
        [Description("日本語")]
        Japanese,

        [Description("中文")]
        Chinese,

        [Description("English")]
        English,
    }

    public enum DBPriority4Restore
    {
        [Description("English:Current database|日本語:現在のデータベース|中文:当前数据库")]
        CurrentDB,

        [Description("English:Database to be restored|日本語:復元元のデータベース|中文:要恢复的数据库")]
        ToBeRestoredDB,

        [Description("English:Override|日本語:上書き|中文:覆盖")]
        NoPriority,
    }

    public enum MetricChartType
    {
        [Description("English:Cumulative Flow Diagram(CFD)|日本語:累積フロー図(CFD)|中文:累计流量图(CFD)")]
        CumulativeFlowDiagram,

        [Description("English:Throughput|日本語:スループット|中文:吞吐量")]
        Throughput,

        [Description("English:Cycle time distribution|日本語:作業分布|中文:工作分布图")]
        CycleTimeDistribution,

        [Description("English:Bugs and blocked|日本語:バグと不順作業|中文:故障和堵塞")]
        BugsAndBlocked,

        [Description("English:Team velocity|日本語:チームベロシティ|中文:团队速度")]
        TeamVelocity
    }
}
