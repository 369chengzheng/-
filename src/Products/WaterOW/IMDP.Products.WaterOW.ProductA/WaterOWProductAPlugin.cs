using IMDP.Domain.Products;

namespace IMDP.Products.WaterOW.ProductA;

public class WaterOWProductAPlugin : ProductPluginBase
{
    public override string ProductKey => "WaterOW.ProductA";
    public override string Category => ProductCategory.WaterOW;
    public override string DisplayName => "海外水务-产品A";

    public override IReadOnlyList<FunctionModuleType> MenuItems => new List<FunctionModuleType>
    {
        FunctionModuleType.WholeSetConfig,
        FunctionModuleType.WholeGetConfig,
        FunctionModuleType.WholeSet,
        FunctionModuleType.WholeGet
    };
}
