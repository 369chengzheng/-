// =============================================================================
// 文件名：UpdateViewModel.cs
// 功能描述：应用更新视图模型（4.2 升级更新窗口）。
//           负责检查最新版本、显示客户端机器名/IP、免更新密钥验证、
//           下载并安装更新包，并通过 IUpdateService 记录更新过程的开始与完成状态。
//           下载/安装由 AutoUpdater.NET 的 DownloadUpdate 方法完成（绕过 Start 的 Running 陷阱）。
// =============================================================================
using System.Net;
using System.Windows;
using AutoUpdaterDotNET;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IMDP.Application.Services;
using IMDP.Presentation.Views;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 应用更新视图模型（4.2）
/// <para>启动/手动检查时自动调用 IUpdateService.CheckUpdateAsync 获取最新版本信息并填充界面字段；
/// 提供当前版本号、最新版本号、更新内容展示，以及"立即更新"与"暂不更新"两个命令。</para>
/// <para>"立即更新"：记录更新开始（写库）→ 构造 UpdateInfoEventArgs 并调用 AutoUpdater.DownloadUpdate
/// 下载对应版本 zip 包 → 记录更新完成（成功/失败）。</para>
/// <para>"暂不更新"：弹岀免更新密钥输入框，密钥核对正确后关闭窗口跳过本次更新；
/// 密钥错误时提示用户重新输入或选择立即更新。</para>
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    /// <summary>应用更新服务（版本检查、免更新密钥校验、更新记录持久化）</summary>
    private readonly IUpdateService _updateService;

    /// <summary>检测到的版本信息（由 CheckUpdateAsync 填充）</summary>
    private VersionInfo? _versionInfo;

    /// <summary>更新记录主键 Id（更新开始时写入数据库）</summary>
    private long _updateRecordId;

    /// <summary>当前应用版本号</summary>
    [ObservableProperty]
    private string _currentVersion = "V1.0.0";

    /// <summary>服务端最新版本号</summary>
    [ObservableProperty]
    private string _latestVersion = string.Empty;

    /// <summary>最新版本发布说明（更新内容）</summary>
    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    /// <summary>下载进度（0~100）</summary>
    [ObservableProperty]
    private double _downloadProgress;

    /// <summary>是否正在下载/安装</summary>
    [ObservableProperty]
    private bool _isDownloading;

    /// <summary>状态消息（如"正在下载更新...""更新完成"）</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>客户端机器名（展示在窗口顶部）</summary>
    [ObservableProperty]
    private string _clientMachineName = string.Empty;

    /// <summary>客户端 IPv4 地址（展示在窗口顶部）</summary>
    [ObservableProperty]
    private string _clientIpAddress = string.Empty;

    /// <summary>是否显示免更新密钥输入区（用户点击"暂不更新"后置为 true）</summary>
    [ObservableProperty]
    private bool _isSkipKeyDialogVisible;

    /// <summary>用户输入的免更新密钥</summary>
    [ObservableProperty]
    private string _skipUpdateKeyInput = string.Empty;

    /// <summary>免更新密钥校验错误提示</summary>
    [ObservableProperty]
    private string _skipKeyErrorMessage = string.Empty;

    /// <summary>
    /// 是否强制更新态（默认 true，VersionView 点击版本号时显式设为 false）
    /// <para>强制更新态下：用户必须更新或输入免更新密钥；关闭窗口时弹拦截提示；</para>
    /// <para>非强制更新态下：用户可随时关闭窗口，无拦截。</para>
    /// </summary>
    [ObservableProperty]
    private bool _isForceUpdate = true;

    /// <summary>
    /// 是否已通过免更新密钥跳过本次更新
    /// <para>密钥正确后置为 true，允许窗口正常关闭而不触发拦截。</para>
    /// </summary>
    [ObservableProperty]
    private bool _isSkipped;

    /// <summary>
    /// 是否已成功完成更新包下载并准备重启退出
    /// <para>下载成功后置为 true，允许窗口正常关闭而不触发拦截。</para>
    /// </summary>
    [ObservableProperty]
    private bool _isUpdateCompleted;

    /// <summary>立即更新命令（触发下载/安装流程）</summary>
    public IRelayCommand UpdateCommand { get; }
    /// <summary>暂不更新命令（弹岀免更新密钥输入框）</summary>
    public IRelayCommand LaterCommand { get; }
    /// <summary>免更新密钥确认命令（校验密钥，正确则关闭窗口）</summary>
    public IRelayCommand ValidateSkipKeyCommand { get; }
    /// <summary>取消免更新密钥输入命令（返回版本信息展示态）</summary>
    public IRelayCommand CancelSkipKeyCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="updateService">应用更新服务</param>
    public UpdateViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
        UpdateCommand = new RelayCommand(OnUpdate);
        LaterCommand = new RelayCommand(OnLater);
        ValidateSkipKeyCommand = new RelayCommand(OnValidateSkipKey);
        CancelSkipKeyCommand = new RelayCommand(OnCancelSkipKey);

        // 填充客户端机器信息与当前版本号
        ClientMachineName = Environment.MachineName;
        ClientIpAddress = GetLocalIpAddress();
        CurrentVersion = _updateService.GetCurrentVersion();

        // 构造时即触发异步检查更新
        _ = CheckUpdateAsync();
    }

    /// <summary>
    /// 应用检测到的版本信息（由 App.xaml.cs 启动/退出检测时预填充）
    /// <para>IsForceUpdate 默认 true（强制更新态）。</para>
    /// </summary>
    /// <param name="versionInfo">检测到的版本信息</param>
    public void ApplyDetectedVersion(VersionInfo versionInfo)
    {
        _versionInfo = versionInfo;
        LatestVersion = versionInfo.Version;
        ReleaseNotes = versionInfo.ReleaseNotes;
    }

    /// <summary>
    /// 异步检查更新
    /// <para>自动检测服务器最新版本并填充界面字段；已由外部预填充（启动/退出检测）则不再重复请求。</para>
    /// </summary>
    private async Task CheckUpdateAsync()
    {
        // 已由外部预填充（启动/退出检测）则不再重复请求
        if (_versionInfo != null) return;

        var info = await _updateService.CheckUpdateAsync();
        if (info != null)
        {
            ApplyDetectedVersion(info);
        }
    }

    /// <summary>
    /// 立即更新处理
    /// <para>记录更新开始 → 构造 UpdateInfoEventArgs → 直接调用 AutoUpdater.DownloadUpdate
    /// 下载对应版本 zip 包并弹下载窗（同步阻塞）→ 记录更新完成（成功/失败）。</para>
    /// <para>改用 DownloadUpdate（替代 Start），绕过 Running 静态标志位陷阱、ReportErrors 吞错、
    /// Start 异步非阻塞导致时序错乱的问题。</para>
    /// <para>下载成功后无论数据库写记录是否成功，都必须退出主进程（否则 ZipExtractor 无法替换文件）。</para>
    /// </summary>
    private async void OnUpdate()
    {
        if (_versionInfo == null)
        {
            StatusMessage = "未检测到可用更新";
            return;
        }

        IsDownloading = true;
        IsSkipKeyDialogVisible = false;
        StatusMessage = "正在下载更新...";

        // 记录更新开始（写入数据库 AppUpdateRecord 表，便于后续审计）
        _updateRecordId = await _updateService.RecordUpdateStartAsync(_versionInfo, CurrentVersion);

        bool downloadSuccess = false;
        try
        {
            // 启用 AutoUpdater 错误报告，让下载失败/网络异常等错误弹出提示
            AutoUpdater.ReportErrors = true;
            AutoUpdater.SetOwner(System.Windows.Application.Current.MainWindow);

            // 构造 UpdateInfoEventArgs（从 _versionInfo 填充目标版本信息）
            var currentVerStr = _updateService.GetCurrentVersion().TrimStart('V', 'v');
            System.Version.TryParse(currentVerStr, out var installedVer);

            var args = new UpdateInfoEventArgs
            {
                CurrentVersion = _versionInfo.Version.TrimStart('V', 'v'),
                DownloadURL = _versionInfo.DownloadUrl,
                ChangelogURL = string.Empty,
                InstalledVersion = installedVer,
                Mandatory = new Mandatory { Value = false }
            };

            // DownloadUpdate 同步阻塞至下载窗关闭，绕过 Running 陷阱
            downloadSuccess = AutoUpdater.DownloadUpdate(args);
        }
        catch (Exception ex)
        {
            // 异常时记录失败原因（不阻断后续退出逻辑）
            try { await _updateService.RecordUpdateCompletedAsync(_updateRecordId, false, ex.Message); } catch { }
            StatusMessage = $"更新失败: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }

        // 下载成功：无论数据库写记录是否成功，都必须退出主进程让 ZipExtractor 替换文件
        if (downloadSuccess)
        {
            IsUpdateCompleted = true;
            try { await _updateService.RecordUpdateCompletedAsync(_updateRecordId, true); } catch { }
            try { await _updateService.UpdateVersionXmlAsync(_versionInfo.Version, _versionInfo.ReleaseNotes, _versionInfo.DownloadUrl, _versionInfo.ReleaseDate.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }
            StatusMessage = "更新已下载完成，应用即将退出以完成安装";
            CloseWindow();
            System.Windows.Application.Current.Shutdown();
            Environment.Exit(0);
        }
        else
        {
            try { await _updateService.RecordUpdateCompletedAsync(_updateRecordId, false, "下载未完成"); } catch { }
            StatusMessage = "更新未完成";
        }
    }

    /// <summary>
    /// 暂不更新处理：弹岀免更新密钥输入框
    /// </summary>
    private void OnLater()
    {
        IsSkipKeyDialogVisible = true;
        SkipUpdateKeyInput = string.Empty;
        SkipKeyErrorMessage = string.Empty;
    }

    /// <summary>
    /// 免更新密钥确认处理
    /// <para>密钥核对正确 → 关闭窗口跳过本次更新；</para>
    /// <para>密钥核对错误 → 提示用户可重新输入或选择立即更新。</para>
    /// </summary>
    private async void OnValidateSkipKey()
    {
        if (string.IsNullOrEmpty(SkipUpdateKeyInput))
        {
            SkipKeyErrorMessage = "请输入免更新密钥";
            return;
        }

        var valid = await _updateService.ValidateSkipUpdateKeyAsync(SkipUpdateKeyInput);
        if (valid)
        {
            // 密钥正确，标记已跳过，关闭窗口（应用继续运行）
            IsSkipped = true;
            CloseWindow();
        }
        else
        {
            // 密钥错误，提示用户可重新输入或选择立即更新
            SkipKeyErrorMessage = "免更新密钥错误，请重新输入或选择立即更新";
        }
    }

    /// <summary>
    /// 取消免更新密钥输入：返回版本信息展示态
    /// </summary>
    private void OnCancelSkipKey()
    {
        IsSkipKeyDialogVisible = false;
        SkipUpdateKeyInput = string.Empty;
        SkipKeyErrorMessage = string.Empty;
    }

    /// <summary>
    /// 关闭升级更新窗口
    /// </summary>
    private void CloseWindow()
    {
        foreach (var window in System.Windows.Application.Current.Windows)
        {
            if (window is UpdateWindow updateWindow)
            {
                updateWindow.Close();
                break;
            }
        }
    }

    /// <summary>
    /// 获取本机 IPv4 地址
    /// </summary>
    /// <returns>本机 IPv4 地址字符串；获取失败返回空字符串</returns>
    private static string GetLocalIpAddress()
    {
        try
        {
            var hostName = Dns.GetHostName();
            var addresses = Dns.GetHostAddresses(hostName);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ipv4?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
