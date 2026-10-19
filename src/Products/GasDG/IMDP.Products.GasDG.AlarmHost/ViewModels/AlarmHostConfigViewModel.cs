using System.ComponentModel;
using IMDP.Products.GasDG.AlarmHost.Models;
using IMDP.Domain.Products;
using Prism.Commands;
using Prism.Events;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

public sealed class AlarmHostConfigViewModel : AlarmHostPageViewModel
{
    private readonly GasDGAlarmHostPlugin _plugin;
    // 只缓存当前物料的两类页面草稿，不是方案库；切换物料或缺失导航上下文时全部清空。
    private readonly Dictionary<string, EditingState> _editingStates = new(StringComparer.Ordinal);
    private EditingState? _editingState;
    private string? _planTypeKey;
    private AlarmHostConfigDraft _draft = new();
    private string _inputCheckMessage = string.Empty;
    private bool _hasInputErrors;
    private bool _resetPending;
    private bool _isParametersExpanded = true;

    public AlarmHostConfigViewModel(GasDGAlarmHostPlugin plugin, IEventAggregator eventAggregator)
        : base(plugin, eventAggregator, FunctionModuleType.WholeSetConfig,
            "设置与检验方案按类型分别维护；页面和控件复用，方案参数不互相覆盖。")
    {
        _plugin = plugin;
        PageTitle = plugin.DisplayName + "方案配置（未指定类型）";
        _draft.PropertyChanged += DraftChanged;
        CheckInputCommand = new DelegateCommand(CheckInput, () => HasValidContext);
        RequestResetCommand = new DelegateCommand(() => ResetPending = true, () => HasValidContext);
        CancelResetCommand = new DelegateCommand(() => ResetPending = false);
        ConfirmResetCommand = new DelegateCommand(ResetDraft, () => HasValidContext);
        ToggleParametersCommand = new DelegateCommand(() => IsParametersExpanded = !IsParametersExpanded);
        // 数据库服务尚未接入，不伪造查询/保存/删除或分页成功。
        QueryCommand = new DelegateCommand(() => { }, () => false);
        MaintainCommand = new DelegateCommand(() => { }, () => false);
        DeleteCommand = new DelegateCommand(() => { }, () => false);
        PageCommand = new DelegateCommand(() => { }, () => false);
    }

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
    public DelegateCommand CheckInputCommand { get; }
    public DelegateCommand RequestResetCommand { get; }
    public DelegateCommand ConfirmResetCommand { get; }
    public DelegateCommand CancelResetCommand { get; }
    public DelegateCommand QueryCommand { get; }
    public DelegateCommand MaintainCommand { get; }
    public DelegateCommand DeleteCommand { get; }
    public DelegateCommand PageCommand { get; }
    public string? PlanTypeKey => _planTypeKey;
    public int? PlanTypeCode => _planTypeKey switch { "WholeSet" => 0, "WholeGet" => 1, _ => null };
    public bool HasValidContext => PlanTypeCode.HasValue && !string.IsNullOrWhiteSpace(MaterialNumber);
    public bool IsInspectionPlan => _planTypeKey == "WholeGet";
    public DelegateCommand ToggleParametersCommand { get; }
    public bool IsParametersExpanded
    {
        get => _isParametersExpanded;
        private set
        {
            if (SetProperty(ref _isParametersExpanded, value)) RaisePropertyChanged(nameof(ToggleParametersToolTip));
        }
    }
    public string ToggleParametersToolTip => IsParametersExpanded ? "收起参数配置区" : "展开参数配置区";
    public bool ResetPending { get => _resetPending; private set => SetProperty(ref _resetPending, value); }
    public bool HasInputErrors { get => _hasInputErrors; private set => SetProperty(ref _hasInputErrors, value); }
    public string InputCheckMessage { get => _inputCheckMessage; private set => SetProperty(ref _inputCheckMessage, value); }
    public string PersistenceNotice => HasValidContext
        ? string.Empty
        : "缺少物料号或有效方案类型，请从首页重新进入。";
    public string PersistenceToolTip => "方案库尚未接入，查询、维护、删除暂不可用，本页不读写数据库；两类草稿独立且未保存，切换物料将清空两类草稿，重置仅清空当前类型。";

    public IReadOnlyList<KeyValuePair<byte, string>> ProductionStatusOptions { get; } =
        Array.AsReadOnly(new[] { new KeyValuePair<byte, string>(0, "工厂态（生产态）"), new(1, "出厂态（用户态）") });
    public IReadOnlyList<KeyValuePair<byte, string>> AddressTypeOptions { get; } =
        Array.AsReadOnly(new[] { new KeyValuePair<byte, string>(0, "IP"), new(1, "域名") });
    public IReadOnlyList<KeyValuePair<byte, string>> ReportTypeOptions { get; } = Array.AsReadOnly(new[]
    {
        new KeyValuePair<byte, string>(0, "每天"), new(1, "单月"), new(2, "双月"), new(3, "每月"),
        new(4, "按分"), new(5, "按时"), new(7, "指定五时"), new(8, "指定五日"), new(9, "按天")
    });
    public IReadOnlyList<int> PageSizeOptions { get; } = Array.AsReadOnly(new[] { 10, 20, 50 });
    public int PageSize
    {
        get => _editingState?.PageSize ?? 10;
        set
        {
            if (_editingState == null || !PageSizeOptions.Contains(value) || value == _editingState.PageSize) return;
            _editingState.PageSize = value;
            RaisePropertyChanged();
        }
    }
    public int TotalCount => 0;
    public int TotalPages => 0;
    public int PageIndex => 1;
    public IReadOnlyList<AlarmHostConfigListItem> CurrentPageData { get; } = Array.Empty<AlarmHostConfigListItem>();

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var previousMaterial = MaterialNumber;
        base.OnNavigatedTo(navigationContext);
        var planType = (navigationContext.Parameters.GetValue<object>("PlanType") as string)?.Trim();
        _planTypeKey = planType is "WholeSet" or "WholeGet" ? planType : null;
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
        foreach (var name in new[] { nameof(PlanTypeKey), nameof(PlanTypeCode), nameof(HasValidContext),
                     nameof(IsInspectionPlan),
                     nameof(PersistenceNotice), nameof(PageSize) })
            RaisePropertyChanged(name);
        CheckInputCommand.RaiseCanExecuteChanged();
        RequestResetCommand.RaiseCanExecuteChanged();
        ConfirmResetCommand.RaiseCanExecuteChanged();
    }

    private void CheckInput()
    {
        var errors = Draft.GetInputErrors();
        HasInputErrors = errors.Count > 0;
        InputCheckMessage = HasInputErrors ? string.Join("\n", errors)
            : "输入检查通过，尚未保存。";
    }

    private void DraftChanged(object? sender, PropertyChangedEventArgs e)
    {
        HasInputErrors = false;
        InputCheckMessage = "未保存，请重新检查输入。";
    }

    private void ResetDraft()
    {
        Draft = new AlarmHostConfigDraft();
        if (_editingState != null) _editingState.Draft = Draft;
        ClearInputFeedback();
    }

    private void ClearInputFeedback()
    {
        ResetPending = false;
        HasInputErrors = false;
        InputCheckMessage = string.Empty;
    }

    private sealed class EditingState
    {
        public AlarmHostConfigDraft Draft { get; set; } = new();
        public int PageSize { get; set; } = 10;
    }
}

