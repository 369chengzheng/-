// =============================================================================
// 文件名：LaserDetectorViewModel.cs
// 功能描述：激光检测仪模块检测方案配置视图模型。
//           维护 19 个检测参数项（含 MES 代码），并通过 ITestPlanService
//           实现方案的分页查询、保存与删除。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.LaserDetector.Models;

namespace IMDP.Products.GasDG.LaserDetector.ViewModels;

/// <summary>
/// 激光检测仪模块检测方案配置视图模型
/// <para>参数配置区以 <see cref="TestParameterDto"/> 列表承载 19 个检测项，
/// 每项的子参数按取值方式生成：Range 用 VALUE/LOWER_LIMIT/UPPER_LIMIT，
/// Value 用 VALUE，TestMode 用 VALUE（01/02），Status 无子参数。</para>
/// </summary>
public partial class LaserDetectorViewModel : ObservableObject, INavigationAware
{
    private readonly ITestPlanService _testPlanService;
    private readonly IEventAggregator _eventAggregator;

    /// <summary>当前软件物料号（由主界面导航时传入，用于面包屑显示）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    /// <summary>产品编码（查询与保存的主键）</summary>
    [ObservableProperty]
    private string _productCode = string.Empty;

    /// <summary>工位数量（激光检测仪协议支持 1～3 工位）</summary>
    [ObservableProperty]
    private int _stationCount = 3;

    /// <summary>
    /// 参数配置区参数列表（19 个激光检测参数项）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TestParameterDto> _parameters = new();

    /// <summary>方案列表（分页查询结果）</summary>
    [ObservableProperty]
    private ObservableCollection<TestPlanDto> _plans = new();

    /// <summary>当前选中的方案</summary>
    [ObservableProperty]
    private TestPlanDto? _selectedPlan;

    /// <summary>当前页码（从 1 开始）</summary>
    [ObservableProperty]
    private int _pageIndex = 1;

    /// <summary>每页条数</summary>
    [ObservableProperty]
    private int _pageSize = 10;

    /// <summary>总记录数</summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>
    /// 总页数（由 TotalCount 与 PageSize 计算，随两者变化通知刷新）
    /// </summary>
    public int TotalPages => PageSize > 0
        ? (TotalCount + PageSize - 1) / PageSize
        : 0;

    /// <summary>
    /// 已启用检测项数（用于参数配置区标题栏显示，如 8/19）
    /// </summary>
    public int EnabledCount => Parameters.Count(parameter => parameter.IsEnabled);

