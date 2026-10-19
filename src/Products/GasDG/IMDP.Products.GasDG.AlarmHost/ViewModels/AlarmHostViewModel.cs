using IMDP.Domain.Products;
using Prism.Events;
using Prism.Navigation.Regions;

namespace IMDP.Products.GasDG.AlarmHost.ViewModels;

/// <summary>设置/检验共用的执行页上下文；尚未接入方案加载或设备执行。</summary>
public sealed class AlarmHostViewModel : AlarmHostPageViewModel
{
    private readonly GasDGAlarmHostPlugin _plugin;
    private string? _planTypeKey;

    public AlarmHostViewModel(GasDGAlarmHostPlugin plugin, IEventAggregator eventAggregator)
        : base(plugin, eventAggregator, FunctionModuleType.WholeSet,
            "按当前类型加载对应的独立方案；设置执行可写项目并回读，检验核对期望参数及检验项目。")
    {
        _plugin = plugin;
        PageTitle = plugin.DisplayName + "设置/检验（未指定类型）";
    }

    public string? PlanTypeKey => _planTypeKey;
    public int? PlanTypeCode => _planTypeKey switch { "WholeSet" => 0, "WholeGet" => 1, _ => null };
    public bool HasValidContext => PlanTypeCode.HasValue && !string.IsNullOrWhiteSpace(MaterialNumber);

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        base.OnNavigatedTo(navigationContext);
        var planType = (navigationContext.Parameters.GetValue<object>("PlanType") as string)?.Trim();
        _planTypeKey = planType is "WholeSet" or "WholeGet" ? planType : null;
        PageTitle = _planTypeKey switch
        {
            "WholeSet" => _plugin.GetMenuDisplayName(FunctionModuleType.WholeSet),
            "WholeGet" => _plugin.GetMenuDisplayName(FunctionModuleType.WholeGet),
            _ => _plugin.DisplayName + "设置/检验（未指定类型）"
        };
        RaisePropertyChanged(nameof(PlanTypeKey));
        RaisePropertyChanged(nameof(PlanTypeCode));
        RaisePropertyChanged(nameof(HasValidContext));
    }
}
