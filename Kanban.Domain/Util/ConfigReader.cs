using System;
using System.Configuration;

namespace Kanban.Util
{
    public static class ConfigReader
    {
        public static T GetValue<T>(string key, T defaultValue)
        {
            try
            {
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
