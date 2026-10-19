namespace IMDP.Domain.Products;

/// <summary>插件可选的功能页面映射；不依赖 WPF 或 Prism，未提供此契约的旧插件沿用平台原映射</summary>
public interface IProductPluginViewRoutes
{
    /// <summary>插件提供的功能到已注册视图名的只读映射；未声明的功能不回退到其他产品页面</summary>
    IReadOnlyDictionary<FunctionModuleType, string> ViewRoutes { get; }
}
