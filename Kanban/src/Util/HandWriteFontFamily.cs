using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Kanban.Util
{
    public static class HandWriteFontFamily
    {
        static FontFamily s_HandWriteFont;

        public static FontFamily Bradley_Hand_ITC_FontFamily => GetFontFamilyFromLocalResource("Bradley Hand ITC");

        public static FontFamily GetFontFamilyFromLocalResource(string familyName)
        {
            if (s_HandWriteFont == null)
            {
                string uri = "pack://application:,,,/kanban;component/fonts/";
                foreach (FontFamily fontFamily in Fonts.GetFontFamilies(new Uri(uri)))
                {
                    bool is_expected_font = fontFamily.ToString().IndexOf(familyName, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (is_expected_font)
                    {
                        s_HandWriteFont = fontFamily;
                        break;
                    }
                }
                s_HandWriteFont = s_HandWriteFont ?? new FontFamily(familyName); // new FontFamily(new Uri(uri), familyName);
            }

            return s_HandWriteFont;
        }
    }
}
