using IMDP.Domain.Products;

namespace IMDP.Products.WaterDW.ProductA;

public class WaterDWProductAPlugin : ProductPluginBase
{
    public override string ProductKey => "WaterDW.ProductA";
    public override string Category => ProductCategory.WaterDW;
    public override string DisplayName => "国内水务-产品A";

    public override IReadOnlyList<FunctionModuleType> MenuItems => new List<FunctionModuleType>
    {
        FunctionModuleType.WholeSetConfig,
        FunctionModuleType.WholeGetConfig,
        FunctionModuleType.WholeSet,
        FunctionModuleType.WholeGet
    };
}
