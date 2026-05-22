using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

using Kanban.ViewModel;
using Kanban.Util;

namespace Kanban
{
    public partial class DrawingCanvas4Metrics : Canvas
    {
        private const double CanvasWidth = AbstractVisualMetrics.METRICS_CHART_WIDTH;
        private const double CanvasHeight = AbstractVisualMetrics.METRICS_CHART_HEIGHT;

        private List<AbstractVisualMetrics> m_Visuals = new List<AbstractVisualMetrics>();
        private ToolTip m_ToolTip = new ToolTip();
        private ScaleTransform m_ScaleTransform = new ScaleTransform(1.0, 1.0);

        public MetricsChartViewModel MetricsChartViewModel { get; set; }

        public DrawingCanvas4Metrics()
        {
            InitializeComponent();

            // Set background for receiving mouse event.
            // Canvas is transparent. Thus, all the events do not halt at canvas,
            // but they are generated for its parent element.
            base.Background = Brushes.White;

            base.MouseLeftButtonDown += OnMouseLeftButtonDown;
            base.MouseLeave += OnMouseLeave;
            base.MouseMove += OnMouseMove;
        }

        protected override Visual GetVisualChild(int index)
        {
            return m_Visuals[index];
        }

        protected override int VisualChildrenCount
        {
            get { return m_Visuals.Count; }
        }

        public void OnScrollViewSizeChanged(double width, double height)
        {
            if ((width > 0.0) && (height > 0.0))
            {
                double scale = GetScale(width, height);
                if (IsSizeChaning(scale))
                {
                    SetSize(scale);
                    ZoomVisuals(scale);
                }
            }
        }

        private double GetScale(double width, double height)
        {
            double HSCROLL_BAR_HEIGHT = SystemParameters.HorizontalScrollBarHeight;

            double scale = height / CanvasHeight;
            double adjustedHeight = height - ((CanvasWidth * scale > width) ? HSCROLL_BAR_HEIGHT : 0);
            adjustedHeight = Math.Max(adjustedHeight, HSCROLL_BAR_HEIGHT);
            return adjustedHeight / CanvasHeight;
        }

        private bool IsSizeChaning(double scale)
        {
            double newWidth = CanvasWidth * scale;
            double newHeight = CanvasHeight * scale;

            bool hasChanged = NotEqual(newWidth, base.Width) ||
                              NotEqual(newHeight, base.Height);
            return hasChanged;
        }

        private bool NotEqual(double v1, double v2) => Math.Abs(v1 - v2) > 0.1;

        private void SetSize(double scale)
        {
            base.Width = CanvasWidth * scale;
            base.Height = CanvasHeight * scale;
        }

        private void ZoomVisuals(double scale)
        {
            m_ScaleTransform.ScaleX = scale;
            m_ScaleTransform.ScaleY = scale;

            foreach (AbstractVisualMetrics visual in m_Visuals)
            {
                visual.Transform = m_ScaleTransform;
            }
        }

        private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point pointClicked = e.GetPosition(this);

            ShowHitInfo(pointClicked);
        }


        /// <summary>
        /// Close ToopTip when mouse leaving
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (m_ToolTip.IsOpen == true) m_ToolTip.IsOpen = false;
        }

        /// <summary>
        /// Try to display ToopTip if available
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point point = e.GetPosition(this);
            AbstractVisualMetrics visual = GetHitVisual(point);
            string toolTip = visual?.GetVisualToolTip(point);
            if (toolTip != null)
            {
                if (!m_ToolTip.IsOpen || (toolTip != m_ToolTip.Content.ToString()))
                {
                    m_ToolTip.IsOpen = true;
                    m_ToolTip.Content = toolTip;
                    m_ToolTip.PlacementTarget = this;
                }
            }
            else
            {
                m_ToolTip.IsOpen = false;
            }
        }

        private void ClearVisualMetrics()
        {
            foreach (AbstractVisualMetrics visual in m_Visuals)
            {
                base.RemoveVisualChild(visual);
                base.RemoveLogicalChild(visual);
            }
            m_Visuals.Clear();
        }

        private void AddVisualMetrics(AbstractVisualMetrics visual)
        {
            if (visual == null) return;

            visual.Transform = m_ScaleTransform;

            m_Visuals.Add(visual);
            base.AddVisualChild(visual);
            base.AddLogicalChild(visual);
        }

        public void OnCycleTimeDurationChanged()
        {
            DrawMetricsGraph();
        }

        private async void DrawMetricsGraph()
        {
            ClearVisualMetrics();
            List<AbstractVisualMetrics> visuals = await MetricsChartViewModel.CreateMetricsGraph();
            visuals.ForEach(x => AddVisualMetrics(x));
        }

        public void OnBoardSeleced(BoardSelectedArg arg)
        {
            MetricsChartViewModel.CurrentBoard = arg.Board;
            DrawMetricsGraph();
        }

        public void OnMetricChartTypeChanged(MetricChartType metricChartType)
        {
            MetricsChartViewModel.MetricChartShowType = metricChartType;
            DrawMetricsGraph();
        }

        private void ShowHitInfo(Point point)
        {
            RemoveVisualHitInfo();
            AbstractVisualMetrics visual = GetHitVisual(point);
            if (visual != null)
            {
                VisualHitInfo info = visual.GetVisualHitInfo(point);
                AddVisualMetrics(info);
            }
        }

        private void RemoveVisualHitInfo()
        {
            AbstractVisualMetrics visual = m_Visuals.FirstOrDefault(x => x is VisualHitInfo);
            if (visual == null) return;

            base.RemoveVisualChild(visual);
            base.RemoveLogicalChild(visual);
            m_Visuals.Remove(visual);
        }

        private AbstractVisualMetrics GetHitVisual(Point point)
        {
            HitTestResult hitResult = VisualTreeHelper.HitTest(this, point);
            return (hitResult != null) ? hitResult.VisualHit as AbstractVisualMetrics : null;
        }
    }
}
