using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Products;

namespace IMDP.Products.GasDG.BoardA.ViewModels;

public partial class BoardConfigViewModel : ObservableObject, INavigationAware
{
    private readonly ITestPlanService _testPlanService;
    private readonly IEventAggregator _eventAggregator;

    [ObservableProperty]
    private string _breadCrumb = "首页 > 模块检测方案配置";

    /// <summary>当前软件物料号（由主界面导航时传入，用于面包屑显示）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    [ObservableProperty]
    private string _productCode = string.Empty;

    [ObservableProperty]
    private int _stationCount = 4;

    /// <summary>
    /// 参数配置区参数列表（8 个模块检测参数项）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TestParameterDto> _parameters = new();

    [ObservableProperty]
    private ObservableCollection<TestPlanDto> _plans = new();

    [ObservableProperty]
    private TestPlanDto? _selectedPlan;

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize = 10;

    [ObservableProperty]
    private int _totalCount;

    public IRelayCommand QueryCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand GoHomeCommand { get; }

    public BoardConfigViewModel(ITestPlanService testPlanService, IEventAggregator eventAggregator)
    {
        _testPlanService = testPlanService;
        _eventAggregator = eventAggregator;

        QueryCommand = new RelayCommand(OnQuery);
        SaveCommand = new RelayCommand(OnSave);
        DeleteCommand = new RelayCommand(OnDelete);
        GoHomeCommand = new RelayCommand(OnGoHome);

        InitializeDefaultParameters();
    }

    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <summary>
    /// 初始化 8 个默认模块检测参数项（按设计文档 4.7 节）
    /// </summary>
    private void InitializeDefaultParameters()
    {
        Parameters = new ObservableCollection<TestParameterDto>
        {
            new() { Name = "IP地址/端口", Order = 1, IsEnabled = true, Code = "IP_ADDR_PORT", MesCode = "IP_ADDR_PORT", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "闲置天数", Order = 2, IsEnabled = true, Code = "IDLE_DAYS", MesCode = "IDLE_DAYS", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "表具规格", Order = 3, IsEnabled = true, Code = "METER_SPEC", MesCode = "METER_SPEC", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "域名/端口", Order = 4, IsEnabled = true, Code = "DOMAIN_ADDR", MesCode = "DOMAIN_ADDR", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "一级不通讯天数", Order = 5, IsEnabled = true, Code = "NO_COMM_DAYS", MesCode = "NO_COMM_DAYS", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "一级不用气天数", Order = 6, IsEnabled = true, Code = "NO_GAS_DAYS", MesCode = "NO_GAS_DAYS", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "大流阈值", Order = 7, IsEnabled = true, Code = "HIGH_FLOW_THR", MesCode = "HIGH_FLOW_THR", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } },
            new() { Name = "小流阈值", Order = 8, IsEnabled = true, Code = "LOW_FLOW_THR", MesCode = "LOW_FLOW_THR", Parameters = new() { new() { Name = SubParameterCodes.ValueDisplayName, Code = SubParameterCodes.Value, Order = SubParameterCodes.ValueOrder } } }
        };
    }

    partial void OnSelectedPlanChanged(TestPlanDto? value)
    {
        if (value != null)
        {
            ProductCode = value.ProductCode;
            StationCount = value.StationCount;
            Parameters = new ObservableCollection<TestParameterDto>(value.Parameters);
        }
    }

    private async void OnQuery()
    {
        var result = await _testPlanService.GetBoardConfigsAsync(ProductCode, PageIndex, PageSize, ProductCategory.GasDG);
        Plans = new ObservableCollection<TestPlanDto>(result.Items);
        TotalCount = result.TotalCount;
    }

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

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 模块检测方案配置无需 PlanType 参数

        // 读取主界面传入的软件物料号，用于面包屑显示
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }

        OnQuery();
    }
}
