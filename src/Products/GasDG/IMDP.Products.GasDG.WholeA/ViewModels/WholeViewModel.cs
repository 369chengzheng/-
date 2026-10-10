using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Products.GasDG.WholeA.ViewModels;

public partial class WholeViewModel : ObservableObject, INavigationAware
{
    private readonly ITestPlanService _testPlanService;
    private readonly IBoardDetectService _boardDetectService;
    private readonly IStationService _stationService;
    private readonly IEventAggregator _eventAggregator;

    private string _planTypeKey = "WholeSet";
    private int _planTypeCode => _planTypeKey == "WholeSet" ? 0 : 1;

    [ObservableProperty]
    private string _breadCrumb = "首页 > 整机设置";

    public string CurrentPageName => _planTypeKey == "WholeSet" ? "整机设置" : "整机检验";

    /// <summary>当前软件物料号（由主界面导航时传入，用于面包屑显示）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    [ObservableProperty]
    private string _productCode = string.Empty;

    [ObservableProperty]
    private string _customerCode = string.Empty;

    [ObservableProperty]
    private bool _isSchemeVisible = true;

    /// <summary>检测方案参数展示列表（含合并父参数，子参数内联展示在父参数卡片内）</summary>
    [ObservableProperty]
    private ObservableCollection<SchemeParameter> _otherSchemeParameters = new();

    [ObservableProperty]
    private ObservableCollection<StationDto> _stations = new();

    [ObservableProperty]
    private ObservableCollection<DetectionRowDto> _detectionRows = new();

    [ObservableProperty]
    private int _stationCount;

    public IRelayCommand QueryCommand { get; }
    public IRelayCommand OpenPortCommand { get; }
    public IRelayCommand ClosePortCommand { get; }
    public IRelayCommand ToggleSchemeCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand GoHomeCommand { get; }

    public WholeViewModel(ITestPlanService testPlanService, IBoardDetectService boardDetectService,
        IStationService stationService, IEventAggregator eventAggregator)
    {
        _testPlanService = testPlanService;
        _boardDetectService = boardDetectService;
        _stationService = stationService;
        _eventAggregator = eventAggregator;

        QueryCommand = new RelayCommand(OnQuery);
        OpenPortCommand = new RelayCommand(OnOpenPort);
        ClosePortCommand = new RelayCommand(OnClosePort);
        ToggleSchemeCommand = new RelayCommand(OnToggleScheme);
        OpenSettingsCommand = new RelayCommand(OnOpenSettings);
        GoHomeCommand = new RelayCommand(OnGoHome);
    }

    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    private void OnOpenSettings()
    {
        _eventAggregator.GetEvent<OpenSettingsDialogEvent>().Publish();
    }

