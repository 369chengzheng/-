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
