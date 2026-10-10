using IMDP.Domain.Products;

namespace IMDP.Products.GasOG.ProductA;

public class GasOGProductAPlugin : ProductPluginBase
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
}
