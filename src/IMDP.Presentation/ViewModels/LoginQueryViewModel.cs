// =============================================================================
// 文件名：LoginQueryViewModel.cs
// 功能描述：登录日志查询视图模型（4.15 模块）。
//           按工号精确匹配 + 登录时间范围分页查询用户登录记录，展示工位、工号、软件版本号、
//           登录时间、登出时间等字段，严格参考数据库表 LoginConfig 字段设计。
//           继承 PaginationViewModelBase<LoginRecordDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
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
/// 登录日志查询视图模型
/// <para>调用 <see cref="ILogQueryService.GetLoginRecordsAsync"/> 分页获取登录记录，
/// 支持按工号（精确匹配）+ 登录时间范围组合筛选，并提供翻页与返回主页命令。</para>
/// <para>仅系统管理员可访问；查询条件合法性校验：开始时间不得晚于结束时间。</para>
/// <para>首次进入页面时自动查询一次，展示最新登录记录。</para>
/// </summary>
public partial class LoginQueryViewModel : PaginationViewModelBase<LoginRecordDto>
{
    private readonly ILogQueryService _logQueryService;

    /// <summary>登录记录集合（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<LoginRecordDto> _records = new();

    /// <summary>查询条件：工号（精确匹配）</summary>
    [ObservableProperty]
    private string _workNumberKeyword = string.Empty;

    /// <summary>查询条件：登录开始时间</summary>
    [ObservableProperty]
    private DateTime? _startTime;

    /// <summary>查询条件：登录结束时间</summary>
    [ObservableProperty]
    private DateTime? _endTime;

    /// <summary>查询命令</summary>
    public IAsyncRelayCommand QueryCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logQueryService">日志查询服务</param>
    /// <param name="eventAggregator">Prism 事件聚合器</param>
    private readonly IMessageService _messageService;
    public LoginQueryViewModel(ILogQueryService logQueryService, IEventAggregator eventAggregator,
        IMessageService messageService)
        : base(eventAggregator)
    {
        _messageService = messageService;
        _logQueryService = logQueryService;
        QueryCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <summary>
    /// 执行查询
    /// <para>合法性校验：若填写了时间范围，开始时间不得晚于结束时间。</para>
    /// </summary>
    public override async Task RefreshAsync()
    {
        // 时间范围合法性校验
        if (StartTime.HasValue && EndTime.HasValue && StartTime.Value > EndTime.Value)
        {
            _messageService.ShowWarning("开始时间不能晚于结束时间", "提示");
            return;
        }

        var result = await _logQueryService.GetLoginRecordsAsync(
            WorkNumberKeyword ?? string.Empty,
            StartTime, EndTime,
            PageIndex, PageSize);

        Records = new ObservableCollection<LoginRecordDto>(result.Items);
        TotalCount = result.TotalCount;
    }

    /// <inheritdoc/>
    /// <remarks>首次进入时自动查询一次，展示最新登录记录</remarks>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = RefreshAsync();
    }
}
