using IMDP.Application.DTOs;
using System.IO;
using System.Text.Json;
using IMDP.Application.Events;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.AlarmHost.Models;
using IMDP.Products.GasDG.AlarmHost.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Navigation.Regions;
using NLog;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>
/// 设置/检验共用查询及工位流程，协议执行与本地记录分别复用独立服务，MES接入暂缓
/// </summary>
public sealed class AlarmHostViewModel : AlarmHostPageViewModel
{
    /// <summary>
    /// 当前报警主机插件，用于取得设置或检验入口名称
    /// </summary>
    private readonly GasDGAlarmHostPlugin _plugin;
    /// <summary>
    /// 公共方案服务，按插件、物料、类型及产品/客户编码精确查询
    /// </summary>
    private readonly ITestPlanService _planService;
    /// <summary>
    /// 平台统一消息服务
    /// </summary>
    private readonly IMessageService _messages;
    /// <summary>
    /// 报警主机设备执行服务，协议及项目转换不放在页面中
    /// </summary>
    private readonly AlarmHostExecutionService _executor;
    /// <summary>
    /// 原有公共检测记录服务，保存本次设备执行结果
    /// </summary>
    private readonly ITestRecordService _testRecordService;
    /// <summary>
    /// 复用平台串口与命令参数，不在报警页另建配置存储
    /// </summary>
    private readonly IGlobalConfigService _globalConfig;
    /// <summary>
    /// 结束工单交由宿主清理当前菜单和生效物料
    /// </summary>
    private readonly IEventAggregator _events;
    /// <summary>
    /// 将串口后台状态通知送回创建页面的UI上下文
    /// </summary>
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    /// <summary>
    /// 记录异常堆栈，不输出MES内容或网络密码
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>
    /// 关闭旧连接的任务，下一次查询或打开前等待其完成
    /// </summary>
    private Task _closingConnections = Task.CompletedTask;
    /// <summary>
    /// 打开连接时读取的公共命令参数快照
    /// </summary>
    private IReadOnlyList<ProtocolCommand> _commands = [];
    /// <summary>
    /// 导航时取得的已生效工单号，手动物料查询时为空
    /// </summary>
    private string _orderNumber = "";
    /// <summary>
    /// 当前登录工位的MES资源编号，与工位列序号分开
    /// </summary>
    private string _resourceCode = "";
    /// <summary>
    /// 导航时的操作员工号，结果保存不改用Windows用户
    /// </summary>
    private string _operatorWorkNumber = "";
    /// <summary>
    /// 每个运行工位独立的取消源，离页或清空方案时统一通知取消
    /// </summary>
    private readonly Dictionary<AlarmHostStation, CancellationTokenSource> _executions = [];

    /// <summary>
    /// 本次加载方案后完成并通过的执行次数，重试完成也计一次
    /// </summary>
    private int _passedCount;

    /// <summary>
    /// 本次加载方案后完成但不通过的执行次数，取消和通信中断不计入
    /// </summary>
    private int _failedCount;

    /// <summary>
    /// 本轮设备项目合格次数，重新加载或清空方案后归零
    /// </summary>
    public int PassedCount
    {
        get => _passedCount;
        private set { if (SetProperty(ref _passedCount, value)) OnPropertyChanged(nameof(StatisticsText)); }
    }

    /// <summary>
    /// 本轮设备项目不合格次数，不包含未完成的通信中止
    /// </summary>
    public int FailedCount
    {
        get => _failedCount;
        private set { if (SetProperty(ref _failedCount, value)) OnPropertyChanged(nameof(StatisticsText)); }
    }

