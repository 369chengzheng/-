using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.WholeA.ViewModels;

/// <summary>
/// 检测方案展示参数包装类
/// <para>包装 TestParameterDto，附加 UI 展示所需的计算属性：
/// HasLimits（是否有上下限）、IsChildParameter（是否为子参数）</para>
/// <para>用于 WholeView 检测方案显示区，与 WholeConfigView 的 ConfigParameter 区分，
/// 此类仅用于只读展示，不包含编辑器联动逻辑。</para>
/// </summary>
public partial class SchemeParameter : ObservableObject
{
    private readonly TestParameterDto _dto;

    /// <summary>抄表时间参数组的所有 Code 代码（父参数 + 子参数）</summary>
    private static readonly HashSet<string> ReadingTimeCodes = new()
    {
        "READING_TYPE", "READING_DAY", "READING_TIME_1", "READING_DATE_1",
        "READING_TIME_2", "READING_DATE_2", "READING_TIME_3", "READING_DATE_3",
        "READING_TIME_4", "READING_DATE_4", "READING_TIME_5", "READING_DATE_5"
    };

    /// <summary>抄表时间参数组的子参数 Code 代码（不显示勾选框和 MES 代码）</summary>
    private static readonly HashSet<string> ChildCodes = new()
    {
        "READING_DAY", "READING_TIME_1", "READING_DATE_1",
        "READING_TIME_2", "READING_DATE_2", "READING_TIME_3",
        "READING_DATE_3", "READING_TIME_4", "READING_DATE_4",
        "READING_TIME_5", "READING_DATE_5"
    };

    public SchemeParameter(TestParameterDto dto)
    {
        _dto = dto;
        HasLimits = dto.Parameters.Any(p => p.Code == SubParameterCodes.LowerLimit || p.Code == SubParameterCodes.UpperLimit);
        IsChildParameter = ChildCodes.Contains(dto.Code);
        IsReadingTimeGroup = ReadingTimeCodes.Contains(dto.Code);
        IsMergedParent = MergedParameterMap.IsParent(dto.Code);
    }

    /// <summary>参数唯一识别码（系统内部使用）</summary>
    public string Code => _dto.Code;

    /// <summary>MES 上传代码（用户配置，用于 MES 系统上传）</summary>
    public string MesCode => _dto.MesCode;

    /// <summary>是否有上下限值（决定是否显示下限/上限）</summary>
    public bool HasLimits { get; }

    /// <summary>是否为子参数（子参数不显示勾选框和 MES 代码）</summary>
    public bool IsChildParameter { get; }

    /// <summary>是否属于抄表时间参数组</summary>
    public bool IsReadingTimeGroup { get; }

    /// <summary>是否为合并参数的父参数（如月冻结、域名端口等，包含多个子参数）</summary>
    public bool IsMergedParent { get; }

    /// <summary>子参数列表（仅 IsMergedParent=true 时有效，子参数内联展示在父参数卡片内）</summary>
    public ObservableCollection<SchemeParameter> SubParameters { get; } = new();

    /// <summary>是否可见（控制参数项的显示/隐藏，由抄表类型联动控制）</summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// 判断指定 Code 是否为抄表时间参数组的子参数
    /// <para>抄表时间子参数（抄表日、抄表时1-5、抄表日1-5）不作为独立检测项目，
    /// 仅作为"抄表时间"父参数的内部参数存在</para>
    /// </summary>
    public static bool IsReadingTimeChild(string code) => ChildCodes.Contains(code);

