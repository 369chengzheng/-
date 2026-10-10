// =============================================================================
// 文件名：ProductPluginRegistry.cs
// 功能描述：产品插件注册表实现，管理产品插件的注册/注销/查询，
//           并根据物料号解析其绑定的产品插件功能模块清单（驱动菜单加载）。
// =============================================================================
using IMDP.Application.DTOs;
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;
using Newtonsoft.Json;

namespace IMDP.Application.Services;

/// <summary>
/// 产品插件注册表实现
/// <para>实现 <see cref="IProductPluginRegistry"/>，使用内存字典维护已注册插件。</para>
/// <para>依赖 <see cref="IMaterialProductMapRepository"/> 查询物料号对应的产品插件 ProductKey 列表（JSON 存储）。</para>
/// </summary>
public class ProductPluginRegistry : IProductPluginRegistry
{
    /// <summary>已注册插件字典（键为 ProductKey，值为插件实例）</summary>
    private readonly Dictionary<string, ProductPluginBase> _plugins = new();

    /// <summary>物料-产品插件映射仓储，用于查询物料号绑定的插件列表</summary>
    private readonly IMaterialProductMapRepository _materialMapRepository;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="materialMapRepository">物料-产品插件映射仓储（由 DI 注入）</param>
    public ProductPluginRegistry(IMaterialProductMapRepository materialMapRepository)
    {
        _materialMapRepository = materialMapRepository;
    }

    /// <inheritdoc/>
    public void RegisterPlugin(ProductPluginBase plugin)
    {
        _plugins[plugin.ProductKey] = plugin;
    }

    /// <inheritdoc/>
    public void UnregisterPlugin(string productKey)
    {
        _plugins.Remove(productKey);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProductPluginDto> GetAllPlugins()
    {
        return _plugins.Values.Select(p => new ProductPluginDto
        {
            ProductKey = p.ProductKey,
            Category = p.Category,
            DisplayName = p.DisplayName,
            MenuItems = p.MenuItems.Select(p.GetMenuDisplayName).ToList()
        }).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public ProductPluginDto? GetPlugin(string productKey)
    {
        if (_plugins.TryGetValue(productKey, out var plugin))
        {
            return new ProductPluginDto
            {
                ProductKey = plugin.ProductKey,
                Category = plugin.Category,
                DisplayName = plugin.DisplayName,
                MenuItems = plugin.MenuItems.Select(plugin.GetMenuDisplayName).ToList()
            };
        }
        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetMenuItemsForMaterialAsync(string materialNumber)
    {
        var menuItems = await GetMenuEntriesForMaterialAsync(materialNumber);
        return menuItems.Select(item => item.DisplayName).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductMenuItemDto>> GetMenuEntriesForMaterialAsync(string materialNumber)
    {
        var menuItems = new List<ProductMenuItemDto>();
        var seenModules = new HashSet<FunctionModuleType>();
        var map = await _materialMapRepository.GetByMaterialNumberAsync(materialNumber);
        if (map == null || string.IsNullOrEmpty(map.ProductPluginsJson))
            return menuItems.AsReadOnly();

        var pluginKeys = JsonConvert.DeserializeObject<List<string>>(map.ProductPluginsJson);
        if (pluginKeys == null)
            return menuItems.AsReadOnly();

        // 与原实现一致：按映射 JSON 顺序遍历，同一功能类型只保留首次出现的插件。
        foreach (var key in pluginKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || !_plugins.TryGetValue(key, out var plugin))
                continue;

            foreach (var moduleType in plugin.MenuItems)
            {
                if (seenModules.Add(moduleType))
                    menuItems.Add(new ProductMenuItemDto(
                        plugin.ProductKey, moduleType, plugin.GetMenuDisplayName(moduleType)));
            }
        }

        return menuItems.AsReadOnly();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 解析流程：根据物料号查询映射记录 → 反序列化 ProductKey 列表 →
    /// 在已注册插件中查找每个 Key → 收集其 MenuItems 并去重。
    /// </remarks>
    public async Task<IReadOnlyList<FunctionModuleType>> GetMenuFunctionModulesForMaterialAsync(string materialNumber)
    {
        var modules = new HashSet<FunctionModuleType>();

        // 查询物料号对应的产品插件映射
        var map = await _materialMapRepository.GetByMaterialNumberAsync(materialNumber);

        if (map == null || string.IsNullOrEmpty(map.ProductPluginsJson))
            return modules.ToList().AsReadOnly();

        // 反序列化绑定的 ProductKey 列表
        var pluginKeys = JsonConvert.DeserializeObject<List<string>>(map.ProductPluginsJson);
        if (pluginKeys != null)
        {
            // 遍历每个 ProductKey，收集已注册插件的功能模块（HashSet 自动去重）
            foreach (var key in pluginKeys)
            {
                if (_plugins.TryGetValue(key, out var plugin))
                {
                    foreach (var item in plugin.MenuItems)
                    {
                        modules.Add(item);
                    }
                }
            }
        }

        return modules.ToList().AsReadOnly();
    }
}