    /// <summary>
    /// 页大小下拉选项
    /// </summary>
    public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 10, 20, 50 };

    /// <summary>查询命令（按产品编码分页查询方案）</summary>
    public IRelayCommand QueryCommand { get; }

    /// <summary>维护命令（保存当前参数为方案）</summary>
    public IRelayCommand SaveCommand { get; }

    /// <summary>删除命令（删除选中的方案）</summary>
    public IRelayCommand DeleteCommand { get; }

    /// <summary>返回首页命令</summary>
    public IRelayCommand GoHomeCommand { get; }

    /// <summary>分页命令：首页</summary>
    public IRelayCommand FirstPageCommand { get; }

    /// <summary>分页命令：上一页</summary>
    public IRelayCommand PreviousPageCommand { get; }

    /// <summary>分页命令：下一页</summary>
    public IRelayCommand NextPageCommand { get; }

    /// <summary>分页命令：末页</summary>
    public IRelayCommand LastPageCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="testPlanService">检测计划服务（方案增删改查）</param>
    /// <param name="eventAggregator">事件聚合器（发布返回首页事件）</param>
    public LaserDetectorViewModel(ITestPlanService testPlanService, IEventAggregator eventAggregator)
    {
        _testPlanService = testPlanService;
        _eventAggregator = eventAggregator;

        QueryCommand = new RelayCommand(OnQuery);
        SaveCommand = new RelayCommand(OnSave);
        DeleteCommand = new RelayCommand(OnDelete);
        GoHomeCommand = new RelayCommand(OnGoHome);
        FirstPageCommand = new RelayCommand(() => NavigateToPage(1), () => PageIndex > 1);
        PreviousPageCommand = new RelayCommand(() => NavigateToPage(PageIndex - 1), () => PageIndex > 1);
        NextPageCommand = new RelayCommand(() => NavigateToPage(PageIndex + 1), () => PageIndex < TotalPages);
        LastPageCommand = new RelayCommand(() => NavigateToPage(TotalPages), () => PageIndex < TotalPages);

        InitializeDefaultParameters();
    }

    /// <summary>
    /// 通知分页相关与统计属性的绑定刷新
    /// </summary>
    private void NotifyPagingProperties()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(EnabledCount));
        FirstPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 跳转到指定页并重新查询
    /// </summary>
    /// <param name="pageIndex">目标页码（从 1 开始）</param>
    private void NavigateToPage(int pageIndex)
    {
        if (pageIndex < 1 || pageIndex > TotalPages)
        {
            return;
        }

        PageIndex = pageIndex;
        OnQuery();
    }

    /// <summary>
    /// 发布返回首页事件
    /// </summary>
    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <summary>
    /// 初始化 19 个默认激光检测参数项（含 MES 代码与取值方式）
    /// </summary>
    private void InitializeDefaultParameters()
    {
        Parameters = new ObservableCollection<TestParameterDto>(
            LaserDetectorConfigDefaults.CreateDefaultItems().ConvertAll(ToParameterDto));
    }

    /// <summary>
    /// 将配置项转换为检测参数 DTO
    /// <para>按取值方式生成子参数：Range 用 VALUE/LOWER_LIMIT/UPPER_LIMIT，
    /// Value 与 TestMode 用 VALUE，Status 无子参数。</para>
    /// </summary>
    /// <param name="item">激光检测配置项</param>
    /// <returns>检测参数 DTO</returns>
    private static TestParameterDto ToParameterDto(LaserDetectorConfigItem item)
    {
        var dto = new TestParameterDto
        {
            Name = item.Name,
            Code = item.Code,
            MesCode = item.MesCode,
            IsEnabled = item.IsEnabled,
            Order = 0
        };

        switch (item.Kind)
        {
            case LaserDetectorItemKind.Range:
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName, Order = SubParameterCodes.ValueOrder
                });
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.LowerLimit, Name = SubParameterCodes.LowerLimitDisplayName, Order = SubParameterCodes.LowerLimitOrder,
                    Value = item.LowerLimit ?? string.Empty, DefaultValue = item.LowerLimit ?? string.Empty
                });
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.UpperLimit, Name = SubParameterCodes.UpperLimitDisplayName, Order = SubParameterCodes.UpperLimitOrder,
                    Value = item.UpperLimit ?? string.Empty, DefaultValue = item.UpperLimit ?? string.Empty
                });
                break;

            case LaserDetectorItemKind.Value:
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName, Order = SubParameterCodes.ValueOrder,
                    Value = item.Value ?? string.Empty, DefaultValue = item.Value ?? string.Empty
                });
                break;

            case LaserDetectorItemKind.TestMode:
                dto.Parameters.Add(new ParasContentDto
                {
                    Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName, Order = SubParameterCodes.ValueOrder,
                    Value = item.TestMode ?? "01", DefaultValue = item.TestMode ?? "01"
                });
                break;

            case LaserDetectorItemKind.Status:
            default:
                // 状态型检测项仅有启用开关，无子参数。
                break;
        }

        return dto;
    }

    /// <summary>
    /// 选中方案变化时回填编辑区
    /// </summary>
    /// <param name="value">新选中的方案</param>
    partial void OnSelectedPlanChanged(TestPlanDto? value)
    {
        if (value != null)
        {
            ProductCode = value.ProductCode;
            StationCount = value.StationCount;
            Parameters = new ObservableCollection<TestParameterDto>(value.Parameters);
        }
    }

    /// <summary>
    /// 按产品编码分页查询方案列表
    /// </summary>
    private async void OnQuery()
    {
        var result = await _testPlanService.GetBoardConfigsAsync(ProductCode, PageIndex, PageSize, ProductCategory.GasDG);
        Plans = new ObservableCollection<TestPlanDto>(result.Items);
        TotalCount = result.TotalCount;
        NotifyPagingProperties();
    }

    /// <summary>
    /// 参数集合变化后刷新启用项数统计
    /// </summary>
    /// <param name="value">新的参数集合</param>
    partial void OnParametersChanged(ObservableCollection<TestParameterDto> value)
    {
        OnPropertyChanged(nameof(EnabledCount));
    }

    /// <summary>
    /// 总记录数变化后刷新总页数与分页按钮状态
    /// </summary>
    /// <param name="value">新的总记录数</param>
    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingProperties();
    }

    /// <summary>
    /// 页大小变化后重置到第一页并刷新分页状态
    /// </summary>
    /// <param name="value">新的页大小</param>
    partial void OnPageSizeChanged(int value)
    {
        PageIndex = 1;
        NotifyPagingProperties();
    }

    /// <summary>
    /// 保存当前编辑区参数为方案（新增或更新）
    /// </summary>
    private async void OnSave()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            MessageBox.Show("请输入产品编码");
            return;
        }

        var dto = new TestPlanDto
        {
            ProductCode = ProductCode,
            StationCount = StationCount,
            Parameters = Parameters.ToList(),
            Category = ProductCategory.GasDG,
            UpdatedBy = "system",
            UpdatedTime = DateTime.Now
        };

        await _testPlanService.SaveBoardConfigAsync(dto, "system");
        OnQuery();
    }

    /// <summary>
    /// 删除选中的方案（含确认提示）
    /// </summary>
    private async void OnDelete()
    {
        if (SelectedPlan == null)
        {
            MessageBox.Show("请选中要删除的方案");
            return;
        }

        var confirm = MessageBox.Show("确认删除选中的方案？", "确认", MessageBoxButton.OKCancel);
        if (confirm != MessageBoxResult.OK) return;

        await _testPlanService.DeleteBoardConfigAsync(SelectedPlan.Id, "system", ProductCategory.GasDG);
        OnQuery();
    }

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    /// <inheritdoc/>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 读取主界面传入的软件物料号，用于面包屑显示
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }

        OnQuery();
    }
}