    /// <summary>
    /// 仅统计设备项目执行，不表示MES或本地记录保存成功
    /// </summary>
    public string StatisticsText => $"本轮设备项目：合格 {PassedCount}  不合格 {FailedCount}";
    /// <summary>
    /// 最近枚举到的真实端口列表，不打开连接
    /// </summary>
    private string[] _availablePorts = [];
    /// <summary>
    /// 导航中的方案类型键，WholeSet=设置、WholeGet=检验，空值表示未指定有效类型
    /// </summary>
    private string? _planTypeKey;
    /// <summary>
    /// 查询输入的产品编码
    /// </summary>
    private string _productCode = "";
    /// <summary>
    /// 查询输入的客户编码
    /// </summary>
    private string _customerCode = "";
    /// <summary>
    /// 当前是否正在查询，查询期间统一禁用编码输入及查询按钮
    /// </summary>
    private bool _isBusy;
    /// <summary>
    /// 当前导航代数，仅用于忽略离页前的迟到结果，不是数据库版本
    /// </summary>
    private int _contextVersion;
    /// <summary>
    /// 最近查询成功的原始方案，显示转换不修改其中参数
    /// </summary>
    private TestPlanDto? _loadedPlan;
    /// <summary>
    /// 只读方案区是否展开
    /// </summary>
    private bool _isPlanVisible;
    /// <summary>
    /// 当前物料中设置、检验各自最近方案的手动展开状态，仅保留在本页面会话内
    /// </summary>
    private readonly Dictionary<int, (string ProductCode, string CustomerCode, bool IsVisible)> _planVisibility = [];
    /// <summary>
    /// 已启用功能的只读显示集合
    /// </summary>
    private IReadOnlyList<AlarmHostPlanItem> _items = [];
    /// <summary>
    /// 当前方案已勾选项目对应的结果行，重新查询时整体重建
    /// </summary>
    private IReadOnlyList<AlarmHostResultRow> _resultRows = [];
    /// <summary>
    /// 当前方案工位的独立扫码状态，查询成功后按工位数量创建
    /// </summary>
    private AlarmHostStation[] _stations = [];
    /// <summary>
    /// 当前查询状态提示
    /// </summary>
    private string _queryMessage = "请输入产品编码和客户编码后查询方案。";

    public AlarmHostViewModel(GasDGAlarmHostPlugin plugin, IEventAggregator eventAggregator,
        ITestPlanService planService, IMessageService messages, AlarmHostExecutionService executor,
        ITestRecordService testRecordService, IGlobalConfigService globalConfig)
        : base(plugin, eventAggregator, FunctionModuleType.WholeSet)
    {
        _plugin = plugin;
        _planService = planService;
        _messages = messages;
        _executor = executor;
        _testRecordService = testRecordService;
        _globalConfig = globalConfig;
        _events = eventAggregator;
        PageTitle = plugin.DisplayName + "设置/检验（未指定类型）";
        QueryCommand = new DelegateCommand(async () => await QueryAsync(), () => CanQuery);
        TogglePlanCommand = new DelegateCommand(TogglePlanVisibility, () => HasPlan);
        RefreshPortsCommand = new DelegateCommand(RefreshPorts, () => !IsBusy);
        TogglePortsCommand = new DelegateCommand(async () => await TogglePortsAsync(), () => HasPlan && !IsBusy);
        EndWorkOrderCommand = new DelegateCommand(async () => await EndWorkOrderAsync(), () => !IsBusy && !IsExecuting);
        ExecuteStationCommand = new DelegateCommand<AlarmHostStation>(async station => await ExecuteStationAsync(station),
            station => CanExecutePlan &&
                !IsBusy && station is { IsRunning: false } && _executor.IsPortOpen(station.PortName));
    }

    /// <summary>
    /// 产品编码输入，改动后清除此前加载的方案，避免与新编码混用
    /// </summary>
    public string ProductCode
    {
        get => _productCode;
        set { if (SetProperty(ref _productCode, value)) ClearPlan(); }
    }

    /// <summary>
    /// 客户编码输入，改动后清除此前加载的方案
    /// </summary>
    public string CustomerCode
    {
        get => _customerCode;
        set { if (SetProperty(ref _customerCode, value)) ClearPlan(); }
    }

