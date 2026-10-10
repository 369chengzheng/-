// =============================================================================
// 文件名：LaserDetectorView.xaml.cs
// 功能描述：激光检测仪方案配置视图后台代码。
//           支持 Ctrl+鼠标滚轮整体缩放（0.5~3.0）与参数面板折叠/展开，
//           交互与 IMDS 系统 LaserDetectorView 保持一致。
// =============================================================================
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IMDP.Products.GasDG.LaserDetector.Views;

/// <summary>
/// 激光检测仪方案配置视图
/// <para>ViewModel 通过 Prism ViewModelLocator 自动装配；
/// 后台代码负责 Ctrl+滚轮缩放与参数面板折叠/展开动画。</para>
/// </summary>
public partial class LaserDetectorView : UserControl
{
    /// <summary>每次滚轮刻度的缩放增量</summary>
    private const double ZoomFactor = 0.05;

    /// <summary>最小缩放比例</summary>
    private const double MinZoom = 0.5;

    /// <summary>最大缩放比例</summary>
    private const double MaxZoom = 3.0;

    /// <summary>参数面板折叠后的高度</summary>
    private const double CollapsedPanelHeight = 0;

    /// <summary>参数面板展开后的固定高度（8 行 × 55px + 边距）</summary>
    private const double ExpandedPanelHeight = 470;

    /// <summary>
    /// 初始化视图组件并挂载滚轮缩放事件
    /// </summary>
    public LaserDetectorView()
    {
        InitializeComponent();
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    /// <summary>
    /// Ctrl+滚轮缩放处理：按住 Ctrl 滚动时整体放大/缩小内容，并标记事件已处理
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
    /// 折叠参数面板：高度动画收起后隐藏上箭头按钮、显示下箭头按钮
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void ButtonCloseMenu_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        AnimatePanelHeight(ExpandedPanelHeight, CollapsedPanelHeight);
        ButtonCloseMenu.Visibility = System.Windows.Visibility.Collapsed;
        ButtonOpenMenu.Visibility = System.Windows.Visibility.Visible;
    }

    /// <summary>
    /// 展开参数面板：高度动画展开后显示上箭头按钮、隐藏下箭头按钮
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void ButtonOpenMenu_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        AnimatePanelHeight(CollapsedPanelHeight, ExpandedPanelHeight);
        ButtonCloseMenu.Visibility = System.Windows.Visibility.Visible;
        ButtonOpenMenu.Visibility = System.Windows.Visibility.Collapsed;
    }

    /// <summary>
    /// 对参数面板执行高度过渡动画
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
