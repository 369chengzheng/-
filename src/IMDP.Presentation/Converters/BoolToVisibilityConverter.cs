// =============================================================================
// 文件名：BoolToVisibilityConverter.cs
// 功能描述：布尔值与 Visibility 枚举之间的双向转换器。
//           提供 BoolToVisibilityConverter（正向）与 InverseBoolToVisibilityConverter（反向）两个类型，
//           用于 XAML 绑定中根据布尔状态控制元素显示/折叠。
// =============================================================================
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IMDP.Presentation.Converters;

/// <summary>
/// 布尔值到可见性的正向转换器
/// <para>转换规则：<c>true</c> → <see cref="Visibility.Visible"/>，<c>false</c> 或非布尔值 → <see cref="Visibility.Collapsed"/>。</para>
/// <para>反向转换：<see cref="Visibility.Visible"/> → <c>true</c>，其他 → <c>false</c>。</para>
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 正向转换：布尔值 → Visibility
    /// </summary>
    /// <param name="value">源值（期望为 bool）</param>
    /// <param name="targetType">目标类型（Visibility）</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（未使用）</param>
    /// <returns>true 返回 Visible；否则返回 Collapsed</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    /// <summary>
    /// 反向转换：Visibility → 布尔值
    /// </summary>
    /// <param name="value">源值（期望为 Visibility）</param>
    /// <param name="targetType">目标类型（bool）</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（未使用）</param>
    /// <returns>Visible 返回 true；其他返回 false</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility == Visibility.Visible;
        return false;
    }
}

/// <summary>
/// 布尔值到可见性的反向转换器
/// <para>转换规则：<c>true</c> → <see cref="Visibility.Collapsed"/>，<c>false</c> 或非布尔值 → <see cref="Visibility.Visible"/>。</para>
/// <para>反向转换：<see cref="Visibility.Visible"/> → <c>false</c>，其他 → <c>true</c>。</para>
/// <para>常用于"未查询时显示提示，查询中隐藏"等场景。</para>
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 正向转换：布尔值 → Visibility（与正向转换器相反）
    /// </summary>
    /// <param name="value">源值（期望为 bool）</param>
    /// <param name="targetType">目标类型（Visibility）</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（未使用）</param>
    /// <returns>true 返回 Collapsed；否则返回 Visible</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    /// <summary>
    /// 反向转换：Visibility → 布尔值（与正向转换器相反）
    /// </summary>
    /// <param name="value">源值（期望为 Visibility）</param>
    /// <param name="targetType">目标类型（bool）</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（未使用）</param>
    /// <returns>Visible 返回 false；其他返回 true</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility != Visibility.Visible;
        return true;
    }
}
