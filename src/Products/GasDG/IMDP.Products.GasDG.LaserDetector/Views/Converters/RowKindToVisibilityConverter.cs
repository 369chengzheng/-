// =============================================================================
// 文件名：RowKindToVisibilityConverter.cs
// 功能描述：表格行类型到可见性的转换器（严格 MVVM，纯视图转换）。
//           统一结果表格的单元格模板按行类型（Kind）分派显示：
//           Auto 行显示实测值文本、Manual 行显示正常/异常下拉、
//           Barcode 行显示扫码输入框；每个行类型一个转换器实例（XAML
//           StaticResource 直接引用，避免每处绑定重复书写参数）。
// =============================================================================
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using IMDP.Products.GasDG.LaserDetector.Models;

namespace IMDP.Products.GasDG.LaserDetector.Views.Converters;

/// <summary>
/// 表格行类型到可见性转换器（基类）
/// <para>Binding 值为行类型（LaserDetectorResultRowKind）；行类型与
/// <see cref="ExpectedKind"/> 一致返回 Visible，否则 Collapsed
/// （非匹配行模板不参与渲染与命中测试）。</para>
/// </summary>
public abstract class RowKindToVisibilityConverterBase : IValueConverter
{
    /// <summary>该转换器期望匹配的行类型（由子类固定）</summary>
    protected abstract LaserDetectorResultRowKind ExpectedKind { get; }

    /// <summary>
    /// 行类型匹配判断
    /// </summary>
    /// <param name="value">绑定值（LaserDetectorResultRowKind 枚举）</param>
    /// <param name="targetType">目标类型（忽略）</param>
    /// <param name="parameter">转换参数（未使用）</param>
    /// <param name="culture">区域信息（忽略）</param>
    /// <returns>行类型匹配返回 <see cref="Visibility.Visible"/>，否则 Collapsed</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is LaserDetectorResultRowKind kind && kind == ExpectedKind
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// 不支持反向转换（单向展示）
    /// </summary>
    /// <param name="value">绑定值（忽略）</param>
    /// <param name="targetType">目标类型（忽略）</param>
    /// <param name="parameter">转换参数（忽略）</param>
    /// <param name="culture">区域信息（忽略）</param>
    /// <returns>不返回（抛出 NotSupportedException）</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("RowKindToVisibilityConverter 不支持反向转换。");
    }
}

/// <summary>自动结果行转换器：Kind=Auto 时可见（实测值文本单元格）</summary>
public sealed class AutoRowToVisibilityConverter : RowKindToVisibilityConverterBase
{
    /// <inheritdoc/>
    protected override LaserDetectorResultRowKind ExpectedKind => LaserDetectorResultRowKind.Auto;
}

/// <summary>人工测试行转换器：Kind=Manual 时可见（正常/异常下拉单元格）</summary>
public sealed class ManualRowToVisibilityConverter : RowKindToVisibilityConverterBase
{
    /// <inheritdoc/>
    protected override LaserDetectorResultRowKind ExpectedKind => LaserDetectorResultRowKind.Manual;
}

/// <summary>表具条码行转换器：Kind=Barcode 时可见（扫码输入框单元格）</summary>
public sealed class BarcodeRowToVisibilityConverter : RowKindToVisibilityConverterBase
{
    /// <inheritdoc/>
    protected override LaserDetectorResultRowKind ExpectedKind => LaserDetectorResultRowKind.Barcode;
}
