using Prism.Ioc;
using Prism.Modularity;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;
using IMDP.Infrastructure.Protocol;
using IMDP.Products.GasDG.WholeA.ViewModels;
using IMDP.Products.GasDG.WholeA.Views;
using IMDP.Products.GasDG.WholeA.Services;

namespace IMDP.Products.GasDG.WholeA;

public class GasDGWholeAModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var registry = containerProvider.Resolve<IProductPluginRegistry>();
        registry.RegisterPlugin(new GasDGWholeAPlugin());
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<WholeADetectService>();

        // 注册产品插件视图用于导航
        // 视图名称与 ProductPluginNavigationService.GetViewName 映射一致
        containerRegistry.RegisterForNavigation<WholeConfigView, WholeConfigViewModel>();
        containerRegistry.RegisterForNavigation<WholeView, WholeViewModel>();
    }
}
