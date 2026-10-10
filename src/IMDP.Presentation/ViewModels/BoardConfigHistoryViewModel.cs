// =============================================================================
// 文件名：BoardConfigHistoryViewModel.cs
// 功能描述：板卡配置维护历史查询视图模型。
//           按产品类别（必选）、产品编码、维护时间范围分页查询板卡配置变更历史，支持翻页与返回主页。
//           继承 PaginationViewModelBase<TestPlanDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.Services;
using IMDP.Application.DTOs;
using IMDP.Domain.Products;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 板卡配置维护历史查询视图模型
/// <para>调用 <see cref="ITestPlanService"/> 分页获取板卡配置变更记录，
/// 支持按产品类别（必选）+ 产品编码 + 维护时间范围筛选，并提供翻页与返回主页命令。</para>
/// <para>产品类别为必选查询条件，未选择时点击查询按钮将弹出"请选择产品类别"提示。</para>
/// </summary>
public partial class BoardConfigHistoryViewModel : PaginationViewModelBase<TestPlanDto>
{
    private readonly ITestPlanService _testPlanService;

    /// <summary>板卡配置历史记录集合（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<TestPlanDto> _records = new();

    /// <summary>当前选中的记录</summary>
    [ObservableProperty]
    private TestPlanDto? _selectedRecord;

    /// <summary>查询条件：产品类别（必选，下拉框选项：国内燃气/国内水务/海外燃气/海外水务）</summary>
    [ObservableProperty]
    private string? _selectedCategory;

    /// <summary>产品类别下拉框可选项（4 个产品类别键名）</summary>
    public string[] ProductCategoryOptions { get; } = { ProductCategory.GasDG, ProductCategory.WaterDW, ProductCategory.GasOG, ProductCategory.WaterOW };

    // 查询条件：产品编码（模糊查询）
    [ObservableProperty]
    private string _productCodeKeyword = string.Empty;

    // 查询条件：维护时间范围
    [ObservableProperty]
    private DateTime? _startTime;

    /// <summary>查询条件：维护时间范围结束时间</summary>
    [ObservableProperty]
    private DateTime? _endTime;

    /// <summary>查询命令（按当前条件拉取首页数据）</summary>
    public IAsyncRelayCommand QueryCommand { get; }

    /// <summary>
    /// 构造函数，注入测试计划服务与事件聚合器并初始化查询命令
    /// </summary>
    /// <param name="testPlanService">测试计划服务（提供板卡配置历史查询）</param>
    /// <param name="eventAggregator">Prism 事件聚合器（用于发布返回主页事件）</param>
    public BoardConfigHistoryViewModel(ITestPlanService testPlanService, IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
        _testPlanService = testPlanService;
        QueryCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <summary>
    /// 执行查询
    /// <para>产品类别为必选条件，未选择时弹出提示并终止查询；
    /// 调用服务端按产品类别 + 产品编码 + 维护时间范围 + 分页获取记录。</para>
    /// </summary>
    public override async Task RefreshAsync()
    {
        // 产品类别必选校验
        if (string.IsNullOrEmpty(SelectedCategory))
        {
            MessageBox.Show("请选择产品类别", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = await _testPlanService.GetBoardConfigHistoryAsync(
            ProductCodeKeyword ?? string.Empty, StartTime, EndTime, PageIndex, PageSize, SelectedCategory);

        Records = new ObservableCollection<TestPlanDto>(result.Items);
        TotalCount = result.TotalCount;
    }
}