    private async void OnQuery()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            MessageBox.Show("请输入产品编码");
            return;
        }

        try
        {
            // GasDG 产品固定路由到国内燃气整机配置仓储
            var plan = await _testPlanService.GetWholeConfigAsync(_planTypeCode, ProductCode, CustomerCode, ProductCategory.GasDG);
            if (plan == null || plan.Parameters == null || plan.Parameters.Count == 0)
            {
                OtherSchemeParameters.Clear();
                DetectionRows.Clear();
                return;
            }

            StationCount = plan.StationCount;

            // 获取抄表类型值，用于控制抄表时间子参数的可见性
            var readingTypeValue = plan.Parameters
                .FirstOrDefault(p => p.Code == "READING_TYPE")?
                .Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.Value)?.Value;

            // 加载已启用的参数 + 已启用合并父参数的子参数（子参数跟随父参数启用状态）
            var enabledParentCodes = plan.Parameters
                .Where(p => p.IsEnabled && MergedParameterMap.IsParent(p.Code))
                .Select(p => p.Code)
                .ToHashSet();

            var allSchemeParams = plan.Parameters
                .Where(p => p.IsEnabled ||
                    (MergedParameterMap.IsChild(p.Code) &&
                     MergedParameterMap.ChildToParent.TryGetValue(p.Code, out var parentCode) &&
                     parentCode != null &&
                     enabledParentCodes.Contains(parentCode)))
                .OrderBy(p => p.Order)
                .Select(p => new SchemeParameter(p))
                .ToList();

            // 根据抄表类型值应用可见性规则
            foreach (var param in allSchemeParams)
            {
                param.IsVisible = SchemeParameter.IsVisibleForReadingType(param.Code, readingTypeValue);
            }

            // 重建合并参数的父子关系（子参数加入父参数的 SubParameters 集合）
            RebuildSchemeMergedLinks(allSchemeParams);

            // 统一使用 OtherSchemeParameters，排除合并子参数（子参数在父参数卡片内展示）
            OtherSchemeParameters = new ObservableCollection<SchemeParameter>(
                allSchemeParams.Where(p => !MergedParameterMap.IsChild(p.Code)));

            _stationService.ClearAll();
            for (int i = 1; i <= plan.StationCount; i++)
            {
                _stationService.AddStation($"COM{i}");
            }
            Stations = new ObservableCollection<StationDto>(_stationService.GetStations());

            BuildDetectionRows(plan, readingTypeValue);

            _eventAggregator.GetEvent<RebuildStationColumnsEvent>().Publish(plan.StationCount);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询方案失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            OtherSchemeParameters.Clear();
            DetectionRows.Clear();
        }
    }

    /// <summary>
    /// 重建合并参数的父子关系
    /// <para>从扁平 SchemeParameter 列表中查找合并父参数和子参数，将子参数添加到父参数的 SubParameters 集合</para>
    /// </summary>
    private void RebuildSchemeMergedLinks(List<SchemeParameter> allParams)
    {
        var parentParams = allParams.Where(p => MergedParameterMap.IsParent(p.Code)).ToList();
        foreach (var parent in parentParams)
        {
            parent.SubParameters.Clear();
            var childCodes = MergedParameterMap.ChildToParent
                .Where(kv => kv.Value == parent.Code)
                .Select(kv => kv.Key)
                .ToHashSet();
            var children = allParams
                .Where(p => childCodes.Contains(p.Code))
                .OrderBy(p => p.Order);
            foreach (var child in children)
            {
                parent.SubParameters.Add(child);
            }
        }
    }

    private void BuildDetectionRows(TestPlanDto plan, string? readingTypeValue)
    {
        DetectionRows = new ObservableCollection<DetectionRowDto>();

        // 红外串口行
        var serialRow = new DetectionRowDto { Order = 1, ParameterName = "红外串口" };
        for (int i = 0; i < plan.StationCount; i++)
        {
            var serialPort = i < Stations.Count ? Stations[i].SerialPort : "";
            serialRow.CellValues.Add(new CellValueDto { Type = CellType.SerialPort, SerialPort = serialPort });
        }
        DetectionRows.Add(serialRow);

        // 检测状态行
        var statusRow = new DetectionRowDto { Order = 2, ParameterName = "检测状态" };
        for (int i = 0; i < plan.StationCount; i++)
        {
            var status = i < Stations.Count ? Stations[i].Status : "Idle";
            statusRow.CellValues.Add(new CellValueDto { Type = CellType.Status, StatusText = status });
        }
        DetectionRows.Add(statusRow);

        // 扫码SN行
        var scanRow = new DetectionRowDto { Order = 3, ParameterName = "扫码SN" };
        for (int i = 0; i < plan.StationCount; i++)
        {
            var sn = i < Stations.Count ? Stations[i].ProductSn : "";
            scanRow.CellValues.Add(new CellValueDto { Type = CellType.ScanSn, ScanSnText = sn });
        }
        DetectionRows.Add(scanRow);

        // 产品SN行
        var snRow = new DetectionRowDto { Order = 4, ParameterName = "产品SN", IsProductSn = true };
        for (int i = 0; i < plan.StationCount; i++)
        {
            snRow.CellValues.Add(new CellValueDto());
        }
        DetectionRows.Add(snRow);

        // 后续行：每个已勾选（IsEnabled）的参数作为独立检测项目
        // 严格按配置方案的 Order 排序，确保与整机设置方案配置页面顺序一致
        // 跳过合并参数的子参数（子参数归入父参数的检测行，不单独显示）
        // 跳过抄表时间子参数（抄表日、抄表时1-5、抄表日1-5），它们仅作为"抄表时间"父参数的内部参数
        var order = 5;
        foreach (var param in plan.Parameters.Where(p => p.IsEnabled).OrderBy(p => p.Order))
        {
            // 跳过合并参数的子参数（它们在父参数行内展示）
            if (MergedParameterMap.IsChild(param.Code))
                continue;

            // 跳过抄表时间子参数（它们仅作为"抄表时间"父参数的内部参数，不单独显示）
            if (SchemeParameter.IsReadingTimeChild(param.Code))
                continue;

            var row = new DetectionRowDto
            {
                Order = order++,
                ParameterName = param.Name,
                MesCode = param.MesCode,
                LowerLimit = ParseSubLimit(param, SubParameterCodes.LowerLimit),
                UpperLimit = ParseSubLimit(param, SubParameterCodes.UpperLimit)
            };
            for (int i = 0; i < plan.StationCount; i++)
            {
                row.CellValues.Add(new CellValueDto());
            }
            DetectionRows.Add(row);
        }
    }

    private async void OnOpenPort()
    {
        foreach (var station in Stations)
        {
            if (!string.IsNullOrEmpty(station.SerialPort))
            {
                await _boardDetectService.OpenPortAsync(station.SerialPort);
            }
        }
    }

    private async void OnClosePort()
    {
        await _boardDetectService.CloseAllPortsAsync();
    }

    /// <summary>
    /// 从 TestParameterDto 的子参数中解析指定 Code 的数值（下限/上限）
    /// <para>按 <see cref="ParasContentDto.Code"/> 在 <paramref name="param"/>.Parameters 中查找子参数，
    /// 并将其 <see cref="ParasContentDto.Value"/> 解析为 double。</para>
    /// </summary>
    /// <param name="param">检测项目 DTO</param>
    /// <param name="code">子参数 Code（建议使用 <see cref="SubParameterCodes.LowerLimit"/> 或 <see cref="SubParameterCodes.UpperLimit"/> 常量）</param>
    /// <returns>解析成功的 double 值；子参数不存在或解析失败时返回 null</returns>
    private static double? ParseSubLimit(TestParameterDto param, string code)
    {
        var sub = param.Parameters.FirstOrDefault(p => p.Code == code);
        if (sub != null && double.TryParse(sub.Value, out var result))
            return result;
        return null;
    }

    private void OnToggleScheme()
    {
        IsSchemeVisible = !IsSchemeVisible;
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        var planType = navigationContext.Parameters.GetValue<string>("PlanType");
        if (!string.IsNullOrEmpty(planType))
        {
            _planTypeKey = planType;
            BreadCrumb = planType == "WholeSet" ? "首页 > 整机设置" : "首页 > 整机检验";
            OnPropertyChanged(nameof(CurrentPageName));
        }

        // 读取主界面传入的软件物料号，用于面包屑显示
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }
    }
}

public class DetectionRowDto
{
    public int Order { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string MesCode { get; set; } = string.Empty;
    public double? LowerLimit { get; set; }
    public double? UpperLimit { get; set; }
    public bool IsProductSn { get; set; }
    public ObservableCollection<CellValueDto> CellValues { get; set; } = new();
}

public enum CellType
{
    Normal,
    SerialPort,
    Status,
    ScanSn
}

public partial class CellValueDto : ObservableObject
{
    [ObservableProperty]
    private string? _value;

    [ObservableProperty]
    private bool? _isPass;

    [ObservableProperty]
    private CellType _type = CellType.Normal;

    [ObservableProperty]
    private string? _serialPort;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private string? _scanSnText;

    public System.Windows.Media.Brush BackgroundBrush
    {
        get
        {
            return IsPass switch
            {
                true => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(198, 246, 213)),
                false => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 205, 205)),
                _ => System.Windows.Media.Brushes.Transparent
            };
        }
    }

    partial void OnIsPassChanged(bool? value)
    {
        OnPropertyChanged(nameof(BackgroundBrush));
    }
}
