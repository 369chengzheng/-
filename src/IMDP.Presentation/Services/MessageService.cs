// =============================================================================
// 文件名：MessageService.cs
// 功能描述：IMessageService 的 WPF 实现。
//           注意：本文件对所有 WPF 类型使用完全限定名（System.Windows.Xxx），
//           因为 IMDP.Application 命名空间的存在会导致 Application / MessageBox
//           标识符解析歧义（命名空间优先于 using 导入的类型）。
// =============================================================================
using IMDP.Application.Services;
using NLog;

namespace IMDP.Presentation.Services;

/// <summary>
/// 基于 WPF MessageBox 的用户消息提示服务实现
/// <para>所有提示统一经由 <see cref="Show"/> 切换到 UI 线程执行，
/// 因此可安全地从后台任务/串口接收回调中调用。</para>
/// </summary>
public class MessageService : IMessageService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    /// <inheritdoc />
    public void ShowInfo(string message, string title = "提示")
        => Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    /// <inheritdoc />
    public void ShowWarning(string message, string title = "警告")
        => Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

    /// <inheritdoc />
    public void ShowDuplicateEquipmentCodeWarning(string message, string title = "表号重复")
    {
        // 表号重复属于输入错误：使用 HandyControl 风格 MessageBox（与主界面主题一致）模态提示，
        // 用户确认后才能继续，避免重复表号被静默忽略
        ShowHandyMessageBoxOnUi(message, title);
    }

    /// <inheritdoc />
    public void ShowError(string message, string title = "错误")
    {
        // 打通日志与提示：错误弹窗自动落 NLog
        _logger.Error($"[UI提示] {title}：{message}");
        Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }

    /// <inheritdoc />
    public bool ShowConfirm(string message, string title = "确认")
        => Show(message, title, System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question)
           == System.Windows.MessageBoxResult.OK;

    /// <inheritdoc />
    public void GrowlInfo(string message)
        => ShowGrowlOnUi(() => HandyControl.Controls.Growl.Info(message));

    /// <inheritdoc />
    public void GrowlSuccess(string message)
        => ShowGrowlOnUi(() => HandyControl.Controls.Growl.Success(message));

    /// <inheritdoc />
    public void GrowlWarning(string message)
        => ShowGrowlOnUi(() => HandyControl.Controls.Growl.Warning(message));

    /// <inheritdoc />
    public void GrowlConfirm(string message, Action<bool> callback)
        => ShowGrowlOnUi(() => HandyControl.Controls.Growl.Ask(message, isConfirmed =>
        {
            // Growl 按钮点击回调本身在 UI 线程执行，直接分发选择结果
            callback(isConfirmed);
            return true; // 返回 true 表示允许关闭 Growl
        }));

    /// <summary>
    /// 在 UI 线程上执行 Growl 轻提示（后台线程调用时自动调度回 UI 线程）。
    /// </summary>
    /// <param name="show">Growl 显示动作。</param>
    private static void ShowGrowlOnUi(System.Action show)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            show();
        }
        else
        {
            app.Dispatcher.Invoke(show);
        }
    }

    /// <summary>
    /// 在 UI 线程上显示 HandyControl 风格 MessageBox（后台线程调用时自动调度回 UI 线程）。
    /// </summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">提示标题。</param>
    private static void ShowHandyMessageBoxOnUi(string message, string title)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
        {
            return;
        }

        void Show() => HandyControl.Controls.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);

        if (app.Dispatcher.CheckAccess())
        {
            Show();
        }
        else
        {
            app.Dispatcher.Invoke(Show);
        }
    }

    /// <summary>
    /// 统一执行 MessageBox 显示，并在非 UI 线程调用时切回 UI 线程。
    /// </summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">提示标题。</param>
    /// <param name="button">按钮组合。</param>
    /// <param name="image">图标类型。</param>
    /// <returns>用户点击结果；无 WPF 应用实例时返回 None。</returns>
    private static System.Windows.MessageBoxResult Show(
        string message,
        string title,
        System.Windows.MessageBoxButton button,
        System.Windows.MessageBoxImage image)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
            return System.Windows.MessageBoxResult.None;

        if (app.Dispatcher.CheckAccess())
            return System.Windows.MessageBox.Show(message, title, button, image);

        return app.Dispatcher.Invoke(() => System.Windows.MessageBox.Show(message, title, button, image));
    }
}
