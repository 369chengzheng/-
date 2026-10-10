// =============================================================================
// 文件名：FunctionModuleType.cs
// 功能描述：功能模块类型枚举及其显示名称扩展方法，定义产品插件可提供的功能菜单项。
// =============================================================================
namespace IMDP.Domain.Products;

/// <summary>
/// 功能模块类型枚举
/// <para>定义产品插件可暴露给主界面的功能菜单项，区分整机设置/检验与模块检测等业务场景。</para>
/// </summary>
public enum FunctionModuleType
{
    /// <summary>整机设置方案配置（维护整机设置检测参数模板）</summary>
    WholeSetConfig,

    /// <summary>整机检验方案配置（维护整机检验检测参数模板）</summary>
    WholeGetConfig,

    /// <summary>整机设置（执行整机设置工位检测）</summary>
    WholeSet,

    /// <summary>整机检验（执行整机检验工位检测）</summary>
    WholeGet,

    /// <summary>模块检测方案配置（维护板卡检测参数模板）</summary>
    BoardConfig,

    /// <summary>模块检测（执行板卡检测工位检测）</summary>
    Board
}

/// <summary>
/// 功能模块类型扩展方法
/// <para>提供将枚举值转换为中文显示名称的便捷方法。</para>
/// </summary>
public static class FunctionModuleTypeExtensions
{
    /// <summary>获取功能模块类型的中文显示名称</summary>
    /// <param name="type">功能模块类型</param>
    /// <returns>中文显示名称；未匹配时返回枚举名</returns>
    public static string GetDisplayName(this FunctionModuleType type)
    {
        return type switch
        {
            FunctionModuleType.WholeSetConfig => "整机设置方案配置",
            FunctionModuleType.WholeGetConfig => "整机检验方案配置",
            FunctionModuleType.WholeSet => "整机设置",
            FunctionModuleType.WholeGet => "整机检验",
            FunctionModuleType.BoardConfig => "模块检测方案配置",
            FunctionModuleType.Board => "模块检测",
            _ => type.ToString()
        };
    }
}
