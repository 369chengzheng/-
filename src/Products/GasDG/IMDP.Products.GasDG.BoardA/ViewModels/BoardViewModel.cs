using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Products.GasDG.BoardA.ViewModels;

public partial class BoardViewModel : ObservableObject, INavigationAware
{
    private readonly ITestPlanService _testPlanService;
    private readonly IBoardDetectService _boardDetectService;
    private readonly IStationService _stationService;
    private readonly IEventAggregator _eventAggregator;

    private readonly DispatcherTimer _simulationTimer;
    private readonly Random _random = new();
    private Dictionary<string, TestParameterDto> _parameterConfigMap = new();
    private int _tickCount;

    [ObservableProperty]
    private string _breadCrumb = "首页 > 模块检测";

    /// <summary>当前软件物料号（由主界面导航时传入，用于面包屑显示）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    [ObservableProperty]
    private string _productCode = string.Empty;

    [ObservableProperty]
    private bool _isSchemeVisible = true;

    [ObservableProperty]
    private ObservableCollection<TestParameterDto> _schemeParameters = new();

    [ObservableProperty]
    private ObservableCollection<StationDto> _stations = new();

    [ObservableProperty]
    private ObservableCollection<BoardDetectionRowDto> _detectionRows = new();

    [ObservableProperty]
    private int _stationCount;

    [ObservableProperty]
    private int _timerInterval = 1000;

    [ObservableProperty]
    private bool _isSimulating;

    public IRelayCommand QueryCommand { get; }
    public IRelayCommand OpenPortCommand { get; }
    public IRelayCommand ClosePortCommand { get; }
    public IRelayCommand ToggleSchemeCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand GoHomeCommand { get; }
    public IRelayCommand StartSimulationCommand { get; }
    public IRelayCommand StopSimulationCommand { get; }

    public BoardViewModel(ITestPlanService testPlanService, IBoardDetectService boardDetectService,
        IStationService stationService, IEventAggregator eventAggregator)
    {
        _testPlanService = testPlanService;
        _boardDetectService = boardDetectService;
        _stationService = stationService;
        _eventAggregator = eventAggregator;

        _simulationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(TimerInterval)
        };
        _simulationTimer.Tick += OnSimulationTick;

