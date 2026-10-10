// =============================================================================
// 文件名：MainWindow.xaml.cs
// 功能描述：主窗口的后台代码。与 MainWindow.xaml 配套，作为 View 的 partial 类，
//           负责初始化 XAML 组件及窗口级交互逻辑：全屏切换、窗口最小化/最大化/关闭、
//           菜单弹窗鼠标移出关闭等。菜单项导航、登录状态、工位/工号等信息展示
//           由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.Windows;
using System.Windows.Input;
using IMDP.Presentation.ViewModels;

namespace IMDP.Presentation.Views;

/// <summary>
/// 主窗口。
/// <para>应用程序主窗口，采用无边框自定义样式，包含顶部标题栏（菜单按钮、标题、</para>
/// <para>最小化/最大化/关闭按钮）、主内容区（Prism 区域注入）与底部状态栏</para>
/// <para>（工位、工号、登录时间、退出登录）。菜单弹窗提供功能模块导航入口。</para>
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>是否处于全屏状态。</summary>
    private bool _isFullScreen;

    /// <summary>
    /// 初始化 <see cref="MainWindow"/> 类的新实例，加载 XAML 组件并订阅 Loaded 事件。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// 窗口加载完成事件处理。暂时注释霸屏显示，方便调试。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 暂时注释霸屏显示，方便调试
        // EnterFullScreen();
    }

    /// <summary>
    /// 进入全屏模式：窗口铺满主屏幕并置顶。
    /// </summary>
    private void EnterFullScreen()
    {
        if (_isFullScreen) return;
        _isFullScreen = true;

        WindowState = WindowState.Normal;
        Left = 0;
        Top = 0;
        Width = SystemParameters.FullPrimaryScreenWidth;
        Height = SystemParameters.FullPrimaryScreenHeight;
        Topmost = true;
        WindowStyle = WindowStyle.None;
    }

    /// <summary>
    /// 退出全屏模式：恢复默认窗口尺寸（1280x720）并居中显示。
    /// </summary>
    private void ExitFullScreen()
    {
        _isFullScreen = false;
        Topmost = false;
        WindowState = WindowState.Normal;
        Width = 1280;
        Height = 720;
        Left = (SystemParameters.FullPrimaryScreenWidth - 1280) / 2;
        Top = (SystemParameters.FullPrimaryScreenHeight - 720) / 2;
    }

    /// <summary>
    /// 最小化按钮点击事件处理：将窗口最小化至任务栏。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// 最大化按钮点击事件处理：在全屏与正常状态之间切换。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullScreen)
        {
            ExitFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    /// <summary>
    /// 关闭按钮点击事件处理：关闭主窗口。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 菜单弹窗鼠标移出事件处理：当 DataContext 为 <see cref="MainWindowViewModel"/> 时
    /// 关闭菜单弹窗。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标事件参数。</param>
    private void MenuPopup_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsMenuPopupOpen = false;
        }
    }
}