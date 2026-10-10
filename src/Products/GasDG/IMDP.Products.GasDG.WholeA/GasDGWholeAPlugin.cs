using IMDP.Domain.Products;

namespace IMDP.Products.GasDG.WholeA;

public class GasDGWholeAPlugin : ProductPluginBase
{
    public override string ProductKey => "GasDG.WholeA";
    public override string Category => ProductCategory.GasDG;
    public override string DisplayName => "国内燃气-整机A";

    public override IReadOnlyList<FunctionModuleType> MenuItems => new List<FunctionModuleType>
    {
        FunctionModuleType.WholeSetConfig,
        FunctionModuleType.WholeGetConfig,
        FunctionModuleType.WholeSet,
        FunctionModuleType.WholeGet
    };
}
