// =============================================================================
// 文件名：ProductPluginNavigationService.cs
// 功能描述：产品插件导航服务实现，将功能模块类型映射为 Prism 导航所需的视图名称与 PlanType 参数。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Application.Services;

/// <summary>
/// 产品插件导航服务实现
/// <para>优先读取已注册插件的自有页面映射——实体插件（WholeA/BoardA/AlarmHost）声明真实路由，
/// 空壳插件（GasOG/WaterDW/WaterOW 的 ProductA）声明空路由表（导航返回 null 并由调用方记 Warn 日志）。</para>
/// <para>静态映射仅剩 LaserDetector 专属兜底（暂未实现路由契约）。</para>
/// </summary>
public class ProductPluginNavigationService : IProductPluginNavigationService
{
    private readonly IProductPluginRegistry _pluginRegistry;

    public ProductPluginNavigationService(IProductPluginRegistry pluginRegistry)
    {
        _pluginRegistry = pluginRegistry;
    }

    /// <inheritdoc/>
    /// <remarks>当前为空实现，导航逻辑由调用方通过 GetViewName 自行处理。</remarks>
    public void NavigateTo(FunctionModuleType moduleType, string productKey)
    {
    }

    /// <inheritdoc/>
    public string? GetViewName(FunctionModuleType moduleType, string? productKey = null)
    {
        if (!string.IsNullOrEmpty(productKey))
        {
            var plugin = _pluginRegistry.GetRegisteredPlugin(productKey);
            if (plugin == null) return null;
            if (plugin is IProductPluginViewRoutes routes)
                return plugin.MenuItems.Contains(moduleType) &&
                       routes.ViewRoutes.TryGetValue(moduleType, out var viewName) &&
                       !string.IsNullOrWhiteSpace(viewName) ? viewName : null;
        }

        return moduleType switch
        {
            FunctionModuleType.BoardConfig when productKey == "GasDG.LaserDetector" || string.IsNullOrEmpty(productKey) => "LaserDetectorConfigView",
            FunctionModuleType.Board when productKey == "GasDG.LaserDetector" => "LaserDetectorView",
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
