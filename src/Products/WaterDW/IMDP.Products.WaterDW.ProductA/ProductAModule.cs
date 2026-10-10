using Prism.Ioc;
using Prism.Modularity;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;

namespace IMDP.Products.WaterDW.ProductA;

public class WaterDWProductAModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var registry = containerProvider.Resolve<IProductPluginRegistry>();
        registry.RegisterPlugin(new WaterDWProductAPlugin());
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }
}
