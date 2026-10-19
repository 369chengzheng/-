// =============================================================================
// 文件名：MainWindow.xaml.cs
// 功能描述：主窗口的后台代码。与 MainWindow.xaml 配套，作为 View 的 partial 类，
//           负责初始化 XAML 组件及窗口级交互逻辑：标题栏按住左键拖动移动窗口、
//           双击标题栏最大化/还原、窗口边缘 8 方向缩放、窗口最小化/最大化/还原/关闭、
//           菜单弹窗鼠标移出关闭等。窗口不使用 WindowChrome，无边框能力由 Win32
//           消息钩子实现：WM_NCHITTEST（边缘缩放命中测试）、WM_NCCALCSIZE（去除
//           系统边框占位）、WM_GETMINMAXINFO（最大化约束到工作区，不遮挡任务栏）。
//           菜单项导航、登录状态、工位/工号等信息展示由对应的 ViewModel 驱动。
// =============================================================================
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using IMDP.Presentation.ViewModels;

namespace IMDP.Presentation.Views;

/// <summary>
/// 主窗口。
/// <para>应用程序主窗口，采用无边框自定义样式（不使用 WindowChrome），包含顶部标题栏</para>
/// <para>（菜单按钮、标题、最小化/最大化/关闭按钮）、主内容区（Prism 区域注入）与底部</para>
/// <para>状态栏（工位、工号、登录时间、退出登录）。标题栏可按住鼠标左键拖动移动窗口、</para>
/// <para>双击最大化/还原；鼠标移到窗口边缘可 8 方向拖拽缩放窗口。</para>
/// </summary>
public partial class MainWindow : Window
{
    // ==================== Win32 常量 ====================

    /// <summary>非客户区命中测试消息：返回鼠标所处的窗口部位（客户区/缩放边框等）。</summary>
    private const int WM_NCHITTEST = 0x0084;

    /// <summary>非客户区尺寸计算消息：此处用于去除系统边框占位，使客户区为整个窗口。</summary>
    private const int WM_NCCALCSIZE = 0x0083;

    /// <summary>窗口尺寸约束消息：最大化时将窗口约束到屏幕工作区（不遮挡任务栏）。</summary>
    private const int WM_GETMINMAXINFO = 0x0024;

    /// <summary>命中测试结果：客户区。</summary>
    private const int HTCLIENT = 1;

    /// <summary>命中测试结果：左边框（向右缩放）。</summary>
    private const int HTLEFT = 10;

    /// <summary>命中测试结果：右边框（向左缩放）。</summary>
    private const int HTRIGHT = 11;

    /// <summary>命中测试结果：上边框（向下缩放）。</summary>
    private const int HTTOP = 12;

    /// <summary>命中测试结果：左上角（对角缩放）。</summary>
    private const int HTTOPLEFT = 13;

    /// <summary>命中测试结果：右上角（对角缩放）。</summary>
    private const int HTTOPRIGHT = 14;

    /// <summary>命中测试结果：下边框（向上缩放）。</summary>
    private const int HTBOTTOM = 15;

    /// <summary>命中测试结果：左下角（对角缩放）。</summary>
    private const int HTBOTTOMLEFT = 16;

    /// <summary>命中测试结果：右下角（对角缩放）。</summary>
    private const int HTBOTTOMRIGHT = 17;

    /// <summary>GetWindowLong 索引：窗口样式。</summary>
    private const int GWL_STYLE = -16;

    /// <summary>窗口样式：可调边框（缺失时系统不会进入缩放循环，边缘缩放无效）。</summary>
    private const long WS_THICKFRAME = 0x00040000;

    /// <summary>窗口样式：最大化能力（保证系统最大化行为正常）。</summary>
    private const long WS_MAXIMIZEBOX = 0x00010000;

    /// <summary>窗口样式：最小化能力（保证任务栏最小化行为正常）。</summary>
    private const long WS_MINIMIZEBOX = 0x00020000;

