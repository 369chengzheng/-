using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IMDP.Application.DTOs;
using IMDP.Products.GasDG.WholeA.Protocol;

namespace IMDP.Products.GasDG.WholeA.ViewModels;

/// <summary>
/// 合并参数映射表：子参数 Code → 父参数 Code
/// <para>用于在加载方案时从扁平 DTO 列表重建父子关系</para>
/// <para>Code 是参数唯一识别码（系统内部使用），与 MES 上传码无关</para>
/// </summary>
public static class MergedParameterMap
{
    /// <summary>合并参数的父参数 Code 集合</summary>
    public static readonly HashSet<string> ParentMesCodes = new()
    {
        "FREEZE_DATETIME",   // 月冻结
        "DOMAIN_ADDR",       // 域名端口
        "IP_ADDR_PORT",      // IP地址端口
        "NET_AUTH",          // 网络参数
        "READING_TYPE"       // 抄表时间
    };

    /// <summary>合并参数的子参数 Code 集合</summary>
    public static readonly HashSet<string> ChildMesCodes = new()
    {
        "FREEZE_DAY", "FREEZE_HOUR", "FREEZE_MINUTE",
        "DOMAIN", "DOMAIN_PORT",
        "IP_ADDR", "IP_PORT",
        "NET_USER", "NET_PWD",
        "READING_DAY",
        "READING_TIME_1", "READING_DATE_1",
        "READING_TIME_2", "READING_DATE_2",
        "READING_TIME_3", "READING_DATE_3",
        "READING_TIME_4", "READING_DATE_4",
        "READING_TIME_5", "READING_DATE_5"
    };

    /// <summary>子参数 Code → 父参数 Code 的映射</summary>
    public static readonly Dictionary<string, string> ChildToParent = new()
    {
        ["FREEZE_DAY"] = "FREEZE_DATETIME",
        ["FREEZE_HOUR"] = "FREEZE_DATETIME",
        ["FREEZE_MINUTE"] = "FREEZE_DATETIME",
        ["DOMAIN"] = "DOMAIN_ADDR",
        ["DOMAIN_PORT"] = "DOMAIN_ADDR",
        ["IP_ADDR"] = "IP_ADDR_PORT",
        ["IP_PORT"] = "IP_ADDR_PORT",
        ["NET_USER"] = "NET_AUTH",
        ["NET_PWD"] = "NET_AUTH",
        ["READING_DAY"] = "READING_TYPE",
        ["READING_TIME_1"] = "READING_TYPE",
        ["READING_DATE_1"] = "READING_TYPE",
        ["READING_TIME_2"] = "READING_TYPE",
        ["READING_DATE_2"] = "READING_TYPE",
        ["READING_TIME_3"] = "READING_TYPE",
        ["READING_DATE_3"] = "READING_TYPE",
        ["READING_TIME_4"] = "READING_TYPE",
        ["READING_DATE_4"] = "READING_TYPE",
        ["READING_TIME_5"] = "READING_TYPE",
        ["READING_DATE_5"] = "READING_TYPE"
    };

    /// <summary>判断指定 Code 是否为合并参数的子参数</summary>
    public static bool IsChild(string code) => ChildMesCodes.Contains(code);

    /// <summary>判断指定 Code 是否为合并参数的父参数</summary>
    public static bool IsParent(string code) => ParentMesCodes.Contains(code);
}

/// <summary>
/// 配置参数包装 ViewModel
/// <para>包装 TestParameterDto，附加 UI 编辑器所需的属性：
/// IsEnum（是否枚举）、Options（枚举选项）、Description（协议说明）、HasLimits（是否有上下限）</para>
/// </summary>
public partial class ConfigParameter : ObservableObject
{
    private readonly TestParameterDto _dto;

    public ConfigParameter(TestParameterDto dto)
    {
        _dto = dto;
        IsEnum = ProtocolEnumOptions.IsEnumParameter(dto.Code);
        Options = ProtocolEnumOptions.GetOptions(dto.Code);
        Description = ProtocolEnumOptions.GetDescription(dto.Code);
        HasLimits = dto.Parameters.Any(p => p.Code == SubParameterCodes.LowerLimit || p.Code == SubParameterCodes.UpperLimit);
    }

    /// <summary>参数值变化事件，用于 ViewModel 监听联动逻辑</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>参数唯一识别码（系统内部使用，不可修改）</summary>
    public string Code => _dto.Code;

