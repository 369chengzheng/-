// =============================================================================
// 文件名：FunctionModuleTypeDisplayConverter.cs
// 功能描述：将 FunctionModuleType 枚举值转换为本地化显示名称的单向 XAML 转换器。
//           供菜单/工具栏等绑定枚举值的界面使用，避免在 XAML 中硬编码中文文案。
// =============================================================================
using System.Globalization;
using System.Windows.Data;
using IMDP.Domain.Products;

namespace IMDP.Presentation.Converters;

/// <summary>
/// 功能模块类型枚举到显示名称的 XAML 转换器
/// <para>调用 <see cref="FunctionModuleType"/> 扩展方法 <c>GetDisplayName()</c> 获取中文显示名；
/// 非枚举值则回退为 <c>ToString()</c>。反向转换返回 <see cref="Binding.DoNothing"/>。</para>
/// <para>提供静态 <see cref="Instance"/> 单例，便于在 XAML 中通过 <c>x:Static</c> 直接引用。</para>
/// </summary>
public class FunctionModuleTypeDisplayConverter : IValueConverter
{
    /// <summary>转换器单例实例，避免在 XAML 资源中重复声明</summary>
    public static readonly FunctionModuleTypeDisplayConverter Instance = new();

    /// <summary>
    /// 正向转换：FunctionModuleType → 显示名称字符串
    /// </summary>
    /// <param name="value">源值（期望为 <see cref="FunctionModuleType"/>）</param>
    /// <param name="targetType">目标类型（string）</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（未使用）</param>
    /// <returns>枚举的中文显示名；非枚举值返回其字符串形式</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FunctionModuleType type)
        {
            return type.GetDisplayName();
        }
        return value?.ToString();
    }

    /// <summary>
    /// 反向转换：不支持，返回 <see cref="Binding.DoNothing"/>
    /// </summary>
    /// <param name="value">源值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（未使用）</param>
    /// <returns>始终返回 <see cref="Binding.DoNothing"/>，保持绑定源不变</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
