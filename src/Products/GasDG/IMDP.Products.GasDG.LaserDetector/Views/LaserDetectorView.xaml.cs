// =============================================================================
// 文件名：LaserDetectorView.xaml.cs
// 功能描述：激光检测仪校验界面后台代码。
//           ViewModel 通过 Prism ViewModelLocator 自动装配；
//           后台负责将界面事件（串口下拉刷新、产品编码失焦、工位扫码）
//           转发到 ViewModel 对应命令，并处理扫码枪回车触发检测；
//           另含配置预览面板的折叠/展开动画（与配置界面交互一致）。
// =============================================================================
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using IMDP.Products.GasDG.LaserDetector.Models;
using IMDP.Products.GasDG.LaserDetector.ViewModels;

namespace IMDP.Products.GasDG.LaserDetector.Views;

/// <summary>
/// 激光检测仪校验界面
/// <para>多工位并行检测：扫码触发自动测试，人工测试项判定后综合评估结果；
/// 顶部常态化展示已启用配置项（只读，未启用项整行收起不占位），可折叠收起。</para>
/// </summary>
public partial class LaserDetectorView : UserControl
{
    /// <summary>配置预览面板折叠后的高度</summary>
    private const double CollapsedPanelHeight = 0;

    /// <summary>
    /// 初始化 <see cref="LaserDetectorView"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public LaserDetectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 获取当前绑定的视图模型（XAML 装配后非空）
    /// </summary>
    private LaserDetectorViewModel? ViewModel => DataContext as LaserDetectorViewModel;

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

    /// <summary>
    /// 串口下拉展开时刷新可用串口列表
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void PortComboBox_DropDownOpened(object sender, System.EventArgs e)
    {
        ViewModel?.RefreshPortsCommand.Execute(null);
    }

    /// <summary>
    /// 产品编码失焦时按编码加载检测方案
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void ProductCodeTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel?.ProductLostFocusCommand.Execute(null);
    }

    /// <summary>
    /// 产品编码输入框回车/空格触发加载方案（扫码枪结束符或手工确认）
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">键盘事件参数</param>
    private void ProductCodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Space)
        {
            return;
        }

        e.Handled = true;
        ViewModel?.ProductLostFocusCommand.Execute(null);
    }

    /// <summary>
    /// 工位条码框失焦时触发该工位自动检测
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">路由事件参数</param>
    private void BarcodeTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: LaserDetectorStationState state })
        {
            return;
        }

        ViewModel?.StationScanCommand.Execute(state.Station);
    }

    /// <summary>
    /// 条码框内按回车（扫码枪结束符）时触发该工位自动检测
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">键盘事件参数</param>
    private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            sender is not TextBox { DataContext: LaserDetectorStationState state })
        {
            return;
        }

        e.Handled = true;
        ViewModel?.StationScanCommand.Execute(state.Station);
    }
}
