using Prism.Ioc;
using Prism.Modularity;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.LaserDetector.Services;

namespace IMDP.Products.GasDG.LaserDetector;

/// <summary>
/// 家用可燃气体激光检测仪插件模块
/// <para>负责将产品插件注册到插件注册表，并注册模块内导航视图。</para>
/// </summary>
public class GasDGLaserDetectorModule : IModule
{
    /// <summary>
    /// 模块初始化时向插件注册表注册当前产品插件
    /// </summary>
    /// <param name="containerProvider">容器提供者</param>
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var registry = containerProvider.Resolve<IProductPluginRegistry>();
        registry.RegisterPlugin(new GasDGLaserDetectorPlugin());
    }

    /// <summary>
    /// 注册模块内的服务与导航视图类型
    /// </summary>
    /// <param name="containerRegistry">容器注册器</param>
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 激光检测服务（单例：复用串口连接与配置缓存）
        containerRegistry.RegisterSingleton<LaserDetectorDetectService>();

        // 模块检测方案配置视图（与 ProductPluginNavigationService.GetViewName 的 LaserDetectorConfigView 映射一致）
        containerRegistry.RegisterForNavigation<Views.LaserDetectorConfigView, ViewModels.LaserDetectorConfigViewModel>();

        // 模块检测校验视图（与 ProductPluginNavigationService.GetViewName 的 LaserDetectorView 映射一致）
        containerRegistry.RegisterForNavigation<Views.LaserDetectorView, ViewModels.LaserDetectorViewModel>();
    }
}
