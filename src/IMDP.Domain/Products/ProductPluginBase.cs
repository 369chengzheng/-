// =============================================================================
// 文件名：ProductPluginBase.cs
// 功能描述：产品插件抽象基类，定义所有产品插件需实现的标识、类别、显示名与功能菜单契约。
//           具体产品插件（如 GasDG.ProductA）继承本类并重写抽象属性。
// =============================================================================
namespace IMDP.Domain.Products;

/// <summary>
/// 产品插件抽象基类
/// <para>定义产品插件向主程序暴露的元信息（键名、类别、显示名）与功能菜单项列表。</para>
/// <para>各产品插件项目继承本类并在 DI 容器中注册，由主程序按物料映射动态加载。</para>
/// </summary>
public abstract class ProductPluginBase
{
    /// <summary>产品插件唯一键名（用于物料映射与插件查找，如 "GasDG.ProductA"）</summary>
    public abstract string ProductKey { get; }

    /// <summary>产品类别（对应 <see cref="ProductCategory"/> 中的常量，如 GasDG）</summary>
    public abstract string Category { get; }

    /// <summary>产品显示名称（用于 UI 展示）</summary>
    public abstract string DisplayName { get; }

    /// <summary>该插件提供的功能菜单项列表（由主程序据动态生成功能入口）</summary>
    public abstract IReadOnlyList<FunctionModuleType> MenuItems { get; }

    /// <summary>以插件展示名为唯一产品名称来源，统一生成具体功能菜单标题。</summary>
    public string GetMenuDisplayName(FunctionModuleType moduleType)
    {
        var productName = string.IsNullOrWhiteSpace(DisplayName) ? ProductKey : DisplayName.Trim();
        var suffix = moduleType switch
        {
            FunctionModuleType.WholeSetConfig => "设置方案配置",
            FunctionModuleType.WholeGetConfig => "检验方案配置",
            FunctionModuleType.WholeSet => "设置",
            FunctionModuleType.WholeGet => "检验",
            FunctionModuleType.BoardConfig => "方案配置",
            FunctionModuleType.Board => "检测",
            _ => moduleType.GetDisplayName()
        };
        return productName + suffix;
    }
}