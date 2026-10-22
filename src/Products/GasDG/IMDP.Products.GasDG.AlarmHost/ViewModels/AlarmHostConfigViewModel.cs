using System.ComponentModel;
using IMDP.Application.DTOs;
using IMDP.Application.Services;
using IMDP.Products.GasDG.AlarmHost.Models;
using IMDP.Products.GasDG.AlarmHost.Services;
using IMDP.Domain.Products;
using Prism.Commands;
using Prism.Events;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>
/// 报警设置和检验方案的编辑状态及页面命令
/// </summary>
public sealed class AlarmHostConfigViewModel : AlarmHostPageViewModel
{
    /// <summary>
    /// 当前报警主机插件，用于取得方案配置入口名称
    /// </summary>
    private readonly GasDGAlarmHostPlugin _plugin;
    /// <summary>
    /// 平台方案服务，负责完整范围存取及审计
    /// </summary>
    private readonly ITestPlanService _plans;
    /// <summary>
    /// 复用平台 Growl 信息、成功、警告和确认提示
    /// </summary>
    private readonly IMessageService _messages;
    /// <summary>
    /// 导航代数，用于忽略旧页面请求返回的结果
    /// </summary>
    private long _contextVersion;
    /// <summary>
    /// 当前页面是否正在查询、维护或删除，操作互斥且不排队
    /// </summary>
    private bool _isBusy;
    // 只缓存当前物料的两类页面草稿，不是方案库；切换物料或缺失导航上下文时全部清空。
    private readonly Dictionary<string, EditingState> _editingStates = new(StringComparer.Ordinal);
    /// <summary>
    /// 当前物料、当前方案类型对应的编辑状态，导航无效时为空
    /// </summary>
    private EditingState? _editingState;
    /// <summary>
    /// 当前方案类型导航键，WholeSet=设置、WholeGet=检验
    /// </summary>
    private string? _planTypeKey;
    /// <summary>
    /// 当前页面绑定并监听属性变化的编辑草稿
    /// </summary>
    private AlarmHostConfigDraft _draft = new();
    /// <summary>
    /// 当前选中并回填的列表记录
    /// </summary>
    private AlarmHostConfigListItem? _selectedPlan;

    /// <summary>
    /// 参数配置区域当前是否展开
    /// </summary>
    private bool _isParametersExpanded = true;
    /// <summary>
    /// 页面是否正在等待用户确认重置草稿
    /// </summary>
    private bool _resetPending;
    /// <summary>
    /// 当前输入检查是否失败
    /// </summary>
    private bool _hasInputErrors;
    /// <summary>
    /// 当前输入检查反馈文本
    /// </summary>
    private string _inputCheckMessage = string.Empty;

    public AlarmHostConfigViewModel(GasDGAlarmHostPlugin plugin, IEventAggregator eventAggregator,
        ITestPlanService plans, IMessageService messages)
        : base(plugin, eventAggregator, FunctionModuleType.WholeSetConfig,
            "设置与检验方案按类型分别维护；页面和控件复用，方案参数不互相覆盖。")
    {
        _plugin = plugin;
        _plans = plans;
        _messages = messages;
        PageTitle = plugin.DisplayName + "方案配置（未指定类型）";
        _draft.PropertyChanged += DraftChanged;
        RequestResetCommand = new DelegateCommand(() => ResetPending = true, () => CanEdit);
        CancelResetCommand = new DelegateCommand(() => ResetPending = false);
        ConfirmResetCommand = new DelegateCommand(ResetDraft, () => CanEdit);
        ToggleParametersCommand = new DelegateCommand(() => IsParametersExpanded = !IsParametersExpanded);
        QueryCommand = new DelegateCommand(async () => await QueryAsync(), () => CanEdit);
        MaintainCommand = new DelegateCommand(async () => await MaintainAsync(), () => CanEdit);
        DeleteCommand = new DelegateCommand(async () => await DeleteAsync(), () => CanEdit && SelectedPlan?.Plan?.Id is > 0);
        PageCommand = new DelegateCommand<string>(async action => await ChangePageAsync(action), CanChangePage);
    }

