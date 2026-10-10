using Prism.Ioc;
using Prism.Modularity;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;

namespace IMDP.Products.GasOG.ProductA;

public class GasOGProductAModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var registry = containerProvider.Resolve<IProductPluginRegistry>();
        registry.RegisterPlugin(new GasOGProductAPlugin());
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }
}
