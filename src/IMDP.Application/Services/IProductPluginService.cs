// =============================================================================
// 文件名：IProductPluginService.cs
// 功能描述：产品插件服务接口集合，定义插件注册表与插件导航两个契约。
//           插件注册表管理产品插件的注册/查询及物料号到功能模块的映射；
//           插件导航服务负责功能模块到视图名称的解析。
// =============================================================================
using IMDP.Application.DTOs;
using IMDP.Domain.Products;

namespace IMDP.Application.Services;

/// <summary>
/// 产品插件注册表接口
/// <para>管理产品插件的注册、注销、查询，并根据物料号解析其绑定的功能模块。</para>
/// <para>实现类为 <see cref="ProductPluginRegistry"/>，依赖 <see cref="IMDP.Domain.Interfaces.IMaterialProductMapRepository"/> 获取物料-插件映射。</para>
/// </summary>
public interface IProductPluginRegistry
{
    /// <summary>注册产品插件（按 ProductKey 索引，重复注册会覆盖）</summary>
    /// <param name="plugin">待注册的产品插件实例</param>
    void RegisterPlugin(ProductPluginBase plugin);

    /// <summary>注销指定 ProductKey 的产品插件</summary>
    /// <param name="productKey">产品标识</param>
    void UnregisterPlugin(string productKey);

    /// <summary>获取所有已注册插件的 DTO 列表</summary>
    /// <returns>产品插件信息只读列表</returns>
    IReadOnlyList<ProductPluginDto> GetAllPlugins();

    /// <summary>获取指定 ProductKey 的插件信息</summary>
    /// <param name="productKey">产品标识</param>
    /// <returns>插件信息 DTO；不存在返回 null</returns>
    ProductPluginDto? GetPlugin(string productKey);

    /// <summary>按现有功能类型去重规则解析菜单，保留首次提供该功能的插件标识和标题。</summary>
    Task<IReadOnlyList<ProductMenuItemDto>> GetMenuEntriesForMaterialAsync(string materialNumber);

    /// <summary>
    /// 获取物料号对应的菜单项显示名称列表
    /// <para>与主窗口菜单共用 <see cref="GetMenuEntriesForMaterialAsync"/> 的标题生成结果。</para>
    /// </summary>
    /// <param name="materialNumber">物料号</param>
    /// <returns>菜单项显示名称只读列表</returns>
    Task<IReadOnlyList<string>> GetMenuItemsForMaterialAsync(string materialNumber);

    /// <summary>
    /// 根据物料号返回所有绑定产品插件提供的功能模块类型列表（去重）
    /// </summary>
    Task<IReadOnlyList<FunctionModuleType>> GetMenuFunctionModulesForMaterialAsync(string materialNumber);
}

/// <summary>
/// 产品插件导航服务接口
/// <para>将功能模块类型解析为具体的视图名称与 PlanType 参数，供 Prism 导航使用。</para>
/// <para>实现类为 <see cref="ProductPluginNavigationService"/>。</para>
/// </summary>
public interface IProductPluginNavigationService
{
    /// <summary>导航到指定功能模块（当前实现为空，预留扩展点）</summary>
    /// <param name="moduleType">功能模块类型</param>
    /// <param name="productKey">所属产品标识</param>
    void NavigateTo(FunctionModuleType moduleType, string productKey);

    /// <summary>获取功能模块对应的视图名称</summary>
    /// <param name="moduleType">功能模块类型</param>
    /// <returns>视图名称（如 WholeConfigView）；无对应视图返回 null</returns>
    string? GetViewName(FunctionModuleType moduleType, string? productKey = null);

    /// <summary>
    /// 获取功能模块对应的 PlanType 参数（WholeSet/WholeGet），其他返回 null
    /// </summary>
    string? GetPlanType(FunctionModuleType moduleType);
}
