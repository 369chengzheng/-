// =============================================================================
// 文件名：LaserDetectorViewModel.cs
// 功能描述：激光检测仪校验界面视图模型（严格 MVVM，参考 IMDS 项目同名界面移植）。
//           按方案的工位数量动态生成工位卡片；扫码触发自动测试 → 轮询结果
//           → 结果项（全部配置项）只读展示 → 人工测试项（LED/蜂鸣器/按键）
//           → 综合判定合格性并保存检测记录。
//           ViewModel 零 UI 引用：提示统一经 IMessageService（Growl 轻提示），
//           工位状态以 Phase 枚举表达（界面表现由 XAML 触发器映射），
//           秒级耗时刷新由视图层计时器驱动 RefreshDurationsCommand。
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
using IMDP.Infrastructure.Logging;
using IMDP.Products.GasDG.LaserDetector.Models;
using IMDP.Products.GasDG.LaserDetector.Services;
using NLog;
namespace IMDP.Products.GasDG.LaserDetector.ViewModels;

/// <summary>
/// 激光检测仪校验界面视图模型
/// <para>按测试方案的工位数量（数据库配置，不设上限）动态生成工位卡片；每工位独立流程：
/// 表具条码扫码触发自动检测 → 自动结果按启用配置项全部只读展示 →
/// 操作员对 LED/蜂鸣器/按键人工判定 → 全部通过才判合格，结果经
/// <see cref="ITestRecordService"/> 保存为板卡检测记录。</para>
/// </summary>
public partial class LaserDetectorViewModel : ObservableObject, INavigationAware
{
    /// <summary>NLog 日志器（串口打开/关闭与指令发送记录）</summary>
    private static readonly ILogger _logger = LogHelper.For<LaserDetectorViewModel>();

    private readonly ITestPlanService _testPlanService;
    private readonly ITestRecordService _testRecordService;
    private readonly LaserDetectorDetectService _detectService;
    private readonly ICurrentContext _currentContext;
    private readonly IEventAggregator _eventAggregator;
    private readonly IMessageService _messageService;

    /// <summary>串口名称缓存（打开串口后记录，关闭与发送时使用）</summary>
    private string _portName = string.Empty;

    /// <summary>各工位测试计时器（键 = 工位号；字典随工位数动态扩容，无固定上限）</summary>
    private readonly Dictionary<int, Stopwatch> _stopwatches = new();

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
    [NotifyPropertyChangedFor(nameof(CanOpenSerial))]
    private bool _isSerialOpen;

    /// <summary>打开串口按钮可用性（串口未打开时可用）</summary>
    public bool CanOpenSerial => !IsSerialOpen;

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

    /// <summary>当前方案的工位数量（来源于数据库方案配置，决定工位卡片生成个数，不设上限）</summary>
    [ObservableProperty]
    private int _stationCount = 3;

    /// <summary>
    /// 单工位检测最大超时时间（秒；产品编码加载方案后更新，检测服务轮询结果的总超时）
    /// </summary>
    [ObservableProperty]
    private int _maxTestTimeout = 120;

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

    /// <summary>
    /// 自动检测结果表格行集合（行 = 检测项目，列 = 工位）
    /// <para>产品编码加载方案后按启用配置项顺序生成（含序号与项目名称的左侧固定列）；
    /// 检测完成后按工位写入对应单元格的实测值与红绿判定。</para>
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LaserDetectorResultRow> _resultRows = new();

    /// <summary>当前打开的串口名称（工位表头下方展示；未打开时为空）</summary>
    [ObservableProperty]
    private string _openedPortName = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="testPlanService">测试计划服务（按产品编码加载方案）</param>
    /// <param name="testRecordService">检测记录服务（保存检测结果）</param>
    /// <param name="detectService">激光检测服务（串口交互与协议解析）</param>
    /// <param name="currentContext">当前用户上下文（记录操作员）</param>
    /// <param name="eventAggregator">事件聚合器（返回首页）</param>
    /// <param name="messageService">统一消息提示服务（Growl 轻提示/对话框）</param>
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