    /// <summary>
    /// 查询忙状态，结束或失败后恢复输入
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanQuery));
            QueryCommand.RaiseCanExecuteChanged();
            ExecuteStationCommand.RaiseCanExecuteChanged();
            TogglePortsCommand.RaiseCanExecuteChanged();
            EndWorkOrderCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 当前页面具备导航范围且没有进行中的查询或设备读取
    /// </summary>
    public bool CanQuery => HasValidContext && !IsBusy && !IsExecuting;

    /// <summary>
    /// 当前页面持有连接的操作文字；断线后可关闭残留并重新打开
    /// </summary>
    public string TogglePortsText => _executor.IsConnectionOpen ? "关闭串口" : "打开串口";

    /// <summary>
    /// 当前导航的工单与MES资源，仅显示已生效快照
    /// </summary>
    public string WorkOrderText => $"工单：{(_orderNumber.Length == 0 ? "手动物料" : _orderNumber)}    资源编码：{_resourceCode}    MES未接入";

    /// <summary>
    /// 是否有工位执行中，只限制重新查询和串口刷新，不冻结其他工位输入
    /// </summary>
    public bool IsExecuting => _executions.Count > 0 || _stations.Any(station => station.IsRunning);

    /// <summary>
    /// 端口列表由刷新按钮更新，空列表不生成模拟 COM 口
    /// </summary>
    public IReadOnlyList<string> AvailablePorts => _availablePorts;

    /// <summary>
    /// 本阶段所需的报警主机及常用设备波特率
    /// </summary>
    public IReadOnlyList<int> BaudRates { get; } = Array.AsReadOnly(new[] { 4800, 9600 });

    /// <summary>
    /// 是否已经加载完整且有效的当前方案
    /// </summary>
    public bool HasPlan => _loadedPlan != null;

    /// <summary>
    /// 已启用功能及其 MES 码和参数，只用于只读显示
    /// </summary>
    public IReadOnlyList<AlarmHostPlanItem> Items => _items;

    /// <summary>
    /// 与只读预览采用相同目录顺序的结果行，携带原始参数及独立工位结果
    /// </summary>
    public IReadOnlyList<AlarmHostResultRow> ResultRows => _resultRows;

    /// <summary>
    /// 新增展示项尚未接执行时整份方案仅预览，避免只跑旧项目后显示整份合格
    /// 已明确暂缓的广播继续沿用原排除汇总规则
    /// </summary>
    private bool CanExecutePlan => HasPlan && !HasPendingExecutionItems && ResultRows.Any(row => row.CanExecute);

    /// <summary>
    /// 查询提示、打开串口提示及执行入口共用同一待接入判断；广播沿用已明确的暂缓规则
    /// </summary>
    private bool HasPendingExecutionItems => ResultRows.Any(row => !row.CanExecute &&
        row.Code != AlarmHostFunctionCatalog.BluetoothBroadcast);

    /// <summary>
    /// 有方案参数的启用项目，普通项与复杂项按原顺序在同一区域换行
    /// </summary>
    public IEnumerable<AlarmHostPlanItem> ParameterItems => Items.Where(item => item.Parameters.Count > 0);

    /// <summary>
    /// 无方案参数的启用项目，统一放在参数区之后，不按设置或检验类型区分
    /// </summary>
    public IEnumerable<AlarmHostPlanItem> NoParameterItems => Items.Where(item => item.Parameters.Count == 0);

    /// <summary>
    /// 实际加载方案的启用项目数量，工位数量单独显示在参数末尾
    /// </summary>
    public string PlanSummary => _loadedPlan == null ? "" : $"启用项目：{Items.Count} 项";

    /// <summary>
    /// 实际方案维护的工位数量，按输入值展示，不限定为四或八工位
    /// </summary>
    public int StationCount => _loadedPlan?.StationCount ?? 0;

    /// <summary>
    /// 当前方案的工位、扫码及单项读取状态，不预设检测合格
    /// </summary>
    public IReadOnlyList<AlarmHostStation> Stations => _stations;

    /// <summary>
    /// 当前加载方案的维护信息，显示实际返回值
    /// </summary>
    public string PlanMaintenance => _loadedPlan == null ? "" :
        $"维护人：{_loadedPlan.UpdatedBy}    维护时间：{_loadedPlan.UpdatedTime:yyyy-MM-dd HH:mm:ss}";

    /// <summary>
    /// 方案区展开状态，收起时仍保留当前加载方案
    /// </summary>
    public bool IsPlanVisible
    {
        get => _isPlanVisible;
        private set
        {
            if (SetProperty(ref _isPlanVisible, value)) OnPropertyChanged(nameof(TogglePlanText));
        }
    }

    /// <summary>
    /// 方案显示切换按钮的文字
    /// </summary>
    public string TogglePlanText => IsPlanVisible ? "隐藏方案" : "显示方案";

    /// <summary>
    /// 查询反馈文字，离页旧请求不更新该提示
    /// </summary>
    public string QueryMessage
    {
        get => _queryMessage;
        private set => SetProperty(ref _queryMessage, value);
    }

    /// <summary>
    /// 精确查询当前类型方案
    /// </summary>
    public DelegateCommand QueryCommand { get; }

    /// <summary>
    /// 展开或收起只读方案区
    /// </summary>
    public DelegateCommand TogglePlanCommand { get; }

    /// <summary>
    /// 刷新本机串口列表，不自动打开
    /// </summary>
    public DelegateCommand RefreshPortsCommand { get; }

    /// <summary>
    /// 打开全部已选择端口或关闭本页连接，关闭时取消排队及设备等待
    /// </summary>
    public DelegateCommand TogglePortsCommand { get; }

    /// <summary>
    /// 确认后结束本地工单并返回空首页，不发送虚构的MES结单指令
    /// </summary>
    public DelegateCommand EndWorkOrderCommand { get; }

    /// <summary>
    /// 按当前加载方案执行指定工位，也用于失败后手动重试整份方案
    /// </summary>
    public DelegateCommand<AlarmHostStation> ExecuteStationCommand { get; }

    /// <summary>
    /// 当前执行页的方案类型导航键，WholeSet=设置、WholeGet=检验
    /// </summary>
    public string? PlanTypeKey => _planTypeKey;
    /// <summary>
    /// 当前执行页的方案类型编码，0=设置、1=检验，空值表示类型无效或缺失
    /// </summary>
    public int? PlanTypeCode => AlarmHostPlanTypes.FromNavigation(_planTypeKey);
    /// <summary>
    /// 当前执行页是否同时具备物料号和有效方案类型
    /// </summary>
    public bool HasValidContext => PlanTypeCode.HasValue && !string.IsNullOrWhiteSpace(MaterialNumber);

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        _contextVersion++;
        var previousMaterial = MaterialNumber;
        base.OnNavigatedTo(navigationContext);
        _orderNumber = navigationContext.Parameters.GetValue<string>("OrderNumber")?.Trim() ?? "";
        _resourceCode = navigationContext.Parameters.GetValue<string>("ResourceCode")?.Trim() ?? "";
        _operatorWorkNumber = navigationContext.Parameters.GetValue<string>("OperatorWorkNumber")?.Trim() ?? "";
        OnPropertyChanged(nameof(WorkOrderText));
        _executor.PortStatusChanged -= PortStatusChanged;
        _executor.PortStatusChanged += PortStatusChanged;
        if (previousMaterial != MaterialNumber) _planVisibility.Clear();
        var planType = (navigationContext.Parameters.GetValue<object>("PlanType") as string)?.Trim();
        _planTypeKey = AlarmHostPlanTypes.FromNavigation(planType).HasValue ? planType : null;
        PageTitle = _planTypeKey switch
        {
            "WholeSet" => _plugin.GetMenuDisplayName(FunctionModuleType.WholeSet),
            "WholeGet" => _plugin.GetMenuDisplayName(FunctionModuleType.WholeGet),
            _ => _plugin.DisplayName + "设置/检验（未指定类型）"
        };
        OnPropertyChanged(nameof(PlanTypeKey));
        OnPropertyChanged(nameof(PlanTypeCode));
        OnPropertyChanged(nameof(HasValidContext));
        OnPropertyChanged(nameof(CanQuery));
        ProductCode = "";
        CustomerCode = "";
        ClearPlan();
        QueryMessage = HasValidContext ? "请输入产品编码和客户编码后查询方案。" : "请从首页选择物料及设置或检验入口。";
        QueryCommand.RaiseCanExecuteChanged();
        RefreshPorts();
    }

    /// <summary>
    /// 离页使本次导航发出的查询结果失效，不取消已经开始的公共查询
    /// </summary>
    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _contextVersion++;
        CancelExecutions();
        _executor.PortStatusChanged -= PortStatusChanged;
        _closingConnections = CloseConnectionsAsync();
        base.OnNavigatedFrom(navigationContext);
    }

    /// <summary>
    /// 按完整业务范围精确加载方案，沿用配置页的解码规则后展示启用项目
    /// 本入口不进行分页模糊检索，也不执行设备或 MES 操作
    /// </summary>
    public async Task QueryAsync()
    {
        if (!CanQuery) return;
        var productCode = ProductCode.Trim();
        var customerCode = CustomerCode.Trim();
        if (productCode.Length is 0 or > 100 || customerCode.Length is 0 or > 100)
        {
            QueryMessage = "产品编码和客户编码均须非空且不超过 100 个字符。";
            _messages.GrowlWarning(QueryMessage);
            return;
        }
        var identity = new AlarmHostPlanIdentity(MaterialNumber, PlanTypeCode!.Value, productCode, customerCode);
        var version = _contextVersion;
        ProductCode = productCode;
        CustomerCode = customerCode;
        ClearPlan();
        IsBusy = true;
        QueryMessage = "正在查询方案…";
        try
        {
            await _closingConnections;
            var plan = await _planService.GetWholeConfigAsync(identity.PlanType, identity.ProductCode,
                identity.CustomerCode, ProductCategory.GasDG, ProductKey, identity.MaterialNumber);
            if (version != _contextVersion) return;
            if (plan == null)
            {
                QueryMessage = "未找到当前物料及类型下的对应方案，请核对产品编码和客户编码。";
                _messages.GrowlInfo(QueryMessage);
                return;
            }
            // 复用既有解码入口，不在执行页复制功能、分支和参数合法性规则。
            AlarmHostPlanCodec.ToDraft(plan, identity);
            // 展示顺序统一跟随配置目录；保留原方案的 Order，不将界面顺序作为协议执行顺序。
            var enabledItems = AlarmHostFunctionCatalog.DisplayOrder.Join(plan.Parameters.Where(item => item.IsEnabled),
                definition => definition.Key, item => item.Code, (_, item) => item).ToArray();
            var items = enabledItems.Select(AlarmHostPlanItem.FromParameter).ToArray();
            // 结果行直接采集原始编码和值，不能从已格式化的预览参数反向还原。
            var resultRows = enabledItems.Select((item, index) =>
                new AlarmHostResultRow(index + 1, item, plan.StationCount)).ToArray();
            var stations = Enumerable.Range(1, plan.StationCount).Select(number => new AlarmHostStation(number)).ToArray();
            _loadedPlan = plan;
            _items = items;
            _resultRows = resultRows;
            _stations = stations;
            NotifyPlanChanged();
            IsPlanVisible = !_planVisibility.TryGetValue(identity.PlanType, out var previous) ||
                previous.ProductCode != identity.ProductCode || previous.CustomerCode != identity.CustomerCode || previous.IsVisible;
            _planVisibility[identity.PlanType] = (identity.ProductCode, identity.CustomerCode, IsPlanVisible);
            QueryMessage = HasPendingExecutionItems
                ? "方案已加载，包含执行未接入项目，当前可查看方案和结果行。"
                : items.Any(item => item.Code == AlarmHostFunctionCatalog.BluetoothBroadcast)
                    ? "方案已加载，蓝牙广播暂缓，不参与本次执行与汇总。" : "方案已加载。";
        }
        catch (Exception error)
        {
            if (version != _contextVersion) return;
            Logger.Error(error, "报警主机方案查询失败");
            QueryMessage = "方案查询失败：" + error.Message;
            _messages.GrowlWarning(QueryMessage);
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// 确认当前工位扫码并清空其旧结果，返回下一待扫码工位供视图定位焦点
    /// 已完成工位可以接下一枚扫码，全部运行时不把焦点交给禁用输入框
    /// </summary>
    public AlarmHostStation? ConfirmScan(AlarmHostStation station)
    {
        if (!station.CanScan) return null;
        var index = Array.IndexOf(_stations, station);
        if (index < 0 || !station.ConfirmScan()) return null;
        foreach (var row in ResultRows) row.Cells[index].Clear(row.CanExecute ? "" : "执行未接入");
        return Enumerable.Range(1, _stations.Length).Select(offset => _stations[(index + offset) % _stations.Length])
            .FirstOrDefault(candidate => candidate != station && candidate.CanEdit && candidate.IsConnected);
    }

    /// <summary>
    /// 编码变化、重新查询或导航时清空旧方案和展示信息
    /// </summary>
    private void ClearPlan()
    {
        CancelExecutions();
        _closingConnections = CloseConnectionsAsync();
        _loadedPlan = null;
        _items = [];
        _resultRows = [];
        _stations = [];
        PassedCount = 0;
        FailedCount = 0;
        IsPlanVisible = false;
        QueryMessage = "请输入产品编码和客户编码后查询方案。";
        NotifyPlanChanged();
    }

    /// <summary>
    /// 只调整方案可见性；同一类型、同一方案重新查询时沿用手动选择
    /// </summary>
    private void TogglePlanVisibility()
    {
        IsPlanVisible = !IsPlanVisible;
        _planVisibility[PlanTypeCode!.Value] = (_loadedPlan!.ProductCode, _loadedPlan.CustomerCode, IsPlanVisible);
    }

    /// <summary>
    /// 统一更新方案、工位和结果绑定及显示切换命令
    /// </summary>
    private void NotifyPlanChanged()
    {
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(ResultRows));
        OnPropertyChanged(nameof(ParameterItems));
        OnPropertyChanged(nameof(NoParameterItems));
        OnPropertyChanged(nameof(PlanSummary));
        OnPropertyChanged(nameof(StationCount));
        OnPropertyChanged(nameof(Stations));
        OnPropertyChanged(nameof(PlanMaintenance));
        TogglePlanCommand.RaiseCanExecuteChanged();
        ExecuteStationCommand.RaiseCanExecuteChanged();
        TogglePortsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 枚举真实端口，设备插拔后可重新刷新；不触发数据库或串口打开
    /// </summary>
    private void RefreshPorts()
    {
        if (IsBusy) return;
        try
        {
            _availablePorts = _executor.GetAvailablePorts().Concat(_stations.Where(station => station.IsConnected)
                .Select(station => station.PortName)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            OnPropertyChanged(nameof(AvailablePorts));
        }
        catch (Exception error)
        {
            Logger.Error(error, "报警主机串口枚举失败");
            _messages.GrowlWarning("串口枚举失败：" + error.Message);
        }
    }

    /// <summary>
    /// 显式打开当前选中的端口，所有工位同端口时共用，否则各端口独立
    /// </summary>
    public async Task OpenPortsAsync()
    {
        if (!HasPlan || IsBusy || IsExecuting) return;
        var selected = _stations.Where(station => !string.IsNullOrWhiteSpace(station.PortName)).ToArray();
        if (selected.Length == 0) { _messages.GrowlWarning("请先选择待使用工位的串口。"); return; }
        var groups = selected.GroupBy(station => station.PortName, StringComparer.OrdinalIgnoreCase).ToArray();
        if (groups.Length != 1 && groups.Length != selected.Length)
        { _messages.GrowlWarning("请选择全部共用同一串口，或各工位使用独立串口。"); return; }
        if (groups.Any(group => group.Select(station => station.BaudRate).Distinct().Count() != 1))
        { _messages.GrowlWarning("共用串口的工位必须使用相同波特率。"); return; }
        var version = _contextVersion;
        IsBusy = true;
        try
        {
            await _closingConnections;
            await _executor.ClosePortsAsync();
            var serial = await _globalConfig.GetSerialPortConfigAsync();
            var protocol = await _globalConfig.GetProtocolConfigAsync();
            if (version != _contextVersion) return;
            var ports = groups.ToDictionary(group => group.Key, group => new SerialPortConfig
            {
                BaudRate = group.First().BaudRate,
                DataBits = serial.DataBits,
                StopBits = serial.StopBits,
                Parity = serial.Parity
            }, StringComparer.OrdinalIgnoreCase);
            _commands = protocol.Commands.ToArray();
            await _executor.OpenPortsAsync(ports);
            if (version != _contextVersion) { await _executor.ClosePortsAsync(); return; }
            NotifyConnectionChanged();
            QueryMessage = HasPendingExecutionItems
                ? "串口已打开，方案包含执行未接入项目，当前仅可查看方案和结果行。"
                : "串口已打开，扫码回车开始本工位操作。";
        }
        catch (Exception error)
        {
            Logger.Error(error, "报警主机打开串口失败");
            if (version == _contextVersion) _messages.GrowlWarning("打开串口失败：" + error.Message);
        }
        finally { IsBusy = false; NotifyConnectionChanged(); }
    }

    /// <summary>
    /// 关闭期间先取消设备等待和排队；已开始保存的检测记录由公共服务完成收尾
    /// </summary>
    private async Task TogglePortsAsync()
    {
        if (_executor.IsConnectionOpen)
        {
            await ClosePortsAsync();
        }
        else await OpenPortsAsync();
    }

    /// <summary>
    /// 页面显式关闭所有自有端口并取消设备等待，供同一关闭命令和离线验证复用
    /// </summary>
    public async Task ClosePortsAsync()
    {
        CancelExecutions();
        _closingConnections = CloseConnectionsAsync();
        await _closingConnections;
    }

    /// <summary>
    /// 将连续的清理请求串行衔接，避免清空编码及重新查询时重复关闭竞态
    /// </summary>
    private async Task CloseConnectionsAsync()
    {
        var previous = _closingConnections;
        try { await previous; await _executor.ClosePortsAsync(); }
        catch (Exception error) { Logger.Error(error, "报警主机关闭自有串口失败"); }
        finally { NotifyConnectionChanged(); }
    }

    /// <summary>
    /// 公共串口事件可能来自接收线程，绑定和命令统一在页面上下文通知
    /// </summary>
    private void PortStatusChanged(object? sender, SerialPortStatusChangedEventArgs args)
    {
        if (_uiContext != null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => NotifyConnectionChanged(), null);
        else NotifyConnectionChanged();
    }

    /// <summary>
    /// 连接状态与扫码状态分开，未选择或已断开的工位不接受扫码
    /// </summary>
    private void NotifyConnectionChanged()
    {
        foreach (var station in _stations) station.IsConnected = _executor.IsPortOpen(station.PortName);
        OnPropertyChanged(nameof(TogglePortsText));
        ExecuteStationCommand.RaiseCanExecuteChanged();
        TogglePortsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 本地结束工单经确认后清空页面并返回首页，不调用MES结单
    /// </summary>
    public async Task EndWorkOrderAsync()
    {
        if (IsBusy || IsExecuting) return;
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _messages.GrowlConfirm("结束当前工单并返回首页？", answer => confirmation.TrySetResult(answer));
        if (!await confirmation.Task) return;
        IsBusy = true;
        try
        {
            _closingConnections = CloseConnectionsAsync();
            await _closingConnections;
            ClearPlan();
            ProductCode = "";
            CustomerCode = "";
            _events.GetEvent<EndWorkOrderEvent>().Publish();
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// 执行本工位已勾选项目，扫码编号作为表号目标，不作为设备地址
    /// 运行状态按工位管理；清空方案或离页后的结果不再回填
    /// </summary>
    public async Task ExecuteStationAsync(AlarmHostStation? station)
    {
        if (IsBusy || station == null || station.IsRunning) return;
        var index = Array.IndexOf(_stations, station);
        if (index < 0 || _loadedPlan == null || ResultRows.Count == 0) return;
        var rows = ResultRows;
        var executableRows = rows.Where(row => row.CanExecute).ToArray();
        if (HasPendingExecutionItems)
        {
            _messages.GrowlWarning("方案包含执行未接入项目，当前仅展示，不执行设备检测。");
            return;
        }
        if (executableRows.Length == 0)
        {
            foreach (var row in rows) row.Cells[index].Clear("暂未接入");
            _messages.GrowlWarning("当前方案没有可执行项目，蓝牙广播本轮暂缓。");
            return;
        }
        if (!station.IsScanned) { _messages.GrowlWarning("请先扫码并按回车确认编号。"); return; }
        if (!_executor.IsPortOpen(station.PortName)) { _messages.GrowlWarning("请先打开当前工位的串口。"); return; }
        var plan = _loadedPlan;
        var version = _contextVersion;
        var scanCode = station.ScanCode;
        var started = DateTime.Now;
        var order = _orderNumber;
        var resource = _resourceCode;
        var workNumber = _operatorWorkNumber;
        using var cancellation = new CancellationTokenSource();
        _executions.Add(station, cancellation);
        station.IsRunning = true;
        station.IsPass = null;
        station.StartTiming();
        foreach (var row in rows) row.Cells[index].Clear(AlarmHostFunctionCatalog.Get(row.Code).ObjectId.HasValue ? "待执行" : "暂未接入");
        NotifyExecutionChanged();
        bool IsCurrent() => !cancellation.IsCancellationRequested && version == _contextVersion &&
            ReferenceEquals(plan, _loadedPlan) && station.ScanCode == scanCode && station.IsScanned;
        try
        {
            station.SetExecutionStatus(PlanTypeCode == 0 ? "正在设置" : "正在检验");
            await _executor.ExecuteAsync(station.PortName, station.BaudRate, plan.PlanType, scanCode, executableRows, result =>
            {
                if (IsCurrent()) rows.Single(row => row.Code == result.Code).Cells[index].Apply(result);
            }, cancellation.Token, _commands);
            if (!IsCurrent()) return;
            foreach (var row in executableRows.Where(row => row.Cells[index].IsPass == null))
                row.Cells[index].Apply(new(row.Code, "未执行", "", false, "前序步骤未通过"));
            var devicePassed = executableRows.All(row => row.Cells[index].IsPass == true);
            station.StopTiming();
            if (devicePassed) PassedCount++;
            else FailedCount++;
            var record = new TestRecordDto
            {
                Category = ProductCategory.GasDG, TestType = plan.PlanType,
                MaterialNumber = plan.MaterialNumber, ProductCode = plan.ProductCode,
                CustomerCode = plan.CustomerCode, OrderNumber = order, ProductSn = scanCode,
                CreatedBy = workNumber, CreatedTime = started, TestResult = devicePassed,
                TestContentJson = JsonSerializer.Serialize(new
                {
                    ExecutionId = Guid.NewGuid(), ProductKey,
                    ResourceCode = resource, StationNumber = station.Number,
                    StartedTime = started, CompletedTime = DateTime.Now,
                    DevicePassed = devicePassed, MesStatus = "未接入",
                    Items = rows.Select(row => new
                    {
                        row.Code, row.Name, row.MesCode, row.Parameters,
                        row.Cells[index].Value, row.Cells[index].MesValue,
                        row.Cells[index].IsPass, row.Cells[index].Message
                    })
                })
            };
            await SaveRecordAsync(station, record);
        }
        catch (OperationCanceledException)
        {
            foreach (var row in executableRows.Where(row => row.Cells[index].IsPass == null)) row.Cells[index].Clear();
            station.SetExecutionStatus("已取消");
        }
        catch (IOException error)
        {
            if (!IsCurrent()) return;
            foreach (var row in executableRows.Where(row => row.Cells[index].IsPass == null)) row.Cells[index].Interrupt(error.Message);
            station.IsPass = null;
            station.SetExecutionStatus("通信中止");
            _messages.GrowlWarning($"工位{station.Number}通信中止：{error.Message}");
        }
        catch (Exception error)
        {
            Logger.Error(error, "报警主机工位{0}执行失败", station.Number);
            if (!IsCurrent()) return;
            foreach (var row in executableRows.Where(row => row.Cells[index].IsPass == null))
                row.Cells[index].Apply(new(row.Code, "未完成", "", false, error.Message));
            station.IsPass = false;
            station.SetExecutionStatus("执行失败");
            _messages.GrowlWarning($"工位{station.Number}执行失败：{error.Message}");
        }
        finally
        {
            _executions.Remove(station);
            station.StopTiming();
            station.IsRunning = false;
            NotifyExecutionChanged();
        }
    }

    /// <summary>
    /// 完成检测后复用公共接口保存一次，失败弹窗并记录日志，处理后允许继续扫码
    /// 不保留补存状态，检测耗时在进入保存前已经结束
    /// </summary>
    private async Task SaveRecordAsync(AlarmHostStation station, TestRecordDto record)
    {
        station.SetExecutionStatus("正在保存记录");
        try
        {
            await _testRecordService.SaveWholeRecordAsync(record);
            station.IsPass = record.TestResult;
            station.SetExecutionStatus(record.TestResult ? "设备项目通过，记录已保存" : "设备项目未通过，记录已保存");
        }
        catch (Exception error)
        {
            Logger.Error(error, "报警主机工位{0}检测记录保存失败", station.Number);
            station.IsPass = null;
            station.SetExecutionStatus("记录保存失败");
            _messages.ShowError($"工位{station.Number}检测已完成，记录保存失败：{error.Message}\n请联系管理员处理。", "检测记录保存失败");
        }
    }

    /// <summary>
    /// 由可见页面的单一UI时钟刷新运行工位，不为每个工位创建后台任务
    /// </summary>
    public void RefreshElapsedTimes()
    {
        foreach (var station in _executions.Keys) station.RefreshElapsed();
    }

    /// <summary>
    /// 离页和方案变化统一取消当前工位，各会话自行退订并关闭拥有的串口
    /// </summary>
    private void CancelExecutions()
    {
        foreach (var cancellation in _executions.Values.ToArray()) cancellation.Cancel();
    }

    /// <summary>
    /// 工位开始与结束后更新整页查询命令及各工位执行命令
    /// </summary>
    private void NotifyExecutionChanged()
    {
        OnPropertyChanged(nameof(IsExecuting));
        OnPropertyChanged(nameof(CanQuery));
        QueryCommand.RaiseCanExecuteChanged();
        ExecuteStationCommand.RaiseCanExecuteChanged();
        RefreshPortsCommand.RaiseCanExecuteChanged();
        EndWorkOrderCommand.RaiseCanExecuteChanged();
        NotifyConnectionChanged();
    }
}
