using System;
using System.Configuration;

namespace Kanban.Util
{
    // サーバー側(Kanban.Api)が起動時に上書きする設定値。WPFは常に空=従来動作(AppSettingsReader)。
    public static class ConfigOverrides
    {
        public static readonly Dictionary<string, string> Values = new();
    }

    public static class ConfigReader
    {
        public static T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                if (ConfigOverrides.Values.TryGetValue(key, out var raw))
                    return (T)Convert.ChangeType(raw, typeof(T));
                return (T)s_Reader.GetValue(key, typeof(T));
            }
            catch (InvalidOperationException)
            {
                return defaultValue;
            }
        }

        static AppSettingsReader s_Reader = new AppSettingsReader();
    }
}