    /// <summary>MonitorFromWindow 标志：取距离窗口最近的显示器。</summary>
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>边缘可缩放区域厚度（设备无关单位，实际按 DPI 换算为物理像素）。</summary>
    private const double ResizeEdgeThickness = 6.0;

    // ==================== Win32 结构体 ====================

    /// <summary>屏幕坐标点（物理像素）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        /// <summary>X 坐标。</summary>
        public int X;

        /// <summary>Y 坐标。</summary>
        public int Y;
    }

    /// <summary>矩形（物理像素）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        /// <summary>左边界。</summary>
        public int Left;

        /// <summary>上边界。</summary>
        public int Top;

        /// <summary>右边界。</summary>
        public int Right;

        /// <summary>下边界。</summary>
        public int Bottom;
    }

    /// <summary>窗口最大化/最小化尺寸约束信息。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        /// <summary>保留字段。</summary>
        public POINT Reserved;

        /// <summary>最大化时的尺寸。</summary>
        public POINT MaxSize;

        /// <summary>最大化时的位置。</summary>
        public POINT MaxPosition;

        /// <summary>最小追踪尺寸（允许的最小尺寸）。</summary>
        public POINT MinTrackSize;

        /// <summary>最大追踪尺寸（允许的最大尺寸）。</summary>
        public POINT MaxTrackSize;
    }

    /// <summary>显示器信息。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        /// <summary>结构体大小（调用前必须赋值）。</summary>
        public int Size;

        /// <summary>显示器整个区域的矩形。</summary>
        public RECT Monitor;

        /// <summary>显示器工作区域的矩形（排除任务栏等停靠栏）。</summary>
        public RECT Work;

        /// <summary>显示器标志位。</summary>
        public int Flags;
    }

    // ==================== Win32 函数导入 ====================

    /// <summary>获取窗口样式等属性。</summary>
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>设置窗口样式等属性。</summary>
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>获取窗口边界矩形（物理像素，屏幕坐标）。</summary>
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>获取指定窗口所在的显示器句柄。</summary>
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    /// <summary>获取显示器信息（含工作区域）。</summary>
    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// 初始化 <see cref="MainWindow"/> 类的新实例，加载 XAML 组件。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 窗口句柄初始化完成：补充系统缩放所需的窗口样式位，并挂接窗口消息钩子
    /// （WM_NCCALCSIZE / WM_NCHITTEST / WM_GETMINMAXINFO），替代 WindowChrome
    /// 实现无边框窗口的边缘缩放与最大化约束。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        EnsureResizableWindowStyle(handle);
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    /// <summary>
    /// 窗口消息钩子：拦截并分发需要自定义处理的窗口消息，其余交给系统默认处理。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="msg">消息 ID。</param>
    /// <param name="wParam">消息参数一。</param>
    /// <param name="lParam">消息参数二。</param>
    /// <param name="handled">是否已处理（置 true 后系统不再默认处理）。</param>
    /// <returns>消息处理结果：命中测试返回命中码，其余已处理消息返回零。</returns>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCCALCSIZE when wParam != IntPtr.Zero:
                // 返回 0 表示整个窗口都是客户区，去除系统边框占位
                handled = true;
                return IntPtr.Zero;

            case WM_NCHITTEST:
                handled = true;
                return new IntPtr(HandleNcHitTest(hwnd, lParam));

            case WM_GETMINMAXINFO:
                HandleGetMinMaxInfo(hwnd, lParam);
                handled = true;
                return IntPtr.Zero;

            default:
                return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 为无边框窗口补充 WS_THICKFRAME / WS_MAXIMIZEBOX / WS_MINIMIZEBOX 样式位。
    /// <para>AllowsTransparency 无边框窗口默认缺少这些样式位，导致系统不进入缩放循环、</para>
    /// <para>边缘缩放与任务栏最小化行为异常，故需在句柄创建后显式补齐。</para>
    /// </summary>
    /// <param name="handle">窗口句柄。</param>
    private static void EnsureResizableWindowStyle(IntPtr handle)
    {
        int style = GetWindowLong(handle, GWL_STYLE);
        int desired = style | (int)(WS_THICKFRAME | WS_MAXIMIZEBOX | WS_MINIMIZEBOX);
        if (desired != style)
        {
            SetWindowLong(handle, GWL_STYLE, desired);
        }
    }

    /// <summary>
    /// 处理 WM_NCHITTEST：鼠标位于窗口边缘（约 6 逻辑像素，随 DPI 换算）时返回
    /// 对应方向的缩放命中码，由系统接管 8 方向缩放拖拽；其余位置返回客户区。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="lParam">鼠标屏幕物理坐标（低 16 位为 X，高 16 位为 Y）。</param>
    /// <returns>命中码（HT 开头常量）。</returns>
    private int HandleNcHitTest(IntPtr hwnd, IntPtr lParam)
    {
        // 非正常状态（最大化/最小化）或取矩形失败时不允许缩放
        if (WindowState != WindowState.Normal || !GetWindowRect(hwnd, out RECT rect))
        {
            return HTCLIENT;
        }

        long value = lParam.ToInt64();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));

        // 边缘厚度由逻辑单位按当前 DPI 换算为物理像素，最小 4 像素保证可命中
        double dpiScale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int edge = Math.Max(4, (int)Math.Ceiling(ResizeEdgeThickness * dpiScale));

        bool onLeft = x >= rect.Left && x < rect.Left + edge;
        bool onRight = x < rect.Right && x >= rect.Right - edge;
        bool onTop = y >= rect.Top && y < rect.Top + edge;
        bool onBottom = y < rect.Bottom && y >= rect.Bottom - edge;

        // 先判断角落（对角缩放），再判断四条边，最后默认客户区
        if (onTop && onLeft)
        {
            return HTTOPLEFT;
        }
        if (onTop && onRight)
        {
            return HTTOPRIGHT;
        }
        if (onBottom && onLeft)
        {
            return HTBOTTOMLEFT;
        }
        if (onBottom && onRight)
        {
            return HTBOTTOMRIGHT;
        }
        if (onLeft)
        {
            return HTLEFT;
        }
        if (onRight)
        {
            return HTRIGHT;
        }
        if (onTop)
        {
            return HTTOP;
        }
        if (onBottom)
        {
            return HTBOTTOM;
        }
        return HTCLIENT;
    }

    /// <summary>
    /// 处理 WM_GETMINMAXINFO：无边框窗口最大化时默认会铺满整个屏幕并遮挡任务栏，
    /// 此处将最大化尺寸与位置约束为窗口所在显示器的工作区域（多显示器场景取最近屏）。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="lParam">指向 MINMAXINFO 结构的指针。</param>
    private void HandleGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        MINMAXINFO info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };

        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
        {
            // 最大化尺寸 = 工作区大小；位置以显示器原点为基准（多显示器下依然正确）
            info.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
            info.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
            info.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
            info.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
            Marshal.StructureToPtr(info, lParam, true);
        }
    }

    /// <summary>
    /// 标题栏鼠标左键按下事件处理：按住左键拖动移动窗口；双击最大化/还原。
    /// <para>标题栏内的按钮（菜单/最小化/最大化/关闭）自行处理鼠标事件，不会触发本方法。</para>
    /// </summary>
    /// <param name="sender">事件源（标题栏）。</param>
    /// <param name="e">鼠标按钮事件参数。</param>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击标题栏：最大化/还原
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed && WindowState == WindowState.Normal)
        {
            try
            {
                // 按住左键拖动：进入系统窗口移动循环
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 按下与调用之间鼠标极快松开时 DragMove 会抛异常，忽略即可
            }
        }
    }

    /// <summary>
    /// 在最大化与还原之间切换窗口状态。
    /// </summary>
    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
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
    /// 最大化/还原按钮点击事件处理：在最大化与还原（之前尺寸和位置）之间切换。
    /// <para>最大化范围由 WM_GETMINMAXINFO 约束为屏幕工作区，不遮挡任务栏。</para>
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
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
