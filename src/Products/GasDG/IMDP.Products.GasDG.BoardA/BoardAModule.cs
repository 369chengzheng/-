// =============================================================================
// 文件名：BoardAModule.cs
// 功能描述：国内燃气（GasDG）板卡 A 的 Prism 模块定义。负责向主框架注册插件、
//          注册产品检测服务和导航视图，使主框架能够识别并加载本产品模块。
// =============================================================================
using Prism.Ioc;
using Prism.Modularity;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;
using IMDP.Products.GasDG.BoardA.ViewModels;
using IMDP.Products.GasDG.BoardA.Views;

namespace IMDP.Products.GasDG.BoardA;

/// <summary>
/// 国内燃气板卡 A 的 Prism 模块定义类。
/// <para>实现 <see cref="IModule"/> 接口，在应用启动时完成插件注册与视图导航注册。</para>
/// </summary>
public class BoardAModule : IModule
{
    /// <summary>
    /// 模块初始化时调用，向产品插件注册表注册当前插件。
    /// </summary>
    /// <param name="containerProvider">DI 容器提供者</param>
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var registry = containerProvider.Resolve<IProductPluginRegistry>();
        registry.RegisterPlugin(new GasDGBoardAPlugin());
    }

    /// <summary>
    /// 注册类型到 DI 容器，包含产品视图与视图模型的导航注册。
    /// </summary>
    /// <param name="containerRegistry">容器注册器</param>
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 注册产品插件视图用于导航
        // 视图名称与 ProductPluginNavigationService.GetViewName 映射一致
        containerRegistry.RegisterForNavigation<BoardConfigView, BoardConfigViewModel>();
        containerRegistry.RegisterForNavigation<BoardView, BoardViewModel>();
    }
}