    /// <summary>MES 上传代码（用户可在方案配置页手动配置）</summary>
    public string MesCode
    {
        get => _dto.MesCode;
        set
        {
            if (_dto.MesCode != value)
            {
                _dto.MesCode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>是否为枚举类型参数（使用 ComboBox 下拉选择）</summary>
    public bool IsEnum { get; }

    /// <summary>枚举选项列表（仅 IsEnum=true 时有效）</summary>
    public List<ProtocolEnumOptions.EnumOption>? Options { get; }

    /// <summary>协议定义的参数说明（用于 Tooltip 提示）</summary>
    public string Description { get; }

    /// <summary>是否有上下限值（决定是否显示下限/上限输入框）</summary>
    public bool HasLimits { get; }

    /// <summary>是否为子参数（子参数不显示勾选框）</summary>
    public bool IsChildParameter { get; set; }

    /// <summary>是否为合并参数的父参数（如月冻结、域名端口等，包含多个子参数）</summary>
    public bool IsMergedParent { get; set; }

    /// <summary>子参数列表（仅 IsMergedParent=true 时有效，子参数内联展示在父参数卡片内）</summary>
    public ObservableCollection<ConfigParameter> SubParameters { get; } = new();

    /// <summary>是否可见（控制参数项的显示/隐藏，与 IsEnabled 独立）</summary>
    [ObservableProperty]
    private bool _isVisible = true;

    // ===== 代理 TestParameterDto 属性 =====

    public string Name => _dto.Name;

    public int Order => _dto.Order;

    public bool IsEnabled
    {
        get => _dto.IsEnabled;
        set
        {
            if (_dto.IsEnabled != value)
            {
                _dto.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 查找指定 Code 的子参数
    /// <para>在 _dto.Parameters 中按 <see cref="ParasContentDto.Code"/> 精确匹配。</para>
    /// </summary>
    /// <param name="code">子参数 Code（建议使用 <see cref="SubParameterCodes"/> 常量）</param>
    /// <returns>匹配到的子参数；未找到返回 null</returns>
    private ParasContentDto? FindSub(string code) => _dto.Parameters.FirstOrDefault(p => p.Code == code);

    /// <summary>
    /// 主值（来自 Code=<see cref="SubParameterCodes.Value"/> 的子参数）
    /// <para>get 时若不存在则返回空字符串；set 时若不存在则新建子参数并触发 <see cref="ValueChanged"/> 事件。</para>
    /// </summary>
    public string Value
    {
        get => FindSub(SubParameterCodes.Value)?.Value ?? string.Empty;
        set
        {
            var sub = FindSub(SubParameterCodes.Value);
            if (sub != null)
            {
                if (sub.Value != value)
                {
                    sub.Value = value;
                    OnPropertyChanged();
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs(Code, value));
                }
            }
            else
            {
                _dto.Parameters.Add(new ParasContentDto
                {
                    Name = SubParameterCodes.ValueDisplayName,
                    Code = SubParameterCodes.Value,
                    Value = value,
                    Order = SubParameterCodes.ValueOrder
                });
                OnPropertyChanged();
                ValueChanged?.Invoke(this, new ValueChangedEventArgs(Code, value));
            }
        }
    }

    /// <summary>
    /// 下限值（来自 Code=<see cref="SubParameterCodes.LowerLimit"/> 的子参数）
    /// <para>get 时将子参数 Value 字符串解析为 double；解析失败或不存在返回 null。</para>
    /// <para>set 时若子参数不存在则新建，Order 取 <see cref="SubParameterCodes.LowerLimitOrder"/> 常量，
    /// 与 <see cref="Value"/> 属性及 <see cref="WholeConfigViewModel.P"/> 方法保持一致的标准处理流程。</para>
    /// </summary>
    public double? LowerLimit
    {
        get
        {
            var sub = FindSub(SubParameterCodes.LowerLimit);
            if (sub != null && double.TryParse(sub.Value, out var result))
                return result;
            return null;
        }
        set
        {
            var sub = FindSub(SubParameterCodes.LowerLimit);
            var strVal = value?.ToString() ?? "";
            if (sub != null)
            {
                if (sub.Value != strVal)
                {
                    sub.Value = strVal;
                    OnPropertyChanged();
                }
            }
            else
            {
                _dto.Parameters.Add(new ParasContentDto
                {
                    Name = SubParameterCodes.LowerLimitDisplayName,
                    Code = SubParameterCodes.LowerLimit,
                    Value = strVal,
                    Order = SubParameterCodes.LowerLimitOrder
                });
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 上限值（来自 Code=<see cref="SubParameterCodes.UpperLimit"/> 的子参数）
    /// <para>get 时将子参数 Value 字符串解析为 double；解析失败或不存在返回 null。</para>
    /// <para>set 时若子参数不存在则新建，Order 取 <see cref="SubParameterCodes.UpperLimitOrder"/> 常量，
    /// 与 <see cref="Value"/> 属性及 <see cref="WholeConfigViewModel.P"/> 方法保持一致的标准处理流程。</para>
    /// </summary>
    public double? UpperLimit
    {
        get
        {
            var sub = FindSub(SubParameterCodes.UpperLimit);
            if (sub != null && double.TryParse(sub.Value, out var result))
                return result;
            return null;
        }
        set
        {
            var sub = FindSub(SubParameterCodes.UpperLimit);
            var strVal = value?.ToString() ?? "";
            if (sub != null)
            {
                if (sub.Value != strVal)
                {
                    sub.Value = strVal;
                    OnPropertyChanged();
                }
            }
            else
            {
                _dto.Parameters.Add(new ParasContentDto
                {
                    Name = SubParameterCodes.UpperLimitDisplayName,
                    Code = SubParameterCodes.UpperLimit,
                    Value = strVal,
                    Order = SubParameterCodes.UpperLimitOrder
                });
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 子参数控件类型（来自主值子参数 Code=<see cref="SubParameterCodes.Value"/> 的 <see cref="ParasContentDto.ControlType"/>）
    /// <para>指定该参数在 View 中应使用的编辑控件类型，默认 TextBox。</para>
    /// <para>View 层可绑定此属性动态选择控件，与 <see cref="IsEnum"/> 互补：</para>
    /// <para>- <see cref="IsEnum"/> 基于 <see cref="ProtocolEnumOptions"/> 判断，仅区分"枚举/非枚举"；</para>
    /// <para>- <see cref="ControlType"/> 提供更细粒度的控件选择（TextBox/ComboBox/CheckBox/NumericUpDown）。</para>
    /// </summary>
    public ParameterControlType ControlType => FindSub(SubParameterCodes.Value)?.ControlType ?? ParameterControlType.TextBox;

    /// <summary>
    /// 子参数默认值（来自主值子参数 Code=<see cref="SubParameterCodes.Value"/> 的 <see cref="ParasContentDto.DefaultValue"/>）
    /// <para>用于重置参数时恢复初始值；主值子参数不存在时返回空字符串。</para>
    /// </summary>
    public string DefaultValue => FindSub(SubParameterCodes.Value)?.DefaultValue ?? string.Empty;

    /// <summary>
    /// 子参数值校验规则（来自主值子参数 Code=<see cref="SubParameterCodes.Value"/> 的 <see cref="ParasContentDto.Validator"/>）
    /// <para>为 null 表示不进行额外校验（仅依赖 <see cref="LowerLimit"/>/<see cref="UpperLimit"/> 的范围约束）。</para>
    /// </summary>
    public ParameterValidator? Validator => FindSub(SubParameterCodes.Value)?.Validator;

    /// <summary>
    /// 最大输入长度（来自 <see cref="Validator"/>.<see cref="ParameterValidator.MaxLength"/>）
    /// <para>用于 View 层绑定 TextBox.MaxLength 限制用户输入长度。</para>
    /// <para>返回 0 表示不限制（WPF TextBox.MaxLength 默认 0 即不限制）。</para>
    /// </summary>
    public int MaxLength => Validator?.MaxLength ?? 0;

    /// <summary>
    /// 校验提示文本（用于 View 层 ToolTip 显示）
    /// <para>聚合 <see cref="Description"/>（协议说明）与 <see cref="Validator"/>（校验规则）信息，
    /// 便于用户在 UI 悬停时查看参数约束。</para>
    /// <para>为空字符串表示无任何提示信息。</para>
    /// </summary>
    public string ValidationToolTip => BuildValidationToolTip();

    /// <summary>
    /// 构建校验提示文本
    /// <para>聚合顺序：协议说明 → 取值范围 → 最大长度 → 正则约束 → 错误信息。</para>
    /// <para>每项以换行符分隔，空值跳过。</para>
    /// </summary>
    /// <returns>聚合后的多行提示文本；无任何信息时返回空字符串。</returns>
    private string BuildValidationToolTip()
    {
        var parts = new List<string>();

        // 协议说明（如"0=每天, 1=单月..."等枚举说明，或字段业务含义）
        if (!string.IsNullOrEmpty(Description))
            parts.Add(Description);

        // 校验规则信息
        if (Validator != null)
        {
            // 取值范围（优先显示"最小~最大"完整区间，其次单独显示最小/最大）
            if (Validator.MinValue.HasValue && Validator.MaxValue.HasValue)
                parts.Add($"取值范围: {Validator.MinValue}~{Validator.MaxValue}");
            else if (Validator.MinValue.HasValue)
                parts.Add($"最小值: {Validator.MinValue}");
            else if (Validator.MaxValue.HasValue)
                parts.Add($"最大值: {Validator.MaxValue}");

            // 最大长度约束
            if (Validator.MaxLength.HasValue)
                parts.Add($"最大长度: {Validator.MaxLength}");

            // 正则格式约束（不直接显示正则本身，避免暴露技术细节）
            if (!string.IsNullOrEmpty(Validator.RegexPattern))
                parts.Add("格式约束: 正则校验");

            // 自定义错误信息
            if (!string.IsNullOrEmpty(Validator.ErrorMessage))
                parts.Add(Validator.ErrorMessage);
        }

        return string.Join("\n", parts);
    }

    /// <summary>转换回 TestParameterDto（用于持久化）</summary>
    public TestParameterDto ToDto() => _dto;
}

/// <summary>参数值变化事件参数</summary>
public class ValueChangedEventArgs : EventArgs
{
    public string Code { get; }
    public string NewValue { get; }

    public ValueChangedEventArgs(string code, string newValue)
    {
        Code = code;
        NewValue = newValue;
    }
}
