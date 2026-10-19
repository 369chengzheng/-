// =============================================================================
// 文件名：LaserDetectorViewModel.cs
// 功能描述：激光检测仪校验界面视图模型（参考 IMDS 项目同名界面移植）。
//           按方案的工位数量动态生成工位卡片；扫码触发自动测试 → 轮询结果
//           → 结果项（全部配置项）只读展示 → 人工测试项（LED/蜂鸣器/按键）
//           → 综合判定合格性并保存检测记录。
// =============================================================================
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Events;
using Prism.Navigation.Regions;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Application.Events;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.LaserDetector.Models;
using IMDP.Products.GasDG.LaserDetector.Services;

namespace IMDP.Products.GasDG.LaserDetector.ViewModels;

/// <summary>
/// 激光检测仪校验界面视图模型
/// <para>按测试方案的工位数量（1～3）动态生成工位卡片；每工位独立流程：
/// 表具条码扫码触发自动检测 → 自动结果按启用配置项全部只读展示 →
/// 操作员对 LED/蜂鸣器/按键人工判定 → 全部通过才判合格，结果经
/// <see cref="ITestRecordService"/> 保存为板卡检测记录。</para>
/// </summary>
public partial class LaserDetectorViewModel : ObservableObject, INavigationAware
{
    private readonly ITestPlanService _testPlanService;
    private readonly ITestRecordService _testRecordService;
    private readonly LaserDetectorDetectService _detectService;
    private readonly ICurrentContext _currentContext;
    private readonly IEventAggregator _eventAggregator;
    private readonly IMessageService _messageService;

    /// <summary>串口名称缓存（打开串口后记录，关闭与发送时使用）</summary>
    private string _portName = string.Empty;

    /// <summary>测试耗时刷新定时器（每秒刷新所有进行中的工位耗时）</summary>
    private readonly System.Windows.Threading.DispatcherTimer _durationTimer;

    /// <summary>各工位测试计时器（索引 = 工位号 - 1）</summary>
    private readonly Stopwatch?[] _stopwatches = new Stopwatch?[LaserDetectorProtocolStationCount];

    /// <summary>协议支持的最大工位数</summary>
    private const int LaserDetectorProtocolStationCount = 3;

    /// <summary>导航时由主界面传入的软件物料号（面包屑显示）</summary>
    [ObservableProperty]
    private string _currentMaterialNumber = string.Empty;

    /// <summary>可用串口列表</summary>
    [ObservableProperty]
    private ObservableCollection<string> _ports = new();

    /// <summary>当前选中的串口</summary>
    [ObservableProperty]
    private string _selectedPort = string.Empty;

    /// <summary>串口是否已打开（控制打开/关闭按钮的可用性）</summary>
    [ObservableProperty]
    private bool _isSerialOpen;

    /// <summary>当前关联的产品编码（失焦后从方案库加载配置）</summary>
    [ObservableProperty]
    private string _productCode = string.Empty;

    /// <summary>有源测试配置预览（如 01-仅测试输出功能）</summary>
    [ObservableProperty]
    private string _activeTestDisplay = string.Empty;

    /// <summary>无源测试配置预览</summary>
    [ObservableProperty]
    private string _passiveTestDisplay = string.Empty;

    /// <summary>功耗配置预览（开启 下限~上限 / 关闭）</summary>
    [ObservableProperty]
    private string _powerConfigDisplay = string.Empty;

    /// <summary>当前方案的工位数量（1～3，决定工位卡片生成个数）</summary>
    [ObservableProperty]
    private int _stationCount = LaserDetectorProtocolStationCount;

    /// <summary>
    /// 当前检测配置（常态化展示全部 19 个配置项，只读）
    /// <para>初始为默认项集合；产品编码失焦加载方案后按系统码合并替换，
    /// 界面按下标绑定的顺序与配置界面完全一致。</para>
    /// </summary>
    [ObservableProperty]
    private LaserDetectorConfig _config = new()
    {
        Items = LaserDetectorConfigDefaults.CreateDefaultItems()
    };

    /// <summary>工位运行时状态列表（数量随方案工位数动态变化）</summary>
    [ObservableProperty]
    private ObservableCollection<LaserDetectorStationState> _stations = new();

    /// <summary>返回首页命令</summary>
    public IRelayCommand GoHomeCommand { get; }

    /// <summary>打开串口命令</summary>
    public IRelayCommand OpenSerialCommand { get; }

    /// <summary>关闭串口命令</summary>
    public IRelayCommand CloseSerialCommand { get; }

    /// <summary>刷新串口列表命令（下拉展开时触发）</summary>
    public IRelayCommand RefreshPortsCommand { get; }