    /// <summary>
    /// 当前物料和方案类型的编辑草稿，切换实例时重新绑定变化通知
    /// </summary>
    public AlarmHostConfigDraft Draft
    {
        get => _draft;
        private set
        {
            _draft.PropertyChanged -= DraftChanged;
            SetProperty(ref _draft, value);
            _draft.PropertyChanged += DraftChanged;
        }
    }
    /// <summary>
    /// 打开当前类型草稿的重置确认提示
    /// </summary>
    public DelegateCommand RequestResetCommand { get; }
    /// <summary>
    /// 确认后重置当前类型的编辑草稿
    /// </summary>
    public DelegateCommand ConfirmResetCommand { get; }
    /// <summary>
    /// 取消当前草稿的重置确认
    /// </summary>
    public DelegateCommand CancelResetCommand { get; }
    /// <summary>
    /// 查询当前物料、插件和类型下的方案，产品与客户编码为可选筛选条件
    /// </summary>
    public DelegateCommand QueryCommand { get; }
    /// <summary>
    /// 保存当前编码对应方案的勾选项及参数
    /// </summary>
    public DelegateCommand MaintainCommand { get; }
    /// <summary>
    /// 确认后软删除选中的实际方案
    /// </summary>
    public DelegateCommand DeleteCommand { get; }
    /// <summary>
    /// 方案列表翻页命令，支持首页、上一页、下一页和末页
    /// </summary>
    public DelegateCommand<string> PageCommand { get; }
    /// <summary>
    /// 当前配置页的方案类型导航键，WholeSet=设置、WholeGet=检验
    /// </summary>
    public string? PlanTypeKey => _planTypeKey;
    /// <summary>
    /// 当前配置页的方案类型编码，0=设置、1=检验，空值表示类型无效或缺失
    /// </summary>
    public int? PlanTypeCode => AlarmHostPlanTypes.FromNavigation(_planTypeKey);
    /// <summary>
    /// 当前配置页是否同时具备物料号和有效方案类型
    /// </summary>
    public bool HasValidContext => PlanTypeCode.HasValue && !string.IsNullOrWhiteSpace(MaterialNumber);
    /// <summary>
    /// 上下文有效且未在查询、确认或保存时，允许编辑查询条件及方案参数
    /// </summary>
    public bool CanEdit => HasValidContext && !IsBusy;
    /// <summary>
    /// 当前是否为检验方案，用于显示检验专属项目和列表列
    /// </summary>
    public bool IsInspectionPlan => _planTypeKey == "WholeGet";
    /// <summary>
    /// 切换参数配置区域的展开或收起状态
    /// </summary>
    public DelegateCommand ToggleParametersCommand { get; }
    /// <summary>
    /// 参数配置区域是否展开，用于控制页面分区高度
    /// </summary>
    public bool IsParametersExpanded
    {
        get => _isParametersExpanded;
        private set
        {
            if (SetProperty(ref _isParametersExpanded, value)) OnPropertyChanged(nameof(ToggleParametersToolTip));
        }
    }
    /// <summary>
    /// 参数区展开或收起按钮的提示文本
    /// </summary>
    public string ToggleParametersToolTip => IsParametersExpanded ? "收起参数配置区" : "展开参数配置区";
    /// <summary>
    /// 是否显示当前类型草稿的重置确认提示
    /// </summary>
    public bool ResetPending
    {
        get => _resetPending;
        private set => SetProperty(ref _resetPending, value);
    }

    /// <summary>
    /// 最近一次输入检查是否失败，草稿变化后清除该标记
    /// </summary>
    public bool HasInputErrors
    {
        get => _hasInputErrors;
        private set => SetProperty(ref _hasInputErrors, value);
    }

    /// <summary>
    /// 当前草稿的输入检查或未保存提示文本
    /// </summary>
    public string InputCheckMessage
    {
        get => _inputCheckMessage;
        private set => SetProperty(ref _inputCheckMessage, value);
    }

