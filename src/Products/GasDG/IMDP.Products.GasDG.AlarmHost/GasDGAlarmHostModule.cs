using IMDP.Application.Services;
using IMDP.Products.GasDG.AlarmHost.ViewModels;
using IMDP.Products.GasDG.AlarmHost.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace IMDP.Products.GasDG.AlarmHost;

/// <summary>
/// 仅注册插件及专属页面，不初始化数据库、串口或 MES 服务
/// </summary>
public sealed class GasDGAlarmHostModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<GasDGAlarmHostPlugin>();
        containerRegistry.RegisterForNavigation<AlarmHostConfigView, AlarmHostConfigViewModel>();
        containerRegistry.RegisterForNavigation<AlarmHostView, AlarmHostViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IProductPluginRegistry>()
            .RegisterPlugin(containerProvider.Resolve<GasDGAlarmHostPlugin>());
    }
}
