// =============================================================================
// 文件名：UpdateWindow.xaml.cs
// 功能描述：升级更新窗口的后台代码。与 UpdateWindow.xaml 配套，作为 View 的
//           partial 类，负责初始化 XAML 组件。订阅 Closing 事件实现强制更新态下的
//           关闭拦截：强制更新态下用户直接关闭窗口时弹提示——"确认开始更新"或"关闭软件退出"。
//           业务逻辑由对应的 ViewModel 通过数据绑定驱动。
// =============================================================================
using System.ComponentModel;
using System.Windows;
using IMDP.Presentation.ViewModels;

namespace IMDP.Presentation.Views;

/// <summary>
/// 升级更新窗口。
/// <para>展示版本信息、升级技术方案、架构示意与技术实现要点，</para>
/// <para>并提供立即更新与稍后提醒操作。</para>
/// <para>强制更新态（IsForceUpdate=true 且未跳过）下，关闭窗口会拦截并弹确认提示。</para>
/// </summary>
public partial class UpdateWindow : Window
{
    /// <summary>
    /// 初始化 <see cref="UpdateWindow"/> 类的新实例，并加载 XAML 组件。
    /// </summary>
    public UpdateWindow()
    {
        InitializeComponent();
        Closing += UpdateWindow_Closing;
    }

    /// <summary>
    /// 窗口关闭事件处理
    /// <para>强制更新态（IsForceUpdate=true 且未跳过）下拦截关闭，弹确认提示：</para>
    /// <para>"是"（确认开始更新）→ 取消关闭 → 触发立即更新命令；</para>
    /// <para>"否"（关闭软件退出）→ 允许关闭 → 退出整个应用。</para>
    /// <para>正在下载/安装时不允许关闭。</para>
    /// </summary>
    private void UpdateWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not UpdateViewModel vm) return;

        // 正在下载/安装中，不允许关闭
        if (vm.IsDownloading)
        {
            e.Cancel = true;
            return;
        }

        // 已跳过更新或已完成更新下载，允许正常关闭
        if (vm.IsSkipped || vm.IsUpdateCompleted) return;

        // 非强制更新态（VersionView 点击触发），允许正常关闭
        if (!vm.IsForceUpdate) return;

        // 强制更新态但未检测到版本信息（如手动检查更新时服务器无新版本），允许正常关闭
        if (string.IsNullOrEmpty(vm.LatestVersion)) return;

        // 强制更新态：拦截关闭，弹确认提示
        e.Cancel = true;
        var result = MessageBox.Show(
            "当前版本与服务器版本不匹配，必须进行更新。\n\n点击\"是\"开始更新，点击\"否\"关闭软件直接退出。",
            "强制更新", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            // 确认开始更新：触发立即更新命令
            vm.UpdateCommand.Execute(null);
        }
        else
        {
            // 关闭软件退出
            System.Windows.Application.Current.Shutdown();
        }
    }
}