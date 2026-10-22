using IMDP.Domain.Products;
using IMDP.Products.GasDG.AlarmHost.Models;
using Prism.Events;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>
/// 设置/检验共用的执行页上下文；尚未接入方案加载或设备执行
/// </summary>
public sealed class AlarmHostViewModel : AlarmHostPageViewModel
{
    /// <summary>
    /// 当前报警主机插件，用于取得设置或检验入口名称
    /// </summary>
    private readonly GasDGAlarmHostPlugin _plugin;
    /// <summary>
    /// 导航中的方案类型键，WholeSet=设置、WholeGet=检验，空值表示未指定有效类型
    /// </summary>
    private string? _planTypeKey;

    public AlarmHostViewModel(GasDGAlarmHostPlugin plugin, IEventAggregator eventAggregator)
        : base(plugin, eventAggregator, FunctionModuleType.WholeSet,
            "按当前类型加载对应的独立方案；设置执行可写项目并回读，检验核对期望参数及检验项目。")
    {
        _plugin = plugin;
        PageTitle = plugin.DisplayName + "设置/检验（未指定类型）";
    }

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
        base.OnNavigatedTo(navigationContext);
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
    }
}
