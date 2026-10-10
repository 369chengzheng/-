// =============================================================================
// 文件名：UpdateQueryViewModel.cs
// 功能描述：升级日志查询视图模型（4.16 模块）。
//           按版本号模糊匹配 + 更新内容关键词 + 升级时间范围分页查询应用升级记录，
//           展示升级前版本号、升级后版本号、更新内容、升级开始时间、升级完成时间、错误信息、
//           升级状态、机器名、系统用户名等字段，严格参考数据库表 AppUpdateRecords 字段设计。
//           支持导出查询结果为 Excel。
//           继承 PaginationViewModelBase<AppUpdateRecordDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.Services;
using IMDP.Application.DTOs;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 升级日志查询视图模型
/// <para>调用 <see cref="ILogQueryService.GetUpdateRecordsAsync"/> 分页获取升级记录，
/// 支持按版本号（模糊匹配升级前/后版本号）+ 更新内容关键词 + 升级时间范围组合筛选，
/// 并提供翻页、返回主页与导出 Excel 命令。</para>
/// <para>仅系统管理员可访问；查询条件合法性校验：开始时间不得晚于结束时间。</para>
/// <para>首次进入页面时自动查询一次，展示最新升级记录。</para>
/// </summary>
public partial class UpdateQueryViewModel : PaginationViewModelBase<AppUpdateRecordDto>
{
    private readonly ILogQueryService _logQueryService;

    /// <summary>升级记录集合（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<AppUpdateRecordDto> _records = new();

    /// <summary>查询条件：版本号模糊关键字（匹配升级前/后版本号）</summary>
    [ObservableProperty]
    private string _versionKeyword = string.Empty;

    /// <summary>查询条件：更新内容关键词（匹配 ReleaseNotes）</summary>
    [ObservableProperty]
    private string _releaseNotesKeyword = string.Empty;

    /// <summary>查询条件：升级开始时间范围起</summary>
    [ObservableProperty]
    private DateTime? _startTime;

    /// <summary>查询条件：升级开始时间范围止</summary>
    [ObservableProperty]
    private DateTime? _endTime;

    /// <summary>查询命令</summary>
    public IAsyncRelayCommand QueryCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public UpdateQueryViewModel(ILogQueryService logQueryService, IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
        _logQueryService = logQueryService;
        QueryCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <summary>
    /// 执行查询
    /// </summary>
    public override async Task RefreshAsync()
    {
        if (StartTime.HasValue && EndTime.HasValue && StartTime.Value > EndTime.Value)
        {
            MessageBox.Show("开始时间不能晚于结束时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = await _logQueryService.GetUpdateRecordsAsync(
            VersionKeyword ?? string.Empty,
            ReleaseNotesKeyword ?? string.Empty,
            StartTime, EndTime,
            PageIndex, PageSize);

        Records = new ObservableCollection<AppUpdateRecordDto>(result.Items);
        TotalCount = result.TotalCount;
    }

    /// <inheritdoc/>
    /// <remarks>首次进入时自动查询一次，展示最新升级记录</remarks>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = RefreshAsync();
    }
}
