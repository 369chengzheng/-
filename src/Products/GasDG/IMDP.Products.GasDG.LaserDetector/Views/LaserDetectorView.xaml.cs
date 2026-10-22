// =============================================================================
// 文件名：LaserDetectorView.xaml.cs
// 功能描述：激光检测仪校验界面后台代码（严格 MVVM）。
//           仅保留三类纯视图行为：Ctrl+鼠标滚轮整体缩放（0.5~3.0）、
//           配置预览面板折叠/展开动画、秒级耗时刷新计时器（UI 计时归视图，
//           驱动 VM 的 RefreshDurationsCommand）。业务交互（扫码、失焦加载、
//           串口刷新）已全部由 XAML 绑定/附加行为直达 ViewModel 命令，
//           本文件不含事件转发胶水代码。
// =============================================================================
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using IMDP.Products.GasDG.LaserDetector.ViewModels;

namespace IMDP.Products.GasDG.LaserDetector.Views;

/// <summary>
/// 激光检测仪校验界面
/// <para>多工位并行检测：扫码触发自动测试，人工测试项判定后综合评估结果；
/// 顶部常态化展示已启用配置项（只读，未启用项整行收起不占位），可折叠收起；
/// Ctrl+鼠标滚轮整体缩放内容区（0.5~3.0，导航条固定不缩放）。</para>
/// </summary>
public partial class LaserDetectorView : UserControl
{
    /// <summary>每次滚轮刻度的缩放增量</summary>
    private const double ZoomFactor = 0.05;

    /// <summary>最小缩放比例</summary>
    private const double MinZoom = 0.5;

    /// <summary>最大缩放比例</summary>
    private const double MaxZoom = 3.0;

    /// <summary>配置预览面板折叠后的高度</summary>
    private const double CollapsedPanelHeight = 0;

    /// <summary>秒级耗时刷新计时器（视图加载期间常驻，驱动 VM 刷新命令）</summary>
    private System.Windows.Threading.DispatcherTimer? _durationTimer;

    /// <summary>
    /// 初始化 <see cref="LaserDetectorView"/> 类的新实例，加载 XAML 组件并挂接生命周期
    /// </summary>
    public LaserDetectorView()
    {
        InitializeComponent();
        PreviewMouseWheel += OnPreviewMouseWheel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 获取当前绑定的视图模型（XAML 装配后非空）
    /// </summary>
    private LaserDetectorViewModel? ViewModel => DataContext as LaserDetectorViewModel;

    /// <summary>
    /// 视图加载：启动秒级计时器，每秒驱动 VM 刷新进行中工位的耗时文本
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _durationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += (_, _) => ViewModel?.RefreshDurationsCommand.Execute(null);
        _durationTimer.Start();
    }

    /// <summary>
    /// 视图卸载：停止计时器（Prism 区域导航保留视图实例，重复进入时重新启动）
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _durationTimer?.Stop();
        _durationTimer = null;
    }

    /// <summary>
    /// Ctrl+滚轮缩放处理：按住 Ctrl 滚动时整体放大/缩小内容区，并标记事件已处理
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">滚轮事件参数</param>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        double zoom = e.Delta > 0 ? ZoomFactor : -ZoomFactor;
        double newScale = Math.Clamp(ScaleTransform.ScaleX + zoom, MinZoom, MaxZoom);
        ScaleTransform.ScaleX = newScale;
        ScaleTransform.ScaleY = newScale;
        e.Handled = true;
    }

    /// <summary>
    /// 折叠配置预览面板：高度动画收起后隐藏上箭头按钮、显示下箭头按钮
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void ButtonCloseMenu_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        AnimatePanelHeight(GridMenu.ActualHeight, CollapsedPanelHeight);
        ButtonCloseMenu.Visibility = System.Windows.Visibility.Collapsed;
        ButtonOpenMenu.Visibility = System.Windows.Visibility.Visible;
    }

    /// <summary>
    /// 展开配置预览面板：高度动画展开后显示上箭头按钮、隐藏下箭头按钮
    /// <para>展开目标高度需动态测量：方案启停项不同，面板内容高度随可见行数变化，
    /// 先取消动画并清除高度约束得到自然高度，再从 0 动画到该高度。</para>
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void ButtonOpenMenu_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // 取消进行中的动画并清除高度约束，让面板按内容自然测量
        GridMenu.BeginAnimation(System.Windows.Controls.Panel.HeightProperty, null);
        GridMenu.Height = double.NaN;

        // 以正无限高约束重新测量，得到当前内容自然高度
        GridMenu.Measure(new System.Windows.Size(GridMenu.ActualWidth, double.PositiveInfinity));
        double targetHeight = GridMenu.DesiredSize.Height;

        AnimatePanelHeight(CollapsedPanelHeight, targetHeight);
        ButtonCloseMenu.Visibility = System.Windows.Visibility.Visible;
        ButtonOpenMenu.Visibility = System.Windows.Visibility.Collapsed;
    }

    /// <summary>
    /// 对配置预览面板执行高度过渡动画（0.3 秒缓出）
    /// </summary>
    /// <param name="from">起始高度</param>
    /// <param name="to">目标高度</param>
    private void AnimatePanelHeight(double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromSeconds(0.3),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        GridMenu.BeginAnimation(System.Windows.Controls.Panel.HeightProperty, animation);
    }
}
