// =============================================================================
// 文件名：ProductCategory.cs
// 功能描述：产品类别常量类，定义所有支持的产品类别键名与中文显示名称映射。
// =============================================================================
namespace IMDP.Domain.Products;

/// <summary>
/// 产品类别常量类
/// <para>以常量形式定义所有支持的产品类别键名，供物料映射与插件加载时引用。</para>
/// </summary>
public static class ProductCategory
{
    /// <summary>国内燃气（GasDG）</summary>
    public const string GasDG = "GasDG";

    /// <summary>国内水务（WaterDW）</summary>
    public const string WaterDW = "WaterDW";

    /// <summary>海外燃气（GasOG）</summary>
    public const string GasOG = "GasOG";

    /// <summary>海外水务（WaterOW）</summary>
    public const string WaterOW = "WaterOW";

    /// <summary>按类别键名获取中文显示名称</summary>
    /// <param name="category">类别键名（如 GasDG）</param>
    /// <returns>中文显示名称；未匹配时原样返回</returns>
    public static string GetDisplayName(string category)
    {
        return category switch
        {
            GasDG => "国内燃气",
            WaterDW => "国内水务",
            GasOG => "海外燃气",
            WaterOW => "海外水务",
            _ => category
        };
    }
}