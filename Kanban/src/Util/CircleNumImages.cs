using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Kanban.Util;

/// <summary>
/// 丸数字のFlyweightクラスである。
/// 同じ丸数字は一回のみ生成して内部Dictionaryで管理する
/// </summary>
public static class CircleNumImages
{
    struct NumberImageValue
    {
        public bool IsCircleNum { get; set; }
        public string Text { get; set; }
        public Color BackgroundColor { get; set; }
        public Color FontColor { get; set; }
    }

    static Dictionary<NumberImageValue, DrawingImage> s_NumberImages = new Dictionary<NumberImageValue, DrawingImage>();

    /// <summary>
    /// 丸内に文字を書いたImageを生成する
    /// </summary>
    /// <param name="text"></param>
    /// <param name="background"></param>
    /// <param name="font_color"></param>
    /// <returns></returns>
    public static DrawingImage GetCircleNumImage(string text, Color background, Color font_color)
    {
        DrawingImage numImage;
        NumberImageValue key = new NumberImageValue { IsCircleNum = true, Text = text, BackgroundColor = background, FontColor = font_color };
        if (!s_NumberImages.TryGetValue(key, out numImage))
        {
            numImage = CreateCircleNumImage(key);
            s_NumberImages.Add(key, numImage);
        }

        return numImage;
    }

    private static DrawingImage CreateCircleNumImage(NumberImageValue key)
    {
        FormattedText formattedText = GetFormattedText(ref key);
        const int length = 64;
        return DrawCircleNumImage(key, length, formattedText);
    }

    public static DrawingImage GetStoryPointSImage(string text, Color background, Color font_color)
    {
        DrawingImage numImage;
        NumberImageValue key = new NumberImageValue { IsCircleNum = false, Text = text, BackgroundColor = background, FontColor = font_color };
        if (!s_NumberImages.TryGetValue(key, out numImage))
        {
            numImage = CreatePokerNumImage(key);
            s_NumberImages.Add(key, numImage);
        }

        return numImage;
    }

    private static DrawingImage CreatePokerNumImage(NumberImageValue key)
    {
        FormattedText formattedText = GetFormattedText(ref key);
        const int length = 64;
        return DrawPokerNumImage(key, length, formattedText);
    }

    private static FormattedText GetFormattedText(ref NumberImageValue key)
    {
        FontFamily handwrite_font = HandWriteFontFamily.Bradley_Hand_ITC_FontFamily;

        double emSize = key.Text.Length < 3 ? 64 : 48; // テキストの書式設定に使用するフォントサイズ
        Typeface font_face = new Typeface(handwrite_font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        return new FormattedText(key.Text,
                                 CultureInfo.InvariantCulture,
                                 FlowDirection.LeftToRight,
                                 font_face,
                                 emSize,
                                 new SolidColorBrush(key.FontColor),
                                 Util.PixelsPerDip());
    }

    private static DrawingImage DrawCircleNumImage(NumberImageValue key, int size, FormattedText formattedText)
    {
        Rect rect = new Rect(0, 0, size, size);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            Point center = new Point(size / 2, size / 2);
            double radius = size / 2.0;
            Brush circle_brush = new SolidColorBrush(key.BackgroundColor);
            dc.DrawEllipse(circle_brush, new Pen(Brushes.Blue, 1), center, radius, radius);
            Point position = new Point((rect.Width - formattedText.Width) / 2,
                                       (rect.Height - formattedText.Height) / 2);
            dc.DrawText(formattedText, position);
        }

        return new DrawingImage(visual.Drawing);
    }

    private static DrawingImage DrawPokerNumImage(NumberImageValue key, int size, FormattedText formattedText)
    {
        Rect rect = new Rect(0, 0, size, size);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            double radius = size * 0.3;
            Brush brush = new SolidColorBrush(key.BackgroundColor);
            dc.DrawRoundedRectangle(brush, new Pen(Brushes.Blue, 1), rect, radius, radius);
            Point position = new Point((rect.Width - formattedText.Width) / 2,
                                       (rect.Height - formattedText.Height) / 2);
            dc.DrawText(formattedText, position);
        }

        return new DrawingImage(visual.Drawing);
    }
}
