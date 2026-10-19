using System.Collections.ObjectModel;
using IMDP.Domain.Products;
using IMDP.Products.GasDG.WholeA.Views;

namespace IMDP.Products.GasDG.WholeA;

public class GasDGWholeAPlugin : ProductPluginBase, IProductPluginViewRoutes
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

    public IReadOnlyDictionary<FunctionModuleType, string> ViewRoutes { get; } =
        new ReadOnlyDictionary<FunctionModuleType, string>(new Dictionary<FunctionModuleType, string>
        {
            [FunctionModuleType.WholeSetConfig] = nameof(WholeConfigView),
            [FunctionModuleType.WholeGetConfig] = nameof(WholeConfigView),
            [FunctionModuleType.WholeSet] = nameof(WholeView),
            [FunctionModuleType.WholeGet] = nameof(WholeView)
        });
}
