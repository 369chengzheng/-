// =============================================================================
// 文件名：WholeQueryViewModel.cs
// 功能描述：整机检测记录查询视图模型。
//           按产品类别（必选）、制令单号、检测类型、产品SN、作业工号、检测时间范围分页查询整机检测记录。
//           继承 PaginationViewModelBase<TestRecordDto>，复用分页状态与翻页命令，仅实现 RefreshAsync()。
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
/// 整机检测记录查询视图模型
/// <para>调用 ITestRecordService 分页获取整机检测记录，
/// 支持按产品类别（必选）+ 制令单号 + 产品SN + 作业工号模糊查询与检测类型、检测时间范围筛选。</para>
/// <para>产品类别为必选查询条件，未选择时点击查询按钮将弹出"请选择产品类别"提示。</para>
/// </summary>
public partial class WholeQueryViewModel : PaginationViewModelBase<TestRecordDto>
{
    private readonly ITestRecordService _testRecordService;

    /// <summary>整机检测记录集合（当前页）</summary>
    [ObservableProperty]
    private ObservableCollection<TestRecordDto> _records = new();

    /// <summary>当前选中的记录</summary>
    [ObservableProperty]
    private TestRecordDto? _selectedRecord;

    /// <summary>查询条件：产品类别（必选，下拉框选项：国内燃气/国内水务/海外燃气/海外水务）</summary>
    [ObservableProperty]
    private string? _selectedCategory;

    /// <summary>产品类别下拉框可选项（4 个产品类别键名）</summary>
    public string[] ProductCategoryOptions { get; } = { ProductCategory.GasDG, ProductCategory.WaterDW, ProductCategory.GasOG, ProductCategory.WaterOW };

    // 查询条件：制令单号（模糊查询）
    [ObservableProperty]
    private string _orderNumberKeyword = string.Empty;

    // 查询条件：检测类型筛选，null 表示全部（0=设置, 1=检验）
    [ObservableProperty]
    private int? _selectedTestType;

    // 查询条件：产品SN（模糊查询）
    [ObservableProperty]
    private string _productSnKeyword = string.Empty;

    // 查询条件：作业工号（模糊查询）
    [ObservableProperty]
    private string _workNumberKeyword = string.Empty;

    // 查询条件：检测时间范围
    [ObservableProperty]
    private DateTime? _startTime;

    /// <summary>查询条件：检测时间范围结束时间</summary>
    [ObservableProperty]
    private DateTime? _endTime;

    /// <summary>查询命令</summary>
    public IAsyncRelayCommand QueryCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="testRecordService">检测记录服务（提供整机检测记录查询）</param>
    /// <param name="eventAggregator">Prism 事件聚合器（用于发布返回主页事件）</param>
    public WholeQueryViewModel(ITestRecordService testRecordService, IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
        _testRecordService = testRecordService;
        QueryCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <summary>
    /// 执行查询
    /// <para>调用服务端按检测类型 + 制令单号 + 产品SN + 作业工号 + 检测时间范围 + 分页参数获取记录。</para>
    /// </summary>
    public override async Task RefreshAsync()
    {
        // 产品类别必选校验
        if (string.IsNullOrEmpty(SelectedCategory))
        {
            MessageBox.Show("请选择产品类别", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = await _testRecordService.GetWholeRecordsAsync(
            SelectedTestType,
            OrderNumberKeyword ?? string.Empty,
            ProductSnKeyword ?? string.Empty,
            WorkNumberKeyword ?? string.Empty,
            StartTime, EndTime,
            PageIndex, PageSize, SelectedCategory);

        Records = new ObservableCollection<TestRecordDto>(result.Items);
        TotalCount = result.TotalCount;
    }
}
