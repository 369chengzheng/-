using System.Collections.ObjectModel;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.AlarmHost.Views;

namespace IMDP.Products.GasDG.AlarmHost;

/// <summary>
/// 报警主机总装插件：设置/检验方案分别维护，两个执行入口分别加载对应方案。
/// </summary>
public sealed class GasDGAlarmHostPlugin : ProductPluginBase, IProductPluginViewRoutes
{
    /// <summary>
    /// 报警主机插件在平台中的唯一产品标识
    /// </summary>
    public override string ProductKey => "GasDG.AlarmHost";
    /// <summary>
    /// 所属产品大类，固定为国内燃气 GasDG
    /// </summary>
    public override string Category => ProductCategory.GasDG;
    /// <summary>
    /// 插件在平台菜单等位置使用的显示名称
    /// </summary>
    public override string DisplayName => "国内燃气-报警主机总装";

    /// <summary>
    /// 插件提供的四个入口：设置方案、检验方案、设置、检验
    /// </summary>
    public override IReadOnlyList<FunctionModuleType> MenuItems { get; } =
        Array.AsReadOnly(new[]
        {
            FunctionModuleType.WholeSetConfig,
            FunctionModuleType.WholeGetConfig,
            FunctionModuleType.WholeSet,
            FunctionModuleType.WholeGet
        });

    /// <summary>
    /// 功能入口到视图的路由映射，两类配置共用配置页，两类执行共用执行页
    /// </summary>
    public IReadOnlyDictionary<FunctionModuleType, string> ViewRoutes { get; } =
        new ReadOnlyDictionary<FunctionModuleType, string>(new Dictionary<FunctionModuleType, string>
        {
            [FunctionModuleType.WholeSetConfig] = nameof(AlarmHostConfigView),
            [FunctionModuleType.WholeGetConfig] = nameof(AlarmHostConfigView),
            [FunctionModuleType.WholeSet] = nameof(AlarmHostView),
            [FunctionModuleType.WholeGet] = nameof(AlarmHostView)
        });
}
