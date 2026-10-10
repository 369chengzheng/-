// =============================================================================
// 文件名：VersionViewModel.cs
// 功能描述：版本信息视图模型（4.5 版本迭代页）。
//           展示当前应用版本号与历史版本迭代记录（从服务器 version.xml 清单动态加载，
//           按版本号降序排列）。
//           支持点击版本号直接触发 AutoUpdater.NET 下载界面：弹确认提示后，
//           构造 UpdateInfoEventArgs 并调用 AutoUpdater.DownloadUpdate 下载对应版本的 zip 包。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using AutoUpdaterDotNET;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Events;
using IMDP.Application.Services;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 版本信息视图模型（4.5 版本迭代页）
/// <para>展示当前版本号与历史版本记录；版本记录从服务器 version.xml 清单动态加载，
/// 按版本号降序排列（最新版本在最上方）。</para>
/// <para>支持点击版本号直接触发 AutoUpdater.NET 下载界面：弹确认提示后，
/// 构造 UpdateInfoEventArgs 并调用 AutoUpdater.DownloadUpdate 下载对应版本的 zip 包，
/// 不再经过 UpdateWindow 中转。</para>
/// </summary>
public partial class VersionViewModel : ObservableObject, INavigationAware
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IUpdateService _updateService;

    /// <summary>版本迭代记录（从服务器 version.xml 清单动态加载，按版本号降序排列）</summary>
    public ObservableCollection<VersionRecordDto> VersionRecords { get; } = new();

    /// <summary>返回主页命令</summary>
    public IRelayCommand GoHomeCommand { get; }

    /// <summary>
    /// 点击版本号触发升级更新命令
    /// <para>参数为被点击的 <see cref="VersionRecordDto"/>，其 DownloadUrl 字段指向该版本更新包，
    /// 直接调用 AutoUpdater.DownloadUpdate 下载对应 zip 包。</para>
    /// </summary>
    public IRelayCommand<VersionRecordDto?> SelectVersionCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventAggregator">Prism 事件聚合器（用于发布返回主页事件）</param>
    /// <param name="updateService">应用更新服务（用于从服务器加载版本清单）</param>
    private readonly IMessageService _messageService;
    public VersionViewModel(IEventAggregator eventAggregator, IUpdateService updateService,
        IMessageService messageService)
    {
        _messageService = messageService;
        _eventAggregator = eventAggregator;
        _updateService = updateService;
        GoHomeCommand = new RelayCommand(OnGoHome);
        SelectVersionCommand = new RelayCommand<VersionRecordDto?>(OnSelectVersion);
    }

    /// <summary>
    /// 点击版本号直接触发 AutoUpdater.NET 下载界面
    /// <para>弹确认提示"是否更新到版本 XXX？"→ 用户确认后构造 UpdateInfoEventArgs
    /// （填入被点击版本的 DownloadUrl、CurrentVersion、InstalledVersion）→
    /// 直接调用 AutoUpdater.DownloadUpdate 弹下载窗下载该版本 zip 包。</para>
    /// <para>下载完成后主动退出主进程（ZipExtractor/安装程序会接管解压与重启），
    /// 否则安装流程会被卡住。下载失败或用户取消则停留当前界面。</para>
    /// <para>用户取消则不执行任何操作。</para>
    /// </summary>
    /// <param name="record">被点击的版本记录（其 DownloadUrl 字段指向该版本更新包）</param>
    private async void OnSelectVersion(VersionRecordDto? record)
    {
        if (record == null) return;

        if (string.IsNullOrEmpty(record.DownloadUrl))
        {
            _messageService.ShowWarning($"版本 {record.Version} 的下载地址无效，无法更新。", "更新失败");
            return;
        }

        // 先弹确认提示
        if (!_messageService.ShowConfirm(
            $"是否更新到版本 {record.Version}？",
            "确认更新")) return;

        try
        {
            // 启用错误报告，让 AutoUpdater 内部异常弹出提示
            AutoUpdater.ReportErrors = true;

            // 构造 UpdateInfoEventArgs，填入被点击版本的具体信息
            var currentVerStr = _updateService.GetCurrentVersion().TrimStart('V', 'v');
            System.Version.TryParse(currentVerStr, out var installedVer);

            var args = new UpdateInfoEventArgs
            {
                CurrentVersion = record.Version.TrimStart('V', 'v'),
                DownloadURL = record.DownloadUrl,
                ChangelogURL = string.Empty,
                InstalledVersion = installedVer,
                Mandatory = new AutoUpdaterDotNET.Mandatory { Value = false }
            };

            // 直接调 DownloadUpdate 弹下载窗（同步阻塞，下载完成后返回）
            AutoUpdater.SetOwner(System.Windows.Application.Current.MainWindow);
            var success = AutoUpdater.DownloadUpdate(args);

            if (!success)
            {
                _messageService.ShowWarning($"版本 {record.Version} 下载失败，请重试。", "更新失败");
                return;
            }

            try { await _updateService.UpdateVersionXmlAsync(record.Version, record.ReleaseNotes, record.DownloadUrl, record.ReleaseDate.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }

            // 下载完成且 ZipExtractor/安装程序已启动：主动退出主进程，
            // 否则 ZipExtractor 等待主程序退出会卡住升级流程（与 AutoUpdater.Exit 行为一致）
            System.Windows.Application.Current.Shutdown();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"更新失败: {ex.Message}", "更新失败");
        }
    }

    /// <summary>
    /// 返回主页处理
    /// </summary>
    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;
    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext) { }
    /// <inheritdoc/>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = LoadVersionHistoryAsync();
    }

    /// <summary>
    /// 从服务器 version.xml 清单异步加载全部版本记录
    /// <para>清空现有列表后，按版本号降序填充 VersionRecords。网络异常或解析失败时列表为空，
    /// 不影响应用正常运行。</para>
    /// </summary>
    private async Task LoadVersionHistoryAsync()
    {
        var versions = await _updateService.GetVersionHistoryAsync();
        VersionRecords.Clear();
        foreach (var v in versions)
        {
            VersionRecords.Add(new VersionRecordDto
            {
                Version = v.Version,
                ReleaseDate = v.ReleaseDate,
                ReleaseNotes = v.ReleaseNotes,
                DownloadUrl = v.DownloadUrl
            });
        }
    }
}
