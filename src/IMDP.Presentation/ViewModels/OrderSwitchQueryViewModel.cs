// =============================================================================
// 文件名：OrderSwitchQueryViewModel.cs
// 功能描述：工单切换日志查询视图模型（4.17 模块）。
//           按制令单号模糊匹配 + 操作员模糊匹配 + 操作时间范围分页查询工单切换记录，
//           展示制令单号、软件物料号、加载菜单、操作员、操作时间、机器名、系统用户名等字段，
//           严格参考数据库表 OrderSwitchs 字段设计。支持按操作时间正序/倒序排序切换。
//           继承 PaginationViewModelBase<OrderSwitchRecordDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.Services;
using IMDP.Application.DTOs;
using NLog;

using IMDP.Infrastructure.Logging;
namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 工单切换日志查询视图模型
/// <para>调用 <see cref="ILogQueryService.GetOrderSwitchRecordsAsync"/> 分页获取工单切换记录，
/// 支持按制令单号（模糊匹配）+ 操作员（模糊匹配）+ 操作时间范围组合筛选，
/// 并提供翻页、按操作时间排序切换（正序/倒序）与返回主页命令。</para>
/// <para>仅系统管理员可访问；查询条件合法性校验：开始时间不得晚于结束时间。</para>
/// <para>首次进入页面时自动查询一次，展示最新工单切换记录。</para>
/// </summary>
public partial class OrderSwitchQueryViewModel : PaginationViewModelBase<OrderSwitchRecordDto>
{
    private static readonly ILogger _logger = LogHelper.For<OrderSwitchQueryViewModel>();
    private readonly ILogQueryService _logQueryService;

    /// <summary>工单切换记录集合（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<OrderSwitchRecordDto> _records = new();

    /// <summary>查询条件：制令单号模糊关键字</summary>
    [ObservableProperty]
    private string _orderNumberKeyword = string.Empty;

    /// <summary>查询条件：操作员模糊关键字</summary>
    [ObservableProperty]
    private string _operatorKeyword = string.Empty;

    /// <summary>查询条件：操作开始时间</summary>
    [ObservableProperty]
    private DateTime? _startTime;

    /// <summary>查询条件：操作结束时间</summary>
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
    public OrderSwitchQueryViewModel(ILogQueryService logQueryService, IEventAggregator eventAggregator,
        IMessageService messageService)
        : base(eventAggregator)
    {
        _messageService = messageService;
        _logQueryService = logQueryService;
        QueryCommand = new AsyncRelayCommand(ResetToFirstPageAndRefreshAsync);
    }

    /// <summary>
    /// 执行查询
    /// <para>合法性校验：若填写了时间范围，开始时间不得晚于结束时间。</para>
    /// <para>默认按操作时间倒序排列。</para>
    /// </summary>
    public override async Task RefreshAsync()
    {
        var startTime = StartTime?.Date;
        var selectedEndDate = EndTime?.Date;

        // DatePicker 只选择日期；结束日期按“包含整天”处理，传给仓储时转为次日零点开区间。
        if (startTime.HasValue && selectedEndDate.HasValue && startTime.Value > selectedEndDate.Value)
        {
            _messageService.ShowWarning("开始时间不能晚于结束时间", "提示");
            return;
        }

        var endTimeExclusive = selectedEndDate?.AddDays(1);

        try
        {
            var result = await _logQueryService.GetOrderSwitchRecordsAsync(
                OrderNumberKeyword ?? string.Empty,
                OperatorKeyword ?? string.Empty,
                startTime, endTimeExclusive,
                PageIndex, PageSize);

            Records = new ObservableCollection<OrderSwitchRecordDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "查询工单切换日志失败");
            _messageService.ShowError("查询工单切换日志失败，请稍后重试。", "错误");
        }
    }

    /// <inheritdoc/>
    /// <remarks>首次进入时自动查询一次，展示最新工单切换记录</remarks>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        _ = RefreshAsync();
    }
}