        RebuildStations(StationCount);
        RefreshPorts();
    }

    /// <summary>
    /// 刷新进行中工位的测试耗时文本（每秒由视图计时器驱动）
    /// <para>命令由 <see cref="LaserDetectorView"/> 的秒级计时器执行，
    /// UI 计时归视图，VM 只做纯计算。</para>
    /// </summary>
    [RelayCommand]
    private void RefreshDurations()
    {
        foreach (var pair in _stopwatches)
        {
            if (pair.Value is not { IsRunning: true })
            {
                continue;
            }

            var state = Stations.FirstOrDefault(s => s.Station == pair.Key);
            if (state != null)
            {
                state.TestDurationTime = pair.Value.Elapsed;
                state.TestDuration = pair.Value.Elapsed.ToString(@"mm\:ss");
                // 耗时变化同步刷新表格行 2（检测状态：耗时-结果）的该工位单元格
                RefreshStatusCell(state);
            }
        }
    }

    /// <summary>
    /// 按方案工位数量重建工位状态列表
    /// <para>工位数量来源于数据库方案配置，无固定上限；在原集合上 Clear + Add
    /// （触发 CollectionChanged），确保 ItemsControl 立即按新工位数刷新卡片。</para>
    /// </summary>
    /// <param name="stationCount">工位数量（≥1，来源数据库）</param>
    private void RebuildStations(int stationCount)
    {
        foreach (var state in Stations)
        {
            state.PropertyChanged -= OnStationPropertyChanged;
        }

        Stations.Clear();
        for (int station = 1; station <= stationCount; station++)
        {
            var state = new LaserDetectorStationState(station);
            state.PropertyChanged += OnStationPropertyChanged;
            Stations.Add(state);
        }
    }

    /// <summary>
    /// 工位状态变化时同步表格并重新评估该工位的综合结果
    /// <para>人工测试项变化（表格行 4-6 下拉）→ 同步回工位状态并评估最终结果；
    /// Phase 变化 → 刷新表格行 2（检测状态）。</para>
    /// </summary>
    private void OnStationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not LaserDetectorStationState station)
        {
            return;
        }

        // 人工测试项变化：重新评估最终结果
        if (e.PropertyName == nameof(LaserDetectorStationState.LedResult) ||
            e.PropertyName == nameof(LaserDetectorStationState.BuzzerResult) ||
            e.PropertyName == nameof(LaserDetectorStationState.ButtonResult))
        {
            EvaluateStationResult(station);
            return;
        }

        // 检测阶段变化：刷新表格行 2（检测状态：耗时-结果）
        if (e.PropertyName == nameof(LaserDetectorStationState.Phase))
        {
            RefreshStatusCell(station);
        }
    }

    /// <summary>
    /// 返回首页（由 [RelayCommand] 生成 GoHomeCommand）
    /// </summary>
    [RelayCommand]
    private void GoHome()
    {
        _eventAggregator.GetEvent<GoHomeEvent>().Publish();
    }

    /// <summary>
    /// 刷新可用串口列表（自动选中第一个；由 [RelayCommand] 生成 RefreshPortsCommand）
    /// </summary>
    [RelayCommand]
    private void RefreshPorts()
    {
        Ports = new ObservableCollection<string>(_detectService.GetAvailablePorts());
        if (Ports.Count > 0 && string.IsNullOrEmpty(SelectedPort))
        {
            SelectedPort = Ports[0];
        }
    }

    /// <summary>
    /// 打开选中的串口（由 [RelayCommand] 生成 OpenSerialCommand）
    /// </summary>
    [RelayCommand]
    private async Task OpenSerialAsync()
    {
        if (string.IsNullOrEmpty(SelectedPort))
        {
            _messageService.GrowlWarning("请先选择一个串口！");
            return;
        }

        var portConfig = await _detectService.OpenPortAsync(SelectedPort);
        IsSerialOpen = portConfig != null;
        if (IsSerialOpen)
        {
            _portName = SelectedPort;
            OpenedPortName = SelectedPort;
            _logger.Info(
                $"已打开串口：{_portName}，波特率={portConfig!.BaudRate}，数据位={portConfig.DataBits}，停止位={portConfig.StopBits}，校验位={portConfig.Parity}");
            _messageService.GrowlSuccess($"已打开串口: {_portName}");
        }
        else
        {
            _logger.Warn($"串口 {SelectedPort} 打开失败");
            _messageService.GrowlWarning($"串口 {SelectedPort} 打开失败，请检查占用或连接。");
        }

        // 串口状态变化后同步表格固定行（行 1 红外串口显示当前串口号）
        RefreshFixedRows();
    }

    /// <summary>
    /// 关闭当前串口并清空全部工位的进行中状态（由 [RelayCommand] 生成 CloseSerialCommand）
    /// </summary>
    [RelayCommand]
    private async Task CloseSerialAsync()
    {
        if (string.IsNullOrEmpty(_portName))
        {
            return;
        }

        await _detectService.ClosePortAsync(_portName);
        _logger.Info($"已关闭串口：{_portName}");
        OpenedPortName = string.Empty;
        IsSerialOpen = false;
        // 串口关闭后同步表格固定行（行 1 红外串口清空）
        RefreshFixedRows();
        foreach (var station in Stations)
        {
            station.Reset();
        }

        _stopwatches.Clear();
        _messageService.GrowlInfo("串口已关闭！");
    }

    /// <summary>
    /// 产品编码失焦或键入空格后按编码从方案库加载检测配置
    /// <para>由 [RelayCommand] 生成 ProductLostFocusCommand；存在该编码的方案时
    /// 加载到配置预览区；不存在时以 Growl 提示。</para>
    /// </summary>
    [RelayCommand]
    private async Task ProductLostFocusAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            return;
        }

        var plan = await _testPlanService.GetBoardConfigAsync(ProductCode, ProductCategory.GasDG);
        if (plan == null)
        {
            // 未找到方案：Growl 信息提示（不阻塞操作员视线）
            _messageService.GrowlInfo($"产品编码 [{ProductCode}] 不存在配置！");
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

        // 工位数量完全以数据库方案配置为准（不设上限），按实际数量重建工位卡片；
        // 无条件重建，避免连续加载相同工位数的不同方案时残留上一轮工位状态
        int stationCount = Math.Max(1, plan.StationCount);
        StationCount = stationCount;
        RebuildStations(stationCount);

        // 查询配置后按工位数创建各工位独立通信单元：
        // 每个工位独立一个 List 接收缓冲 + 一个 Channel 发送队列用于发送数据，
        // 工位之间互不干扰；工位检测解析完成后由检测服务立即清理对应单元
        _detectService.ConfigureStations(stationCount);

        // 记录方案配置的单工位检测总超时（秒），检测轮询结果时使用；
        // 超时时间作为参数项存于方案 JSON（旧方案无该项时回退默认 120 秒）
        MaxTestTimeout = LaserDetectorTimeoutParameter.Resolve(plan.Parameters);

        // 按启用配置项顺序重建结果表格行（序号 + 项目名称 + 各工位空单元格），
        // 作为表格最前方的"待检测项目"列；未启用的项不出现
        RebuildResultRows(config, stationCount);
    }

    /// <summary>
    /// 按固定行序 + 启用配置项重建结果表格行
    /// <para>固定行序（按产线要求）：
    /// 1=红外串口（打开的串口号）→ 2=检测状态（耗时-当前状态）→ 3=人工检测项LED
    /// → 4=蜂鸣器 → 5=按键（默认全部"正常"）→ 6=表具条码（扫码框）
    /// → 7..n=方案已启用配置项（自动检测红绿结果，序号自 7 连续编号）；
    /// 未启用的配置项不出现。
    /// 固定行（红外串口/检测状态/人工项/表具条码）均不加红绿底色，
    /// 仅配置项结果行与检测状态行按判定/阶段配色。</para>
    /// </summary>
    /// <param name="config">当前检测配置（取启用项生成配置行）</param>
    /// <param name="stationCount">工位数量（决定每行的单元格列数）</param>
    private void RebuildResultRows(LaserDetectorConfig config, int stationCount)
    {
        var rows = new ObservableCollection<LaserDetectorResultRow>
        {
            // 1 红外串口：单元格显示已打开的串口号（不加底色）
            new(1, "红外串口", stationCount, LaserDetectorResultRowKind.Auto),
            // 2 检测状态：耗时-当前状态（0:00-待检测 / 0:0x-检测中 / x:xx-合格/不合格）
            new(2, "检测状态", stationCount, LaserDetectorResultRowKind.Auto),
            // 3-5 人工测试项：默认全部"正常"（不加底色）
            new(3, "LED", stationCount, LaserDetectorResultRowKind.Manual),
            new(4, "蜂鸣器", stationCount, LaserDetectorResultRowKind.Manual),
            new(5, "按键", stationCount, LaserDetectorResultRowKind.Manual),
            // 6 表具条码：扫码输入框（不加底色）
            new(6, "表具条码", stationCount, LaserDetectorResultRowKind.Barcode)
        };

        // 人工行单元格标记（下拉绑定与工位状态同步）
        foreach (var manualRow in rows.Where(r => r.Kind == LaserDetectorResultRowKind.Manual))
        {
            foreach (var cell in manualRow.Cells)
            {
                cell.IsManual = true;
            }
        }

        // 7..n 配置项行：按启用配置项顺序生成
        int index = 7;
        foreach (var item in config.Items.Where(i => i.IsEnabled))
        {
            rows.Add(new LaserDetectorResultRow(index++, item.Name, stationCount, LaserDetectorResultRowKind.Auto));
        }

        ResultRows = rows;

        // 固定行与工位状态的双向同步：
        // 人工行（3-5）下拉变化 → 写回工位状态（参与综合判定与记录保存）；
        // 条码行（6）扫码输入 → 写回工位 EquipmentCode（检测命令取值）
        SyncManualRows(rows);
        SyncBarcodeRow(rows);

        // 行 1（红外串口）与行 2（检测状态）按当前串口/工位状态立即填充
        RefreshFixedRows();
    }

    /// <summary>
    /// 建立人工测试行（LED/蜂鸣器/按键）单元格与工位状态的双向同步
    /// <para>表格行 3-5 的下拉选择（ManualResult）→ 写回对应工位的
    /// LedResult/BuzzerResult/ButtonResult（触发综合判定与记录保存）；
    /// 默认值"正常"（新一轮检测人工项默认全部正常）。</para>
    /// </summary>
    /// <param name="rows">新构建的表格行集合</param>
    private void SyncManualRows(ObservableCollection<LaserDetectorResultRow> rows)
    {
        // (行名, 工位状态属性回写委托) 映射
        var manualRowMap = new (string RowName, Action<LaserDetectorStationState, string> Write)[]
        {
            ("LED", (state, value) => state.LedResult = value),
            ("蜂鸣器", (state, value) => state.BuzzerResult = value),
            ("按键", (state, value) => state.ButtonResult = value)
        };

        foreach (var (rowName, write) in manualRowMap)
        {
            var row = rows.FirstOrDefault(r => r.Name == rowName);
            if (row == null)
            {
                continue;
            }

            foreach (var cell in row.Cells)
            {
                cell.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(LaserDetectorResultCell.ManualResult))
                    {
                        var state = Stations.FirstOrDefault(s => s.Station == cell.Station);
                        if (state != null)
                        {
                            write(state, cell.ManualResult);
                        }
                    }
                };
            }
        }
    }

    /// <summary>
    /// 建立表具条码行（行 6）单元格与工位状态 EquipmentCode 的双向同步
    /// <para>表格条码框输入 → 写回对应工位的 EquipmentCode（扫码检测命令取值）；
    /// 工位 Reset/重扫清空时反向同步回单元格。</para>
    /// </summary>
    /// <param name="rows">新构建的表格行集合</param>
    private void SyncBarcodeRow(ObservableCollection<LaserDetectorResultRow> rows)
    {
        var barcodeRow = rows.FirstOrDefault(r => r.Kind == LaserDetectorResultRowKind.Barcode);
        if (barcodeRow == null)
        {
            return;
        }

        foreach (var cell in barcodeRow.Cells)
        {
            // 表格输入 → 工位状态（检测命令从 state.EquipmentCode 取值）
            cell.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LaserDetectorResultCell.EquipmentCode))
                {
                    var state = Stations.FirstOrDefault(s => s.Station == cell.Station);
                    if (state != null && state.EquipmentCode != cell.EquipmentCode)
                    {
                        state.EquipmentCode = cell.EquipmentCode;
                    }
                }
            };

            // 工位状态复位（如串口关闭 Reset）→ 清空表格输入框
            var state = Stations.FirstOrDefault(s => s.Station == cell.Station);
            if (state != null)
            {
                state.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(LaserDetectorStationState.EquipmentCode) &&
                        cell.EquipmentCode != state.EquipmentCode)
                    {
                        cell.EquipmentCode = state.EquipmentCode;
                    }
                };

                // 初始同步（重建表格时保留已扫码内容）
                cell.EquipmentCode = state.EquipmentCode;
            }
        }
    }

    /// <summary>单元格底色模式常量：中性（无结果/非配色行）</summary>
    private const string ColorNone = "";

    /// <summary>单元格底色模式常量：通过（绿）</summary>
    private const string ColorGreen = "green";

    /// <summary>单元格底色模式常量：未通过（红）</summary>
    private const string ColorRed = "red";

    /// <summary>单元格底色模式常量：进行中（橙）</summary>
    private const string ColorOrange = "orange";

    /// <summary>
    /// 刷新固定行（行 1 红外串口 / 行 2 检测状态）的全部工位单元格
    /// <para>串口打开/关闭、工位 Phase 与耗时变化时调用，保持表格固定行
    /// 与 VM 状态同步。</para>
    /// </summary>
    private void RefreshFixedRows()
    {
        var portRow = ResultRows.FirstOrDefault(r => r.Name == "红外串口");
        var statusRow = ResultRows.FirstOrDefault(r => r.Name == "检测状态");
        if (portRow == null || statusRow == null)
        {
            return;
        }

        foreach (var state in Stations)
        {
            int cellIndex = state.Station - 1;
            var portCell = portRow.Cells.ElementAtOrDefault(cellIndex);
            if (portCell != null)
            {
                // 红外串口行不加底色：仅显示串口号文本
                portCell.Value = OpenedPortName;
                portCell.HasResult = false;
                portCell.ColorMode = ColorNone;
            }

            RefreshStatusCell(state);
        }
    }

    /// <summary>
    /// 刷新单工位的检测状态单元格（行 2：耗时-结果）
    /// <para>Phase 或耗时变化时调用（耗时由视图计时器每秒驱动刷新）；
    /// 底色按阶段映射：合格绿 / 不合格与通信失败红 / 测试与人工中橙 / 待检测中性；
    /// 值与底色均未变化时跳过写入，避免每秒触发无谓的属性通知。</para>
    /// </summary>
    /// <param name="state">工位运行时状态</param>
    private void RefreshStatusCell(LaserDetectorStationState state)
    {
        var statusRow = ResultRows.FirstOrDefault(r => r.Name == "检测状态");
        var cell = statusRow?.Cells.ElementAtOrDefault(state.Station - 1);
        if (cell == null)
        {
            return;
        }

        string display = BuildStatusDisplay(state);
        string color = state.Phase switch
        {
            LaserDetectorStationPhase.Passed => ColorGreen,
            LaserDetectorStationPhase.Failed or LaserDetectorStationPhase.CommFailed => ColorRed,
            LaserDetectorStationPhase.AutoTesting or LaserDetectorStationPhase.AwaitingManual
                or LaserDetectorStationPhase.ManualTesting => ColorOrange,
            _ => ColorNone
        };

        if (cell.Value == display && cell.ColorMode == color)
        {
            return;
        }

        cell.Value = display;
        cell.ColorMode = color;
        cell.HasResult = color != ColorNone;
    }

    /// <summary>
    /// 构建工位检测状态单元格的展示文本（耗时-当前状态）
    /// <para>Phase 映射文案：0:00-待检测 / 0:0x-检测中 / x:xx-合格/不合格；
    /// 耗时格式 m:ss（分不补零、秒两位），检测进行中每秒刷新
    /// （由视图计时器驱动）。</para>
    /// </summary>
    /// <param name="state">工位运行时状态</param>
    /// <returns>如「0:00-待检测」「1:05-合格」</returns>
    private static string BuildStatusDisplay(LaserDetectorStationState state)
    {
        // 耗时格式 m:ss（分不补零）；未开始时统一显示 0:00
        string duration = state.Phase == LaserDetectorStationPhase.Idle
            ? "0:00"
            : $"{(int)state.TestDurationTime.TotalMinutes}:{state.TestDurationTime.Seconds:D2}";

        return state.Phase switch
        {
            LaserDetectorStationPhase.Idle => $"{duration}-待检测",
            LaserDetectorStationPhase.AutoTesting => $"{duration}-检测中",
            LaserDetectorStationPhase.ManualTesting => $"{duration}-人工测试中",
            LaserDetectorStationPhase.AwaitingManual => $"{duration}-请人工测试",
            LaserDetectorStationPhase.Passed => $"{duration}-合格",
            LaserDetectorStationPhase.Failed => $"{duration}-不合格",
            LaserDetectorStationPhase.CommFailed => $"{duration}-通信失败",
            _ => $"{duration}-待检测"
        };
    }

    /// <summary>
    /// 将单工位自动检测结果写入结果表格的对应工位列
    /// <para>仅写入配置项行（Kind=Auto 且序号 ≥7，行与配置启用项顺序一致，
    /// 与结果对象 CreatePropertyItems 顺序相同），逐行写入实测值与红绿判定；
    /// 固定行（红外串口/检测状态/条码/人工项）不在此处理；通信失败时由
    /// 调用方保持该工位各单元格空白。</para>
    /// </summary>
    /// <param name="station">工位号（1 起）</param>
    /// <param name="result">自动检测结果</param>
    private void FillStationResult(int station, LaserDetectorTestResult result)
    {
        // 展示项与配置项行按启用配置项顺序一一对应，数量不一致时以行集合为准截断
        var displayItems = result.CreatePropertyItems(Config);
        int cellIndex = station - 1;
        var autoRows = ResultRows.Where(r => r.Kind == LaserDetectorResultRowKind.Auto && r.Index >= 7).ToList();
        for (int row = 0; row < autoRows.Count; row++)
        {
            var cell = autoRows[row].Cells.ElementAtOrDefault(cellIndex);
            if (cell == null)
            {
                continue;
            }

            if (row < displayItems.Count)
            {
                cell.Value = displayItems[row].Value;
                cell.IsPassed = displayItems[row].IsPassed;
                cell.HasResult = true;
                cell.ColorMode = displayItems[row].IsPassed ? ColorGreen : ColorRed;
            }
            else
            {
                cell.Value = string.Empty;
                cell.HasResult = false;
                cell.ColorMode = ColorNone;
            }
        }
    }

    /// <summary>
    /// 清空结果表格中指定工位列的配置项单元格（重新扫码前复位为待检测占位）
    /// </summary>
    /// <param name="station">工位号（1 起）</param>
    private void ClearStationCells(int station)
    {
        int cellIndex = station - 1;
        foreach (var row in ResultRows.Where(r => r.Kind == LaserDetectorResultRowKind.Auto && r.Index >= 7))
        {
            var cell = row.Cells.ElementAtOrDefault(cellIndex);
            if (cell != null)
            {
                cell.Value = string.Empty;
                cell.IsPassed = false;
                cell.HasResult = false;
                cell.ColorMode = ColorNone;
            }
        }
    }

    /// <summary>
    /// 复位指定工位的人工测试单元格（行 3-5 LED/蜂鸣器/按键）为默认"正常"
    /// <para>重新扫码触发新一轮检测时调用，保证人工项默认全部正常（不加底色）。</para>
    /// </summary>
    /// <param name="station">工位号（1 起）</param>
    private void ResetManualCells(int station)
    {
        int cellIndex = station - 1;
        foreach (var row in ResultRows.Where(r => r.Kind == LaserDetectorResultRowKind.Manual))
        {
            var cell = row.Cells.ElementAtOrDefault(cellIndex);
            if (cell != null)
            {
                cell.ManualResult = LaserDetectorStationState.ManualOptions[0];
            }
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
    /// <para>由 [RelayCommand] 生成 StationScanCommand（参数为工位号）。</para>
    /// </summary>
    /// <param name="station">工位号（1 起，来源于数据库方案配置）</param>
    [RelayCommand]
    private async Task StationScanAsync(int station)
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
            _messageService.GrowlWarning("请先打开串口后再开始测试！");
            return;
        }

        // 重置该工位为新一轮检测初始状态并启动计时
        state.IsAutoTesting = true;
        state.IsAutoPassed = false;
        state.Phase = LaserDetectorStationPhase.AutoTesting;
        state.TestDuration = "00:00";
        state.TestDurationTime = TimeSpan.Zero;
        state.PropertyItems = new ObservableCollection<LaserDetectorResultDisplayItem>();
        // 人工判定复位为默认"正常"（新一轮检测人工项默认全部正常）
        state.ResetManualResults();
        ClearStationCells(station);
        ResetManualCells(station);
        _stopwatches[station] = Stopwatch.StartNew();

        try
        {
            var result = await _detectService.DetectAsync(
                station,
                Config,
                MaxTestTimeout,
                // 串口指令发送日志：字节以空格分隔的十六进制输出（替代默认的“-”分隔）
                command => _logger.Info(
                    $"[{_portName}] 工位{station} 发送指令({command.Length}字节)：{BitConverter.ToString(command).Replace('-', ' ')}"));
            if (result == null)
            {
                // 通信失败：工位置红并直接保存不合格记录（结果列保持空白占位）
                state.Phase = LaserDetectorStationPhase.CommFailed;
                await SaveRecordAsync(state, isPass: false, result: null);
                return;
            }

            // 检测结果写入表格该工位对应列（红绿判定），同时保留原只读结果列表供记录保存使用
            FillStationResult(station, result);
            state.PropertyItems = new ObservableCollection<LaserDetectorResultDisplayItem>(
                result.CreatePropertyItems(Config));

            if (!result.IsAutomaticTestPassed(Config))
            {
                // 自动检测项未全部通过：置红保存不合格记录
                state.Phase = LaserDetectorStationPhase.Failed;
                await SaveRecordAsync(state, isPass: false, result);
                return;
            }

            // 自动项全部通过：进入人工测试阶段，由操作员判定 LED/蜂鸣器/按键
            state.IsAutoPassed = true;
            state.Phase = LaserDetectorStationPhase.AwaitingManual;
        }
        finally
        {
            if (_stopwatches.TryGetValue(station, out var stopwatch))
            {
                stopwatch.Stop();
            }

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
            state.Phase = LaserDetectorStationPhase.Failed;
            _ = SaveRecordAsync(state, isPass: false, result: null);
        }
        else if (allPass)
        {
            state.Phase = LaserDetectorStationPhase.Passed;
            _ = SaveRecordAsync(state, isPass: true, result: null);
        }
        else
        {
            state.Phase = LaserDetectorStationPhase.ManualTesting;
        }
    }

    /// <summary>
    /// 保存检测记录（异步执行，不阻塞界面）
    /// <para>内部捕获全部异常并以 Growl 警告提示，保证事件驱动的调用方
    /// （人工项变化触发）可安全地异步丢弃。</para>
    /// </summary>
    /// <param name="state">工位运行时状态</param>
    /// <param name="isPass">综合判定结果（true=合格）</param>
    /// <param name="result">自动检测结果（通信失败时为 null）</param>
    private async Task SaveRecordAsync(LaserDetectorStationState state, bool isPass, LaserDetectorTestResult? result)
    {
        try
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

            await _testRecordService.SaveBoardRecordAsync(record);
        }
        catch (Exception ex)
        {
            _messageService.GrowlWarning($"检测记录保存失败：{ex.Message}");
        }
    }

    /// <inheritdoc/>
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    /// <inheritdoc/>
    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        // 离开页面：关闭串口（避免占用）并整体重置校验界面为初始空白状态
        //（不保留任何已加载的配置与检测结果；秒级计时器由视图层 Unloaded 自行停止）
        ResetPageAsync();
    }

    /// <summary>
    /// 整体重置校验界面为初始空白状态
    /// <para>关闭串口、清空产品编码与配置预览、复位全部工位状态与结果表格、
    /// 清除计时器；用于离开页面导航与需要完全复位场景（不加载任何配置）。</para>
    /// </summary>
    private async void ResetPageAsync()
    {
        // 1 关闭串口（未打开时跳过）
        if (IsSerialOpen && !string.IsNullOrEmpty(_portName))
        {
            await _detectService.ClosePortAsync(_portName);
            _logger.Info($"导航离开，已自动关闭串口：{_portName}");
        }

        _portName = string.Empty;
        OpenedPortName = string.Empty;
        IsSerialOpen = false;

        // 2 清空产品编码与配置预览（恢复默认 19 项、关闭状态）
        ProductCode = string.Empty;
        Config = new LaserDetectorConfig { Items = LaserDetectorConfigDefaults.CreateDefaultItems() };
        ActiveTestDisplay = string.Empty;
        PassiveTestDisplay = string.Empty;
        PowerConfigDisplay = string.Empty;
        StationCount = 3;

        // 3 复位全部工位状态（条码/人工项/耗时清零）并重建默认 3 工位
        RebuildStations(3);

        // 4 清空结果表格（回到无配置的空白表格）
        ResultRows = new ObservableCollection<LaserDetectorResultRow>();

        // 5 清除全部计时器
        _stopwatches.Clear();
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