    /// <summary>产品编码失焦命令（从方案库加载配置）</summary>
    public IRelayCommand ProductLostFocusCommand { get; }

    /// <summary>工位扫码触发检测命令（参数为工位号）</summary>
    public IRelayCommand<int> StationScanCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="testPlanService">测试计划服务（按产品编码加载方案）</param>
    /// <param name="testRecordService">检测记录服务（保存检测结果）</param>
    /// <param name="detectService">激光检测服务（串口交互与协议解析）</param>
    /// <param name="currentContext">当前用户上下文（记录操作员）</param>
    /// <param name="eventAggregator">事件聚合器（返回首页）</param>
    /// <param name="messageService">统一消息提示服务</param>
    public LaserDetectorViewModel(
        ITestPlanService testPlanService,
        ITestRecordService testRecordService,
        LaserDetectorDetectService detectService,
        ICurrentContext currentContext,
        IEventAggregator eventAggregator,
        IMessageService messageService)
    {
        _testPlanService = testPlanService;
        _testRecordService = testRecordService;
        _detectService = detectService;
        _currentContext = currentContext;
        _eventAggregator = eventAggregator;
        _messageService = messageService;

        _durationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += OnDurationTimerTick;

        GoHomeCommand = new RelayCommand(OnGoHome);
        OpenSerialCommand = new RelayCommand(OnOpenSerial);
        CloseSerialCommand = new RelayCommand(OnCloseSerial);
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        ProductLostFocusCommand = new RelayCommand(OnProductLostFocus);
        StationScanCommand = new RelayCommand<int>(station => _ = TriggerStationTestAsync(station));

        RebuildStations(StationCount);
        RefreshPorts();
    }

    /// <summary>
    /// 每秒刷新所有进行中工位的测试耗时文本
    /// </summary>
    private void OnDurationTimerTick(object? sender, EventArgs e)
    {
        bool anyRunning = false;
        for (int index = 0; index < _stopwatches.Length; index++)
        {
            var stopwatch = _stopwatches[index];
            if (stopwatch?.IsRunning != true)
            {
                continue;
            }

            anyRunning = true;
            var state = Stations.FirstOrDefault(s => s.Station == index + 1);
            if (state != null)
            {
                state.TestDuration = stopwatch.Elapsed.ToString(@"mm\:ss");
            }
        }

        if (!anyRunning)
        {
            _durationTimer.Stop();
        }
    }

    /// <summary>
    /// 按方案工位数量重建工位状态列表
    /// </summary>
    /// <param name="stationCount">工位数量（1～3）</param>
    private void RebuildStations(int stationCount)
    {
        foreach (var state in Stations)
        {
            state.PropertyChanged -= OnStationPropertyChanged;
        }

        var stations = new ObservableCollection<LaserDetectorStationState>();
        for (int station = 1; station <= stationCount; station++)
        {
            var state = new LaserDetectorStationState(station);
            state.PropertyChanged += OnStationPropertyChanged;
            stations.Add(state);
        }

        Stations = stations;
    }

