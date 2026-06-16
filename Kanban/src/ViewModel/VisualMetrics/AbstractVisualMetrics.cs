using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Kanban.ViewModel;

public class AbstractVisualMetrics : DrawingVisual
{
    public const double METRICS_CHART_WIDTH = 840;
    public const double METRICS_CHART_HEIGHT = 480;

    public const int ONE_WEEK = 7; // One week is 7 days
    public const double SCALE_HORIZONTAL_INTERVAL = 80.0;
    public const double SCALE_VERTICAL_INTERVAL = 40.0;

    protected const double ARROW_LEN = 5.0;
    protected const double PLOT_RADIUS = 3.0;

    public const int PADDING = 10;
    public const int H_SCALE_COUNT = (int)(METRICS_CHART_WIDTH / SCALE_HORIZONTAL_INTERVAL) - 1;
    public const int V_SCALE_COUNT = (int)(METRICS_CHART_HEIGHT / SCALE_VERTICAL_INTERVAL) - 2;

    public static IViewModelProperties Properties { get; set; }

    public static DateTime FromDate4Metrics => DateTime.Now.AddDays(-H_SCALE_COUNT * ONE_WEEK);

    public static double CalculateYCoordinate(int cardCount, double yUnit)
    {
        double startY = VisualXYGridChart.StartPos.Y;
        double offset = (SCALE_VERTICAL_INTERVAL * cardCount) / yUnit;
        return (startY - offset);
    }

    protected static FormattedText GetFormattedText(string text)
    {
        return GetFormattedText(text, Brushes.Blue, 12.0);
    }

    protected static FormattedText GetFormattedText(string text, Brush brush, double emSize)
    {
        return GetFormattedText(text, brush, emSize, "Arial");
    }

    protected static FormattedText GetFormattedText(string text, Brush brush, double emSize, string typeFace)
    {
        return new FormattedText(text,
                                 CultureInfo.CurrentCulture,
                                 FlowDirection.LeftToRight,
                                 new Typeface(typeFace),
                                 emSize,
                                 brush,
                                 Util.Util.PixelsPerDip());
    }

    protected static FormattedText GetFormattedText(string text, Brush brush, double emSize, FontFamily fontFamity)
    {
        Typeface typeFace = new Typeface(fontFamity,
                                         FontStyles.Normal,
                                         FontWeights.Normal,
                                         FontStretches.Normal,
                                         new FontFamily("Arial"));
        FormattedText formattedText = new FormattedText(
                                            text,
                                            CultureInfo.CurrentCulture,
                                            FlowDirection.LeftToRight,
                                            typeFace,
                                            emSize,
                                            brush,
                                            Util.Util.PixelsPerDip());
        formattedText.SetFontFamily(fontFamity);
        return formattedText;
    }

    // Draw a polygon or polyline.
    protected static void DrawPolygonOrPolyline(
        DrawingContext dc,
        Brush brush,
        Pen pen,
        Point[] points,
        FillRule fill_rule,
        bool draw_polygon)
    {
        // Make a StreamGeometry to hold the drawing objects.
        StreamGeometry geo = new StreamGeometry();
        geo.FillRule = fill_rule;

        // Open the context to use for drawing.
        using (StreamGeometryContext context = geo.Open())
        {
            // Start at the first point.
            context.BeginFigure(points[0], true, draw_polygon);

            // Add the points after the first one.
            context.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }

        dc.DrawGeometry(brush, pen, geo);
    }

    protected static void DrawRightArrow(DrawingContext dc, Brush brush, Point pt)
    {
        Point p1 = new Point(pt.X, pt.Y - ARROW_LEN);
        Point p2 = new Point(pt.X + ARROW_LEN, pt.Y);
        Point p3 = new Point(pt.X, pt.Y + ARROW_LEN);

        DrawArrow(dc, brush, p1, p2, p3);
    }

    protected static void DrawUpArrow(DrawingContext dc, Brush brush, Point pt)
    {
        Point p1 = new Point(pt.X - ARROW_LEN, pt.Y + ARROW_LEN);
        Point p2 = new Point(pt.X + ARROW_LEN, pt.Y + ARROW_LEN);
        Point p3 = new Point(pt.X, pt.Y);

        DrawArrow(dc, brush, p1, p2, p3);
    }

    protected static void DrawDownArrow(DrawingContext dc, Brush brush, Point pt)
    {
        Point p1 = new Point(pt.X - ARROW_LEN, pt.Y - ARROW_LEN);
        Point p2 = new Point(pt.X + ARROW_LEN, pt.Y - ARROW_LEN);
        Point p3 = new Point(pt.X, pt.Y);

        DrawArrow(dc, brush, p1, p2, p3);
    }

    protected static void DrawArrow(DrawingContext dc, Brush brush, Point p1, Point p2, Point p3)
    {
        var segments = new[]
                   {
                      new LineSegment(p2, isStroked: true),
                      new LineSegment(p3, isStroked: true)
                   };
        var figure = new PathFigure(p1, segments, closed: true);
        var geo = new PathGeometry(new[] { figure });
        dc.DrawGeometry(brush, null, geo);
    }

    protected static void DrawPercentage(DrawingContext dc, Rect rect, int percentage, string symbol = "")
    {
        Point pos = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        FormattedText formattedText = GetFormattedText(symbol + String.Format("{0}%", percentage), Brushes.Black, emSize: 12);
        double x = pos.X - formattedText.Width / 2;
        double y = pos.Y - formattedText.Height / 2;
        dc.DrawText(formattedText, new Point(x, y));
    }

    protected static void DrawItemCount(DrawingContext dc, Brush brush, CardsCountByDays items, bool drawZero)
    {
        DateTime startDate = items.FromDate;

        for (int i = 1; i < H_SCALE_COUNT + 1; i++)
        {
            DateTime endDate = startDate.AddDays(ONE_WEEK);

            int itemCount = items.GetItemCount(endDate);
            if (drawZero || (itemCount > 0))
            {
                // Draw count text only if count > 0
                Point point = items.GetPoint(endDate);
                DrawTextAtPoint(dc, brush, itemCount.ToString(), point);
            }

            startDate = endDate;
        }
    }

    protected static void DrawTextAtPoint(DrawingContext dc, Brush brush, string text, Point pos)
    {
        dc.DrawEllipse(brush, null, pos, PLOT_RADIUS, PLOT_RADIUS);

        FormattedText formattedText = GetFormattedText(text);
        double x = pos.X - formattedText.Width / 2;
        double y = pos.Y - formattedText.Height - PADDING / 2;
        dc.DrawText(formattedText, new Point(x, y));
    }

    public virtual VisualHitInfo GetVisualHitInfo(Point hitPoint) => null;
    public virtual string GetVisualToolTip(Point point) => null;
}
