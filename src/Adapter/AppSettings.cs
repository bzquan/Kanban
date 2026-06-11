using Kanban.Util;
using System.Configuration;
using System.Globalization;
using System.Windows;

namespace Kanban.Infrastructure
{
    public class AppSettings : ApplicationSettingsBase, IAppSettings
    {
        [UserScopedSetting()]
        public Languages Language
        {
            get
            {
                var lang = this["Language"];
                return (lang != null) ? (Languages)lang : GetLanguageFromCultureInfo();
            }
            set { this["Language"] = value; }
        }

        private Languages GetLanguageFromCultureInfo()
        {
            CultureInfo ci = CultureInfo.InstalledUICulture;
            switch (ci.Name)
            {
                case "ja-JP":
                    return Languages.Japanese;
                case "zh-CN":
                    return Languages.Chinese;
                default:
                    return Languages.English;
            }
        }

        [UserScopedSetting()]
        public string Database
        {
            get
            {
                string db_name = (string)this["Database"];
                return db_name ?? ConfigReader.GetValue("DB_NAME", "kanban");
            }
            set
            {
                if (value == null) return;

                string db_name = value.Trim();
                if (db_name.Length == 0) return;

                this["Database"] = db_name;
            }
        }

        [UserScopedSetting()]
        public string DBBackupFolder
        {
            get { return (string)this["DBBackupFolder"]; }
            set { this["DBBackupFolder"] = value; }
        }

        [UserScopedSetting()]
        [DefaultSettingValueAttribute("CumulativeFlowDiagram")]
        public MetricChartType LastUsedMetricChartType
        {
            get { return (MetricChartType)this["LastUsedMetricChartType"]; }
            set { this["LastUsedMetricChartType"] = value; }
        }

        [UserScopedSetting()]
        [DefaultSettingValueAttribute("")]
        public string LastUsedBoardID
        {
            get { return (string)this["LastUsedBoardID"]; }
            set { this["LastUsedBoardID"] = value; }
        }

        [UserScopedSetting()]
        [DefaultSettingValueAttribute("1024, 800")]
        public Size MainWindowSize
        {
            get { return (Size)this["MainWindowSize"]; }
            set { this["MainWindowSize"] = value; }
        }

        [UserScopedSetting()]
        [DefaultSettingValueAttribute("False")]
        public bool IsMainWindowStateMaximized
        {
            get { return (bool)this["IsMainWindowStateMaximized"]; }
            set { this["IsMainWindowStateMaximized"] = value; }
        }

        [UserScopedSetting()]
        [DefaultSettingValueAttribute("False")]
        public bool IsBoardZoomed
        {
            get { return (bool)this["IsBoardZoomed"]; }
            set { this["IsBoardZoomed"] = value; }
        }
    }
}