    /// <summary>
    /// 工位状态变化时重新评估该工位的综合结果
    /// </summary>
    private void OnStationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not LaserDetectorStationState station)
        {
            return;
        }

        // 仅人工测试项变化时才需要重新评估最终结果
        if (e.PropertyName == nameof(LaserDetectorStationState.LedResult) ||
            e.PropertyName == nameof(LaserDetectorStationState.BuzzerResult) ||
            e.PropertyName == nameof(LaserDetectorStationState.ButtonResult))
        {
            EvaluateStationResult(station);
        }
    }

    /// <summary>
    /// 返回首页
    /// </summary>
    private void OnGoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <summary>
    /// 刷新可用串口列表（自动选中第一个）
    /// </summary>
    private void RefreshPorts()
    {
        Ports = new ObservableCollection<string>(_detectService.GetAvailablePorts());
        if (Ports.Count > 0 && string.IsNullOrEmpty(SelectedPort))
        {
            SelectedPort = Ports[0];
        }
    }

    /// <summary>
    /// 打开选中的串口
    /// </summary>
    private async void OnOpenSerial()
    {
        if (string.IsNullOrEmpty(SelectedPort))
        {
            _messageService.ShowWarning("请先选择一个串口！");
            return;
        }

        IsSerialOpen = await _detectService.OpenPortAsync(SelectedPort);
        if (IsSerialOpen)
        {
            _portName = SelectedPort;
            _messageService.ShowInfo($"已打开串口: {_portName}");
        }
        else
        {
            _messageService.ShowWarning($"串口 {SelectedPort} 打开失败，请检查占用或连接。");
        }
    }

    /// <summary>
    /// 关闭当前串口并清空全部工位的进行中状态
    /// </summary>
    private async void OnCloseSerial()
    {
        if (string.IsNullOrEmpty(_portName))
        {
            return;
        }

        await _detectService.ClosePortAsync(_portName);
        IsSerialOpen = false;
        foreach (var station in Stations)
        {
            station.Reset();
        }

        Array.Clear(_stopwatches, 0, _stopwatches.Length);
        _durationTimer.Stop();
        _messageService.ShowInfo("串口已关闭！");
    }

    /// <summary>
    /// 产品编码失焦或键入空格后按编码从方案库加载检测配置
    /// <para>存在该编码的方案时加载到配置预览区；不存在时以 Growl 提示。</para>
    /// </summary>
    private async void OnProductLostFocus()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            return;
        }

        var plan = await _testPlanService.GetBoardConfigAsync(ProductCode, ProductCategory.GasDG);
        if (plan == null)
        {
            // 未找到方案：Growl 信息提示（不阻塞操作员视线）
            HandyControl.Controls.Growl.Info($"产品编码 [{ProductCode}] 不存在配置！");
            return;
        }

        ApplyPlan(plan);
    }

    /// <summary>
    /// 将方案 DTO 合并到默认配置项并刷新界面
    /// <para>以默认 19 项为基准按系统码合并，保证界面按下标绑定时的
    /// 固定展示顺序与配置界面完全一致；未出现在方案中的项保持默认。</para>
    /// </summary>
    /// <param name="plan">测试方案</param>
    private void ApplyPlan(TestPlanDto plan)
    {
        var items = LaserDetectorConfigDefaults.CreateDefaultItems();
        foreach (var dto in plan.Parameters)
        {
            var target = items.FirstOrDefault(item => item.Code == dto.Code);
            if (target == null)
            {
                continue;
            }

            target.Name = dto.Name;
            target.MesCode = dto.MesCode;
            target.IsEnabled = dto.IsEnabled;
            foreach (var sub in dto.Parameters)
            {
                switch (sub.Code)
                {
                    case SubParameterCodes.Value:
                        target.Value = sub.Value;
                        target.TestMode = sub.Value;
                        break;
                    case SubParameterCodes.LowerLimit:
                        target.LowerLimit = sub.Value;
                        break;
                    case SubParameterCodes.UpperLimit:
                        target.UpperLimit = sub.Value;
                        break;
                }
            }
        }

        var config = new LaserDetectorConfig { Items = items };
        Config = config;
        ActiveTestDisplay = BuildTestModeDisplay(config, LaserDetectorItemCodes.ActiveTest);
        PassiveTestDisplay = BuildTestModeDisplay(config, LaserDetectorItemCodes.PassiveTest);
        PowerConfigDisplay = BuildPowerDisplay(config);

        // 按方案配置的工位数量重建工位卡片（1～3）
        int stationCount = Math.Clamp(plan.StationCount, 1, LaserDetectorProtocolStationCount);
        if (stationCount != StationCount)
        {
            StationCount = stationCount;
            RebuildStations(stationCount);
        }
    }

    /// <summary>
    /// 构建有源/无源测试方式的预览文本
    /// </summary>
    private static string BuildTestModeDisplay(LaserDetectorConfig config, string code)
    {
        var item = config.GetItem(code);
        if (item is not { IsEnabled: true })
        {
            return "关闭";
        }

        return item.TestMode switch
        {
            "01" => "01-仅测试输出功能",
            "02" => "02-测试输入和输出功能",
            _ => item.TestMode ?? string.Empty
        };
    }

    /// <summary>
    /// 构建功耗配置的预览文本
    /// </summary>
    private static string BuildPowerDisplay(LaserDetectorConfig config)
    {
        var item = config.GetItem(LaserDetectorItemCodes.PowerConsumption);
        if (item is not { IsEnabled: true })
        {
            return "关闭";
        }

        return $"{item.LowerLimit}~{item.UpperLimit}";
    }

    /// <summary>
    /// 扫码或回车触发指定工位的自动检测
    /// </summary>
    /// <param name="station">工位号 1～3</param>
    private async Task TriggerStationTestAsync(int station)
    {
        var state = Stations.FirstOrDefault(s => s.Station == station);
        if (state == null || state.IsAutoTesting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state.EquipmentCode))
        {
            return;
        }

        if (!IsSerialOpen)
        {
            _messageService.ShowWarning("请先打开串口后再开始测试！");
            return;
        }

        // 重置该工位为新一轮检测初始状态并启动计时
        state.IsAutoTesting = true;
        state.IsAutoPassed = false;
        state.StatusColor = "Orange";
        state.StatusText = "自动测试中...";
        state.TestDuration = "00:00";
        state.PropertyItems = new ObservableCollection<LaserDetectorResultDisplayItem>();
        _stopwatches[station - 1] = Stopwatch.StartNew();
        if (!_durationTimer.IsEnabled)
        {
            _durationTimer.Start();
        }

        try
        {
            var result = await _detectService.DetectAsync(_portName, station, Config);
            if (result == null)
            {
                // 通信失败：工位置红并直接保存不合格记录
                state.StatusColor = "Red";
                state.StatusText = "通信失败";
                SaveRecord(state, isPass: false, result: null);
                return;
            }

            // 填充全部启用配置项的只读结果展示（红绿判定）
            state.PropertyItems = new ObservableCollection<LaserDetectorResultDisplayItem>(
                result.CreatePropertyItems(Config));

            if (!result.IsAutomaticTestPassed(Config))
            {
                // 自动检测项未全部通过：置红保存不合格记录
                state.StatusColor = "Red";
                state.StatusText = "自动测试不合格";
                SaveRecord(state, isPass: false, result);
                return;
            }

            // 自动项全部通过：进入人工测试阶段，由操作员判定 LED/蜂鸣器/按键
            state.IsAutoPassed = true;
            state.StatusColor = "Orange";
            state.StatusText = "请进行人工测试";
        }
        finally
        {
            _stopwatches[station - 1]?.Stop();
            state.IsAutoTesting = false;
        }
    }

    /// <summary>
    /// 评估工位综合结果：自动项通过且人工项全部正常→合格；存在异常→不合格；否则保持人工测试中
    /// </summary>
    private void EvaluateStationResult(LaserDetectorStationState state)
    {
        if (!state.IsAutoPassed || string.IsNullOrWhiteSpace(state.EquipmentCode))
        {
            return;
        }

        bool hasFail = state.LedResult == "异常" || state.BuzzerResult == "异常" || state.ButtonResult == "异常";
        bool allPass = state.LedResult == "正常" && state.BuzzerResult == "正常" && state.ButtonResult == "正常";

        if (hasFail)
        {
            state.StatusColor = "Red";
            state.StatusText = "不合格";
            SaveRecord(state, isPass: false, result: null);
        }
        else if (allPass)
        {
            state.StatusColor = "Green";
            state.StatusText = "合格";
            SaveRecord(state, isPass: true, result: null);
        }
        else
        {
            state.StatusColor = "Orange";
            state.StatusText = "人工测试中...";
        }
    }

    /// <summary>
    /// 保存检测记录（后台执行，不阻塞界面）
    /// </summary>
    private void SaveRecord(LaserDetectorStationState state, bool isPass, LaserDetectorTestResult? result)
    {
        var testData = new List<object>
        {
            new { Name = "有源测试", Value = ActiveTestDisplay, Result = "1" },
            new { Name = "无源测试", Value = PassiveTestDisplay, Result = "1" },
            new { Name = "功耗配置", Value = PowerConfigDisplay, Result = "1" },
            new { Name = "LED指示灯", Value = state.LedResult, Result = state.LedResult == "正常" ? "1" : "0" },
            new { Name = "蜂鸣器", Value = state.BuzzerResult, Result = state.BuzzerResult == "正常" ? "1" : "0" },
            new { Name = "按键", Value = state.ButtonResult, Result = state.ButtonResult == "正常" ? "1" : "0" }
        };

        if (result != null)
        {
            // 自动测试结果项一并写入检测内容（名称=实测值=判定）
            foreach (var item in result.CreatePropertyItems(Config))
            {
                testData.Add(new { Name = item.Name, Value = item.Value, Result = item.IsPassed ? "1" : "0" });
            }
        }

        var record = new TestRecordDto
        {
            ProductSn = state.EquipmentCode,
            ProductCode = ProductCode,
            Category = ProductCategory.GasDG,
            TestResult = isPass,
            TestContentJson = JsonSerializer.Serialize(testData),
            CreatedBy = _currentContext.UserName,
            CreatedTime = DateTime.Now
        };

        Task.Run(async () => await _testRecordService.SaveBoardRecordAsync(record));
    }

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // 离开页面时关闭串口并停止计时，避免占用
        _durationTimer.Stop();
        if (IsSerialOpen && !string.IsNullOrEmpty(_portName))
        {
            _ = _detectService.ClosePortAsync(_portName);
            IsSerialOpen = false;
        }
    }

    /// <inheritdoc/>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        var materialNumber = navigationContext.Parameters.GetValue<string>("MaterialNumber");
        if (!string.IsNullOrEmpty(materialNumber))
        {
            CurrentMaterialNumber = materialNumber;
        }

        RefreshPorts();
    }
}
