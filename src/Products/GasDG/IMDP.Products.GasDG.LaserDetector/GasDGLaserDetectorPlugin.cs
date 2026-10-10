using IMDP.Domain.Products;

namespace IMDP.Products.GasDG.LaserDetector;

/// <summary>
/// 家用可燃气体激光检测仪产品插件
/// <para>向主程序注册产品键名、类别、显示名称与功能菜单。</para>
/// </summary>
public class GasDGLaserDetectorPlugin : ProductPluginBase
{
    /// <summary>产品插件唯一键名</summary>
    public override string ProductKey => "GasDG.LaserDetector";

    /// <summary>产品类别（国内燃气）</summary>
    public override string Category => ProductCategory.GasDG;

    /// <summary>产品显示名称</summary>
    public override string DisplayName => "国内燃气-家用可燃气体激光检测仪";

    /// <summary>该插件提供的功能菜单项列表</summary>
    public override IReadOnlyList<FunctionModuleType> MenuItems => new List<FunctionModuleType>
    {
        FunctionModuleType.BoardConfig,
        FunctionModuleType.Board
    };
}
