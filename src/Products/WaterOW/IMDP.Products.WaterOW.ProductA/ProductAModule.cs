using Prism.Ioc;
using Prism.Modularity;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;

namespace IMDP.Products.WaterOW.ProductA;

public class WaterOWProductAModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var registry = containerProvider.Resolve<IProductPluginRegistry>();
        registry.RegisterPlugin(new WaterOWProductAPlugin());
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }
}
