using System.Collections.ObjectModel;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.AlarmHost.Views;

namespace IMDP.Products.GasDG.AlarmHost;

/// <summary>报警主机总装插件：设置/检验方案分别维护，两个执行入口分别加载对应方案。</summary>
public sealed class GasDGAlarmHostPlugin : ProductPluginBase, IProductPluginViewRoutes
{
    public override string ProductKey => "GasDG.AlarmHost";
    public override string Category => ProductCategory.GasDG;
    public override string DisplayName => "国内燃气-报警主机总装";

    public override IReadOnlyList<FunctionModuleType> MenuItems { get; } =
        Array.AsReadOnly(new[]
        {
            FunctionModuleType.WholeSetConfig,
            FunctionModuleType.WholeGetConfig,
            FunctionModuleType.WholeSet,
            FunctionModuleType.WholeGet
        });

    public IReadOnlyDictionary<FunctionModuleType, string> ViewRoutes { get; } =
        new ReadOnlyDictionary<FunctionModuleType, string>(new Dictionary<FunctionModuleType, string>
        {
            [FunctionModuleType.WholeSetConfig] = nameof(AlarmHostConfigView),
            [FunctionModuleType.WholeGetConfig] = nameof(AlarmHostConfigView),
            [FunctionModuleType.WholeSet] = nameof(AlarmHostView),
            [FunctionModuleType.WholeGet] = nameof(AlarmHostView)
        });
}
