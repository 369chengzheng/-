using System.Collections.ObjectModel;
using IMDP.Domain.Products;

namespace IMDP.Products.GasOG.ProductA;

public class GasOGProductAPlugin : ProductPluginBase, IProductPluginViewRoutes
{
    public override string ProductKey => "GasOG.ProductA";
    public override string Category => ProductCategory.GasOG;
    public override string DisplayName => "海外燃气-产品A";

    public override IReadOnlyList<FunctionModuleType> MenuItems => new List<FunctionModuleType>
    {
        FunctionModuleType.WholeSetConfig,
        FunctionModuleType.WholeGetConfig,
        FunctionModuleType.WholeSet,
        FunctionModuleType.WholeGet
    };

    /// <summary>空壳插件尚无自有视图；空路由表使导航返回 null（记 Warn 日志），不再落入其他产品的共享页面。</summary>
    public IReadOnlyDictionary<FunctionModuleType, string> ViewRoutes { get; } =
        new ReadOnlyDictionary<FunctionModuleType, string>(new Dictionary<FunctionModuleType, string>());
}
