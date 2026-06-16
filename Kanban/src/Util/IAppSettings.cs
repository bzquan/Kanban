using System.Windows;

namespace Kanban.Util
{
    public interface IAppSettings
    {
        Languages Language { get; set; }
        string Database { get; set; }
        Size MainWindowSize { get; set; }
        bool IsMainWindowStateMaximized { get; set; }
        bool IsBoardZoomed { get; set; }
        string DBBackupFolder { get; set; }
        string LastUsedBoardID { get; set; }
        MetricChartType LastUsedMetricChartType { get; set; }
        void Save();
    }
}
