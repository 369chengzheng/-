// =============================================================================
// 文件名：ProductPluginNavigationService.cs
// 功能描述：产品插件导航服务实现，将功能模块类型映射为 Prism 导航所需的视图名称与 PlanType 参数。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Application.Services;

/// <summary>
/// 产品插件导航服务实现
/// <para>实现 <see cref="IProductPluginNavigationService"/>，提供功能模块到视图名称的静态映射。</para>
/// <para>映射规则：配置类模块（WholeSetConfig/WholeGetConfig）→ WholeConfigView；</para>
/// <para>检测类模块（WholeSet/WholeGet）→ WholeView；板卡配置 → LaserDetectorView；板卡检测 → BoardView。</para>
/// </summary>
public class ProductPluginNavigationService : IProductPluginNavigationService
{
    /// <inheritdoc/>
    /// <remarks>当前为空实现，导航逻辑由调用方通过 GetViewName 自行处理。</remarks>
    public void NavigateTo(FunctionModuleType moduleType, string productKey)
    {
    }

    /// <inheritdoc/>
    public string? GetViewName(FunctionModuleType moduleType, string? productKey = null)
    {
        return moduleType switch
        {
            FunctionModuleType.WholeSetConfig or FunctionModuleType.WholeGetConfig => "WholeConfigView",
            FunctionModuleType.WholeSet or FunctionModuleType.WholeGet => "WholeView",
            FunctionModuleType.BoardConfig when productKey == "GasDG.BoardA" => "BoardConfigView",
            FunctionModuleType.BoardConfig when productKey == "GasDG.LaserDetector" || string.IsNullOrEmpty(productKey) => "LaserDetectorView",
            FunctionModuleType.Board => "BoardView",
            _ => null
        };
    }

    /// <inheritdoc/>
    public string? GetPlanType(FunctionModuleType moduleType)
    {
        return moduleType switch
        {
            FunctionModuleType.WholeSetConfig or FunctionModuleType.WholeSet => "WholeSet",
            FunctionModuleType.WholeGetConfig or FunctionModuleType.WholeGet => "WholeGet",
            _ => null
        };
    }
}