    /// <summary>
    /// 物料号或方案类型缺失时的页面提示文本
    /// </summary>
    public string PersistenceNotice => HasValidContext
        ? string.Empty
        : "缺少物料号或有效方案类型，请从首页重新进入。";
    /// <summary>
    /// 当前存储接入状态及草稿保留范围的说明文本
    /// </summary>
    public string PersistenceToolTip => "按当前物料、插件及方案类型分页查询，产品和客户编码可留空或填写关键字；选择记录回填，维护时两个编码必填，删除须选择方案并确认。";

    /// <summary>
    /// 当前是否正在等待查询、维护或删除结果，包含确认等待
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            NotifyPlanCommands();
        }
    }

    /// <summary>
    /// 生产状态下拉选项，0=工厂态（生产态）、1=出厂态（用户态）
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte, string>> ProductionStatusOptions { get; } =
        Array.AsReadOnly(new[] { new KeyValuePair<byte, string>(0, "工厂态（生产态）"), new(1, "出厂态（用户态）") });
    /// <summary>
    /// 服务器地址类型下拉选项，0=IP、1=域名
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte, string>> AddressTypeOptions { get; } =
        Array.AsReadOnly(new[] { new KeyValuePair<byte, string>(0, "IP"), new(1, "域名") });
    /// <summary>
    /// 主电上报类型下拉选项，编码及名称与当前草稿输入分支对应
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte, string>> ReportTypeOptions { get; } = Array.AsReadOnly(new[]
    {
        new KeyValuePair<byte, string>(0, "每天"), new(1, "单月"), new(2, "双月"), new(3, "每月"),
        new(4, "按分"), new(5, "按时"), new(7, "指定五时"), new(8, "指定五日"), new(9, "按天")
    });
    /// <summary>
    /// 方案列表每页条数的可选值：10、20、50
    /// </summary>
    public IReadOnlyList<int> PageSizeOptions { get; } = Array.AsReadOnly(new[] { 10, 20, 50 });
    /// <summary>
    /// 当前类型草稿缓存的每页条数，默认值为 10
    /// </summary>
    public int PageSize
    {
        get => _editingState?.PageSize ?? 10;
        set
        {
            if (!CanEdit || _editingState == null || !PageSizeOptions.Contains(value) || value == _editingState.PageSize) return;
            if (_editingState.HasQuery)
            {
                _ = QueryPageAsync(1, _editingState.ProductCodeKeyword, _editingState.CustomerCodeKeyword, value);
                return;
            }
            _editingState.PageSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalPages));
            PageCommand.RaiseCanExecuteChanged();
        }
    }
    /// <summary>
    /// 当前查询条件匹配的总记录数
    /// </summary>
    public int TotalCount => _editingState?.TotalCount ?? 0;
    /// <summary>
    /// 当前查询条件及每页条数对应的总页数
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    /// <summary>
    /// 当前页码，从1开始
    /// </summary>
    public int PageIndex => _editingState?.PageIndex ?? 1;
    /// <summary>
    /// 当前类型最近一次查询列表或保存的实际方案
    /// </summary>
    public IReadOnlyList<AlarmHostConfigListItem> CurrentPageData => _editingState?.Results ?? [];

    /// <summary>
    /// 选中实际方案后回填编码、工位数量、勾选项、MES码及参数
    /// 回填失败仍接受选中项并保留原草稿，删除按实际选中记录定位
    /// </summary>
    public AlarmHostConfigListItem? SelectedPlan
    {
        get => _selectedPlan;
        set
        {
            if (ReferenceEquals(_selectedPlan, value)) return;
            if (value?.Plan is { } plan && _editingState is { } state)
            {
                try
                {
                    var draft = AlarmHostPlanCodec.ToDraft(plan,
                        new AlarmHostPlanIdentity(MaterialNumber, PlanTypeCode!.Value, plan.ProductCode, plan.CustomerCode));
                    Draft = draft;
                    state.Draft = draft;
                    ClearInputFeedback();
                    InputCheckMessage = "方案已选中并回填，修改后可维护。";
                }
                catch (Exception error)
                {
                    HasInputErrors = true;
                    InputCheckMessage = $"方案回填失败：{error.Message}";
                    _messages.GrowlWarning(InputCheckMessage);
                }
            }
            if (SetProperty(ref _selectedPlan, value)) DeleteCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 根据物料和方案类型切换独立编辑缓存，导航无效或切换物料时清空旧缓存
    /// </summary>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        _contextVersion++;
        SelectedPlan = null;
        var previousMaterial = MaterialNumber;
        base.OnNavigatedTo(navigationContext);
        var planType = (navigationContext.Parameters.GetValue<object>("PlanType") as string)?.Trim();
        _planTypeKey = AlarmHostPlanTypes.FromNavigation(planType).HasValue ? planType : null;
        if (!HasValidContext || !string.Equals(previousMaterial, MaterialNumber, StringComparison.Ordinal))
            _editingStates.Clear();

        _editingState = null;
        if (HasValidContext)
        {
            if (!_editingStates.TryGetValue(_planTypeKey!, out _editingState))
                _editingStates[_planTypeKey!] = _editingState = new EditingState();
        }
        Draft = _editingState?.Draft ?? new AlarmHostConfigDraft();
        PageTitle = _planTypeKey switch
        {
            "WholeSet" => _plugin.GetMenuDisplayName(FunctionModuleType.WholeSetConfig),
            "WholeGet" => _plugin.GetMenuDisplayName(FunctionModuleType.WholeGetConfig),
            _ => _plugin.DisplayName + "方案配置（未指定类型）"
        };
        ClearInputFeedback();
        foreach (var name in new[] { nameof(PlanTypeKey), nameof(PlanTypeCode), nameof(HasValidContext), nameof(CanEdit),
                     nameof(IsInspectionPlan),
                     nameof(PersistenceNotice), nameof(PageSize) })
            OnPropertyChanged(name);
        NotifyPlanCommands();
        NotifyResults();
    }

    /// <summary>
    /// 离页后使旧请求的页面回填失效，不取消已经开始的保存或删除
    /// </summary>
    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _contextVersion++;
        base.OnNavigatedFrom(navigationContext);
    }

    /// <summary>
    /// 按输入条件查询第一页，空编码表示不按该字段筛选
    /// </summary>
    public async Task QueryAsync()
    {
        if (!CanEdit) return;
        await QueryPageAsync(1, Draft.ProductCode?.Trim() ?? string.Empty, Draft.CustomerCode?.Trim() ?? string.Empty, PageSize);
    }

    /// <summary>
    /// 保留本次列表查询条件，切换到指定页
    /// </summary>
    public async Task ChangePageAsync(string action)
    {
        if (!CanChangePage(action)) return;
        var page = action switch
        {
            "First" => 1,
            "Previous" => PageIndex - 1,
            "Next" => PageIndex + 1,
            "Last" => TotalPages,
            _ => PageIndex
        };
        await QueryPageAsync(page, _editingState!.ProductCodeKeyword, _editingState.CustomerCodeKeyword, PageSize);
    }

    /// <summary>
    /// 根据忙状态和当前页边界控制翻页命令是否可用
    /// </summary>
    private bool CanChangePage(string action) => CanEdit && (action switch
    {
        "First" or "Previous" => PageIndex > 1,
        "Next" or "Last" => PageIndex < TotalPages,
        _ => false
    });

    /// <summary>
    /// 统一处理主动查询、翻页和条数切换的忙状态与提示，查询失败保留原结果，回填失败保留新列表
    /// </summary>
    private async Task QueryPageAsync(int pageIndex, string productCodeKeyword, string customerCodeKeyword, int pageSize)
    {
        var version = _contextVersion;
        IsBusy = true;
        try
        {
            if (await LoadQueryPageAsync(pageIndex, productCodeKeyword, customerCodeKeyword, pageSize) && !HasInputErrors)
                _messages.GrowlInfo(InputCheckMessage);
        }
        catch (Exception error)
        {
            if (_contextVersion != version) return;
            OnPropertyChanged(nameof(PageSize));
            HasInputErrors = true;
            InputCheckMessage = $"方案查询失败：{error.Message}";
            _messages.GrowlWarning(InputCheckMessage);
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// 由查询、维护和删除共用列表刷新，忙状态由调用操作统一管理
    /// 通过范围及页面上下文核对后更新列表、分页和查询条件，再复用选中逻辑回填唯一结果
    /// 回填失败只提示原因，不撤销已查询的列表和选中项
    /// </summary>
    private async Task<bool> LoadQueryPageAsync(int pageIndex, string productCodeKeyword, string customerCodeKeyword, int pageSize)
    {
        var state = _editingState!;
        var version = _contextVersion;
        var materialNumber = MaterialNumber;
        var planType = PlanTypeCode!.Value;
        var productCode = Draft.ProductCode?.Trim() ?? string.Empty;
        var customerCode = Draft.CustomerCode?.Trim() ?? string.Empty;
        if (new[] { materialNumber, productCodeKeyword, customerCodeKeyword }.Any(code => code.Length > 100))
            throw new ArgumentException("物料、产品或客户编码不能超过100个字符。");
        var result = await _plans.GetWholeConfigsAsync(planType, productCodeKeyword, customerCodeKeyword, pageIndex, pageSize,
            _plugin.Category, _plugin.ProductKey, materialNumber);
        if (!IsCurrent(materialNumber, planType, productCode, customerCode, state, version)) return false;
        var lastPage = Math.Max(1, (result.TotalCount + pageSize - 1) / pageSize);
        if (pageIndex > lastPage)
        {
            pageIndex = lastPage;
            result = await _plans.GetWholeConfigsAsync(planType, productCodeKeyword, customerCodeKeyword, pageIndex, pageSize,
                _plugin.Category, _plugin.ProductKey, materialNumber);
            if (!IsCurrent(materialNumber, planType, productCode, customerCode, state, version)) return false;
        }
        if (result.Items.Any(plan => !SameScope(plan, planType, _plugin.Category, _plugin.ProductKey, materialNumber)))
            throw new InvalidOperationException("返回的方案范围不匹配，请重新查询。");
        var rows = result.Items.Select(AlarmHostConfigListItem.FromPlan).ToArray();
        SelectedPlan = null;
        state.Results = rows;
        state.TotalCount = result.TotalCount;
        state.PageIndex = pageIndex;
        state.PageSize = pageSize;
        OnPropertyChanged(nameof(PageSize));
        state.ProductCodeKeyword = productCodeKeyword;
        state.CustomerCodeKeyword = customerCodeKeyword;
        state.HasQuery = true;
        HasInputErrors = false;
        InputCheckMessage = result.TotalCount switch
        {
            0 => "未找到方案，可维护新增；当前编辑值尚未保存。",
            1 => "方案已查询并回填。",
            _ => $"已查询到 {result.TotalCount} 条方案，第 {pageIndex}/{TotalPages} 页，请选择记录回填。"
        };
        NotifyResults();
        if (result.TotalCount == 1 && rows.Length == 1)
        {
            SelectedPlan = rows[0];
            if (!HasInputErrors) InputCheckMessage = "方案已查询并回填。";
        }
        return true;
    }

    /// <summary>
    /// 确认前采集独立方案，等待公共保存完成后按本次保存的编码重新查询第一页
    /// 保留每页条数，查询失败时区分已保存和未保存，避免误报保存失败
    /// </summary>
    public async Task MaintainAsync()
    {
        if (!CanEdit) return;
        var state = _editingState!;
        var version = _contextVersion;
        var materialNumber = MaterialNumber;
        var planType = PlanTypeCode!.Value;
        var saved = false;
        IsBusy = true;
        try
        {
            var document = AlarmHostPlanFactory.FromDraft(materialNumber, planType, Draft);
            var productCode = document.Identity.ProductCode;
            var customerCode = document.Identity.CustomerCode;
            if (new[] { materialNumber, productCode, customerCode }.Any(code => code.Length > 100))
                throw new ArgumentException("方案定位编码不能超过100个字符。");
            if (!await ConfirmAsync($"确认维护物料 {materialNumber} 的{(planType == 0 ? "设置" : "检验")}方案 {productCode}/{customerCode}？")) return;
            if (!IsCurrent(materialNumber, planType, productCode, customerCode, state, version)) return;
            await _plans.SaveWholeConfigAsync(planType, productCode, customerCode, _plugin.Category,
                _plugin.ProductKey, materialNumber, document.ToPlanDto());
            if (!IsCurrent(materialNumber, planType, productCode, customerCode, state, version)) return;
            saved = true;
            if (!await LoadQueryPageAsync(1, productCode, customerCode, state.PageSize) || HasInputErrors) return;
            HasInputErrors = false;
            InputCheckMessage = "方案维护完成。";
            _messages.GrowlSuccess(InputCheckMessage);
        }
        catch (Exception error)
        {
            if (_contextVersion != version) return;
            HasInputErrors = true;
            InputCheckMessage = saved ? $"方案已维护，列表刷新失败：{error.Message}" : $"方案操作失败：{error.Message}";
            _messages.GrowlWarning(InputCheckMessage);
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// 确认后按选中方案的主键和完整范围软删除，重置草稿并清空编码筛选后查询第一页
    /// 刷新前清空旧列表及计数并同步页码，避免查询失败时将旧页内容显示为第一页
    /// </summary>
    public async Task DeleteAsync()
    {
        if (!CanEdit || SelectedPlan?.Plan is not { Id: > 0 } plan) return;
        var state = _editingState!;
        var version = _contextVersion;
        var materialNumber = MaterialNumber;
        var planType = PlanTypeCode!.Value;
        var processed = false;
        IsBusy = true;
        try
        {
            if (!await ConfirmAsync($"确认删除物料 {materialNumber} 的{(planType == 0 ? "设置" : "检验")}方案 {plan.ProductCode}/{plan.CustomerCode}？")) return;
            if (!IsCurrentContext(materialNumber, planType, state, version)) return;
            var deleted = await _plans.DeleteWholeConfigAsync(plan.Id, planType, plan.ProductCode, plan.CustomerCode,
                _plugin.Category, _plugin.ProductKey, materialNumber);
            if (!IsCurrentContext(materialNumber, planType, state, version)) return;
            processed = true;
            state.Results = [];
            state.TotalCount = 0;
            ResetDraft();
            state.ProductCodeKeyword = string.Empty;
            state.CustomerCodeKeyword = string.Empty;
            state.PageIndex = 1;
            NotifyResults();
            if (!await LoadQueryPageAsync(1, state.ProductCodeKeyword, state.CustomerCodeKeyword, state.PageSize) || HasInputErrors) return;
            InputCheckMessage = deleted ? "方案删除完成。" : "方案不存在或已删除，列表已刷新。";
            if (deleted) _messages.GrowlSuccess(InputCheckMessage);
            else _messages.GrowlInfo(InputCheckMessage);
        }
        catch (Exception error)
        {
            if (_contextVersion != version) return;
            HasInputErrors = true;
            InputCheckMessage = processed ? $"删除已处理，列表刷新失败：{error.Message}" : $"方案删除失败：{error.Message}";
            _messages.GrowlWarning(InputCheckMessage);
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// 等待公共Growl确认，操作期间保持互斥，重复回调只完成一次
    /// </summary>
    private Task<bool> ConfirmAsync(string message)
    {
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _messages.GrowlConfirm(message, confirmed => confirmation.TrySetResult(confirmed));
        return confirmation.Task;
    }

    /// <summary>
    /// 核对请求所属页面和编辑缓存，防止离页或切换物料、类型后安装旧结果
    /// </summary>
    private bool IsCurrentContext(string materialNumber, int planType, EditingState state, long version) =>
        version == _contextVersion && ReferenceEquals(state, _editingState) &&
        MaterialNumber == materialNumber && PlanTypeCode == planType;

    /// <summary>
    /// 在页面上下文有效的基础上核对编辑编码，确保确认与回填仍针对原目标
    /// </summary>
    private bool IsCurrent(string materialNumber, int planType, string productCode, string customerCode,
        EditingState state, long version) => IsCurrentContext(materialNumber, planType, state, version) &&
        Draft.ProductCode?.Trim() == productCode && Draft.CustomerCode?.Trim() == customerCode;

    /// <summary>
    /// 核对返回方案的分类、插件、物料和类型，产品及客户编码按具体操作另行核对
    /// </summary>
    private static bool SameScope(TestPlanDto plan, int planType, string category, string productKey, string materialNumber) =>
        plan.Category == category && plan.ProductKey == productKey && plan.MaterialNumber == materialNumber && plan.PlanType == planType;

    /// <summary>
    /// 编辑条件、忙状态或选中项变化后，刷新方案操作和分页命令的可用状态
    /// </summary>
    private void NotifyPlanCommands()
    {
        QueryCommand.RaiseCanExecuteChanged();
        MaintainCommand.RaiseCanExecuteChanged();
        RequestResetCommand.RaiseCanExecuteChanged();
        ConfirmResetCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        PageCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 通知列表及分页显示更新，并重新计算翻页命令的可用状态
    /// </summary>
    private void NotifyResults()
    {
        foreach (var name in new[] { nameof(CurrentPageData), nameof(TotalCount), nameof(TotalPages), nameof(PageIndex) })
            OnPropertyChanged(name);
        PageCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 草稿发生变化时清除旧错误标记，提示当前修改尚未保存
    /// </summary>
    private void DraftChanged(object? sender, PropertyChangedEventArgs e)
    {
        HasInputErrors = false;
        InputCheckMessage = "修改尚未保存。";
    }

    /// <summary>
    /// 将当前类型的草稿恢复默认值并更新缓存，同时取消列表选中，不删除已保存方案或清空查询列表
    /// </summary>
    private void ResetDraft()
    {
        SelectedPlan = null;
        Draft = new AlarmHostConfigDraft();
        if (_editingState != null) _editingState.Draft = Draft;
        ClearInputFeedback();
    }

    /// <summary>
    /// 清除重置确认、错误标记及操作提示，供导航、回填和重置共用
    /// </summary>
    private void ClearInputFeedback()
    {
        ResetPending = false;
        HasInputErrors = false;
        InputCheckMessage = string.Empty;
    }

    private sealed class EditingState
    {
        /// <summary>
        /// 当前物料某一种方案类型的独立编辑草稿
        /// </summary>
        public AlarmHostConfigDraft Draft { get; set; } = new();
        /// <summary>
        /// 当前页方案的列表投影
        /// </summary>
        public IReadOnlyList<AlarmHostConfigListItem> Results { get; set; } = [];
        /// <summary>
        /// 该方案类型在当前物料会话中保留的每页条数
        /// </summary>
        public int PageSize { get; set; } = 10;
        /// <summary>
        /// 当前查询页码，从1开始
        /// </summary>
        public int PageIndex { get; set; } = 1;
        /// <summary>
        /// 当前查询条件匹配的总记录数
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// 是否已完成列表查询，用于维护后刷新及每页条数切换
        /// </summary>
        public bool HasQuery { get; set; }
        /// <summary>
        /// 本次列表查询的产品关键字，翻页时不改用选中行的编码
        /// </summary>
        public string ProductCodeKeyword { get; set; } = string.Empty;
        /// <summary>
        /// 本次列表查询的客户关键字，翻页时不改用选中行的编码
        /// </summary>
        public string CustomerCodeKeyword { get; set; } = string.Empty;
    }
}
