// =============================================================================
// 文件名：IMessageService.cs
// 功能描述：统一的用户消息/弹窗提示服务接口。
//           收敛各 ViewModel 中散落的 System.Windows.MessageBox.Show 调用，
//           使提示风格统一、可测试替换，并保证后台线程调用时的 UI 线程安全。
// =============================================================================
namespace IMDP.Application.Services;

/// <summary>
/// 用户消息提示服务接口
/// <para>由 Presentation 层基于 WPF MessageBox 实现，通过 DI 以单例注入，
/// 供 ViewModel 与产品插件使用。</para>
/// </summary>
public interface IMessageService
{
    /// <summary>显示信息提示。</summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">提示标题，默认“提示”。</param>
    void ShowInfo(string message, string title = "提示");

    /// <summary>显示警告提示。</summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">提示标题，默认“警告”。</param>
    void ShowWarning(string message, string title = "警告");

    /// <summary>显示错误提示（同时写入错误日志）。</summary>
    /// <param name="message">错误内容。</param>
    /// <param name="title">提示标题，默认“错误”。</param>
    void ShowError(string message, string title = "错误");

    /// <summary>
    /// 显示确认对话框（确定/取消）。
    /// </summary>
    /// <param name="message">确认内容。</param>
    /// <param name="title">提示标题，默认“确认”。</param>
    /// <returns>用户点击“确定”返回 true；否则返回 false。</returns>
    bool ShowConfirm(string message, string title = "确认");

    /// <summary>显示 Growl 信息轻提示（右上角滑出，自动消失）。</summary>
    /// <param name="message">提示内容。</param>
    void GrowlInfo(string message);

    /// <summary>显示 Growl 成功轻提示。</summary>
    /// <param name="message">提示内容。</param>
    void GrowlSuccess(string message);

    /// <summary>显示 Growl 警告轻提示。</summary>
    /// <param name="message">提示内容。</param>
    void GrowlWarning(string message);

    /// <summary>
    /// 显示 Growl 是/否确认轻提示（非阻塞，用户点击后经回调返回选择结果）。
    /// </summary>
    /// <param name="message">确认内容。</param>
    /// <param name="callback">选择回调（参数 true=确认，false=取消；在 UI 线程执行）。</param>
    void GrowlConfirm(string message, Action<bool> callback);
}