    /// <summary>
    /// 根据抄表类型值计算指定参数的可见性
    /// <para>协议 4.1.4 抄表时间参数：</para>
    /// <para>0=每天, 1=单月, 2=双月, 3=每月, 4=按分, 5=按时, 9=按天 → 显示"抄表日"</para>
    /// <para>7=指定5时 → 显示5组"抄表时"，隐藏"抄表日"</para>
    /// <para>8=指定5日 → 显示5组"抄表日"，隐藏"抄表日"</para>
    /// </summary>
    /// <param name="code">参数的唯一识别码</param>
    /// <param name="readingTypeValue">抄表类型值（READING_TYPE 参数的 Value）</param>
    /// <returns>该参数在当前抄表类型下是否应该显示</returns>
    public static bool IsVisibleForReadingType(string code, string? readingTypeValue)
    {
        // 非抄表时间子参数始终可见
        if (!ChildCodes.Contains(code))
            return true;

        // 抄表类型父参数始终可见
        if (code == "READING_TYPE")
            return true;

        // 子参数根据抄表类型值决定可见性
        return readingTypeValue switch
        {
            "7" => code.StartsWith("READING_TIME_"),  // 指定5时：显示5组"抄表时"
            "8" => code.StartsWith("READING_DATE_"),  // 指定5日：显示5组"抄表日"
            _ => code == "READING_DAY"                // 其他类型：显示"抄表日"
        };
    }

    // ===== 代理 TestParameterDto 属性 =====

    /// <summary>检测项目显示名称（如"抄表时间"）</summary>
    public string Name => _dto.Name;

    /// <summary>项目排序序号（决定 UI 显示顺序与下发顺序）</summary>
    public int Order => _dto.Order;

    /// <summary>是否启用该项目（false 表示该检测项目不参与本次测试）</summary>
    public bool IsEnabled => _dto.IsEnabled;

    /// <summary>
    /// 主值（来自 Code=<see cref="SubParameterCodes.Value"/> 的子参数）
    /// <para>只读展示场景下直接读取已存在的子参数值；未找到时返回空字符串。</para>
    /// </summary>
    public string Value => _dto.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.Value)?.Value ?? string.Empty;

    /// <summary>
    /// 下限值（来自 Code=<see cref="SubParameterCodes.LowerLimit"/> 的子参数）
    /// <para>解析子参数 Value 字符串为 double；未配置或解析失败时返回 null。</para>
    /// </summary>
    public double? LowerLimit
    {
        get
        {
            var sub = _dto.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.LowerLimit);
            if (sub != null && double.TryParse(sub.Value, out var result))
                return result;
            return null;
        }
    }

    /// <summary>
    /// 上限值（来自 Code=<see cref="SubParameterCodes.UpperLimit"/> 的子参数）
    /// <para>解析子参数 Value 字符串为 double；未配置或解析失败时返回 null。</para>
    /// </summary>
    public double? UpperLimit
    {
        get
        {
            var sub = _dto.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.UpperLimit);
            if (sub != null && double.TryParse(sub.Value, out var result))
                return result;
            return null;
        }
    }

    /// <summary>
    /// 子参数控件类型（来自主值子参数 Code=<see cref="SubParameterCodes.Value"/> 的 <see cref="ParasContentDto.ControlType"/>）
    /// <para>指定该参数在 View 中应使用的编辑控件类型，默认 TextBox。</para>
    /// <para>只读展示场景下可用于驱动展示控件的样式选择。</para>
    /// </summary>
    public ParameterControlType ControlType => _dto.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.Value)?.ControlType ?? ParameterControlType.TextBox;

    /// <summary>
    /// 子参数默认值（来自主值子参数 Code=<see cref="SubParameterCodes.Value"/> 的 <see cref="ParasContentDto.DefaultValue"/>）
    /// <para>用于展示参数的初始默认值；主值子参数不存在时返回空字符串。</para>
    /// </summary>
    public string DefaultValue => _dto.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.Value)?.DefaultValue ?? string.Empty;

    /// <summary>
    /// 子参数值校验规则（来自主值子参数 Code=<see cref="SubParameterCodes.Value"/> 的 <see cref="ParasContentDto.Validator"/>）
    /// <para>为 null 表示不进行额外校验。</para>
    /// </summary>
    public ParameterValidator? Validator => _dto.Parameters.FirstOrDefault(p => p.Code == SubParameterCodes.Value)?.Validator;

    /// <summary>转换回 TestParameterDto</summary>
    public TestParameterDto ToDto() => _dto;
}