        QueryCommand = new RelayCommand(OnQuery);
        OpenPortCommand = new RelayCommand(OnOpenPort);
        ClosePortCommand = new RelayCommand(OnClosePort);
        ToggleSchemeCommand = new RelayCommand(OnToggleScheme);
        OpenSettingsCommand = new RelayCommand(OnOpenSettings);
        GoHomeCommand = new RelayCommand(OnGoHome);
        StartSimulationCommand = new RelayCommand(OnStartSimulation);
        StopSimulationCommand = new RelayCommand(OnStopSimulation);
    }

    partial void OnTimerIntervalChanged(int value)
    {
        if (value > 0)
        {
            _simulationTimer.Interval = TimeSpan.FromMilliseconds(value);
        }
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
        if (string.IsNullOrWhiteSpace(ProductCode)) return;

        var plan = await _testPlanService.GetBoardConfigAsync(ProductCode, ProductCategory.GasDG);
        if (plan != null)
        {
            StationCount = plan.StationCount;
            SchemeParameters = new ObservableCollection<TestParameterDto>(plan.Parameters.Where(p => p.IsEnabled));
            _parameterConfigMap = plan.Parameters
                .Where(p => p.IsEnabled)
                .ToDictionary(p => p.Name, p => p);

            _stationService.ClearAll();
            for (int i = 1; i <= plan.StationCount; i++)
            {
                _stationService.AddStation($"COM{i}");
            }
            Stations = new ObservableCollection<StationDto>(_stationService.GetStations());

            BuildDetectionRows(plan);

            _eventAggregator.GetEvent<RebuildStationColumnsEvent>().Publish(plan.StationCount);
        }
    }

    private void BuildDetectionRows(TestPlanDto plan)
    {
        DetectionRows = new ObservableCollection<BoardDetectionRowDto>();

        // 红外串口行
        var serialRow = new BoardDetectionRowDto { Order = 1, ParameterName = "红外串口" };
        for (int i = 0; i < plan.StationCount; i++)
        {
            var serialPort = i < Stations.Count ? Stations[i].SerialPort : "";
            serialRow.CellValues.Add(new BoardCellValueDto { Type = CellType.SerialPort, SerialPort = serialPort });
        }
        DetectionRows.Add(serialRow);

        // 检测状态行
        var statusRow = new BoardDetectionRowDto { Order = 2, ParameterName = "检测状态" };
        for (int i = 0; i < plan.StationCount; i++)
        {
            var status = i < Stations.Count ? Stations[i].Status : "Idle";
            statusRow.CellValues.Add(new BoardCellValueDto { Type = CellType.Status, StatusText = status });
        }
        DetectionRows.Add(statusRow);

        // 扫码SN行
        var scanRow = new BoardDetectionRowDto { Order = 3, ParameterName = "扫码SN" };
        for (int i = 0; i < plan.StationCount; i++)
        {
            var sn = i < Stations.Count ? Stations[i].ProductSn : "";
            scanRow.CellValues.Add(new BoardCellValueDto { Type = CellType.ScanSn, ScanSnText = sn });
        }
        DetectionRows.Add(scanRow);

        // 产品SN行
        var snRow = new BoardDetectionRowDto { Order = 4, ParameterName = "产品SN", IsProductSn = true };
        for (int i = 0; i < plan.StationCount; i++)
        {
            snRow.CellValues.Add(new BoardCellValueDto());
        }
        DetectionRows.Add(snRow);

        // 后续行：每个启用的参数
        var order = 5;
        foreach (var param in plan.Parameters.Where(p => p.IsEnabled))
        {
            var row = new BoardDetectionRowDto
            {
                Order = order++,
                ParameterName = param.Name,
                MesCode = param.MesCode
            };
            for (int i = 0; i < plan.StationCount; i++)
            {
                row.CellValues.Add(new BoardCellValueDto());
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

    private void OnToggleScheme()
    {
        IsSchemeVisible = !IsSchemeVisible;
    }

    private void OnStartSimulation()
    {
        if (DetectionRows.Count == 0)
        {
            MessageBox.Show("请先查询检测方案");
            return;
        }

        _tickCount = 0;
        _simulationTimer.Interval = TimeSpan.FromMilliseconds(TimerInterval);
        _simulationTimer.Start();
        IsSimulating = true;
    }

    private void OnStopSimulation()
    {
        _simulationTimer.Stop();
        IsSimulating = false;
    }

    private void OnSimulationTick(object? sender, EventArgs e)
    {
        _tickCount++;
        UpdateStatusRow();
        UpdateParameterRows();
    }

    private void UpdateStatusRow()
    {
        var statusRow = DetectionRows.FirstOrDefault(r => r.ParameterName == "检测状态");
        if (statusRow == null) return;

        var cycle = _tickCount % 4;
        var statusText = cycle switch
        {
            0 => "Idle",
            1 => "Testing",
            2 => "Testing",
            _ => "Completed"
        };

        foreach (var cell in statusRow.CellValues)
        {
            cell.StatusText = statusText;
            cell.IsPass = cycle == 3 ? true : null;
        }
    }

    private void UpdateParameterRows()
    {
        foreach (var row in DetectionRows)
        {
            if (row.ParameterName is "红外串口" or "检测状态" or "扫码SN" or "产品SN")
                continue;

            if (!_parameterConfigMap.TryGetValue(row.ParameterName, out var param))
                continue;

            foreach (var cell in row.CellValues)
            {
                var (receivedValue, isPass) = GenerateSimulatedValue(param);
                cell.Value = receivedValue;
                cell.IsPass = isPass;
            }
        }
    }

    private (string value, bool? isPass) GenerateSimulatedValue(TestParameterDto param)
    {
        var content = param.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.Value);
        var lowerSub = param.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.LowerLimit);
        var upperSub = param.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.UpperLimit);
        double? lowerLimit = null;
        double? upperLimit = null;
        if (lowerSub != null && double.TryParse(lowerSub.Value, out var lowerVal)) lowerLimit = lowerVal;
        if (upperSub != null && double.TryParse(upperSub.Value, out var upperVal)) upperLimit = upperVal;
        var value = content?.Value ?? string.Empty;

        var hasRange = lowerLimit.HasValue && upperLimit.HasValue;
        var hasExactValue = !string.IsNullOrWhiteSpace(value);

        if (hasRange)
        {
            var lower = lowerLimit!.Value;
            var upper = upperLimit!.Value;
            var pass = _random.NextDouble() < 0.75;

            if (pass)
            {
                var v = Math.Round(_random.NextDouble() * (upper - lower) + lower, 2);
                return (v.ToString("F2"), true);
            }
            else
            {
                var v = Math.Round(lower - _random.NextDouble() * Math.Abs(lower) - 0.5, 2);
                return (v.ToString("F2"), false);
            }
        }

        if (hasExactValue)
        {
            var pass = _random.NextDouble() < 0.75;
            if (pass)
            {
                return (value, true);
            }
            else
            {
                var v = Math.Round(_random.NextDouble() * 100, 2);
                return (v.ToString("F2"), false);
            }
        }

        var displayValue = Math.Round(_random.NextDouble() * 100, 2).ToString("F2");
        return (displayValue, null);
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        OnStopSimulation();
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // 模块检测页无需 PlanType 参数

        // 读取主界面传入的软件物料号，用于面包屑显示
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }
    }
}

public class BoardDetectionRowDto
{
    public int Order { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string MesCode { get; set; } = string.Empty;
    public bool IsProductSn { get; set; }
    public ObservableCollection<BoardCellValueDto> CellValues { get; set; } = new();
}

public enum CellType
{
    Normal,
    SerialPort,
    Status,
    ScanSn
}

public partial class BoardCellValueDto : ObservableObject
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
                _ => System.Windows.Media.Brushes.White
            };
        }
    }

    partial void OnIsPassChanged(bool? value)
    {
        OnPropertyChanged(nameof(BackgroundBrush));
    }
}
