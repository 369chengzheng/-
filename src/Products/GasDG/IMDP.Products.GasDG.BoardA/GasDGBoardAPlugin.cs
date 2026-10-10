// =============================================================================
// 文件名：GasDGBoardAPlugin.cs
// 功能描述：国内燃气（GasDG）板卡 A 插件定义。继承 ProductPluginBase，声明产品
//          标识、分类、显示名称以及暴露给主框架的功能菜单项，供插件注册与导航使用。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Products.GasDG.BoardA;

/// <summary>
/// 国内燃气板卡 A 插件定义类。
/// <para>继承 <see cref="ProductPluginBase"/>，向主框架暴露本产品的元数据与可用菜单。</para>
/// </summary>
public class GasDGBoardAPlugin : ProductPluginBase
{
    /// <summary>
    /// 获取产品唯一标识键。
    /// </summary>
    public override string ProductKey => "GasDG.BoardA";

    /// <summary>
    /// 获取产品所属分类（国内燃气 GasDG）。
    /// </summary>
    public override string Category => ProductCategory.GasDG;

    /// <summary>
    /// 获取产品在界面中展示的名称。
    /// </summary>
    public override string DisplayName => "国内燃气-板卡A";

    /// <summary>
    /// 获取当前产品暴露给主框架的功能菜单项集合。
    /// <para>包含模块检测方案配置与模块检测两个功能模块。</para>
    /// </summary>
    public override IReadOnlyList<FunctionModuleType> MenuItems => new List<FunctionModuleType>
    {
        FunctionModuleType.BoardConfig,
        FunctionModuleType.Board
    };
}
