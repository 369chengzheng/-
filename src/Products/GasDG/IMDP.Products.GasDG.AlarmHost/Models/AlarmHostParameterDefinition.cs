using System.Globalization;
using System.Text.Json;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 参数的公共 DTO 模板及插件显示定义，供编辑、校验和只读展示共用
/// 选项、显示函数及控件尺寸只保留在代码中，不写入方案 JSON
/// </summary>
public sealed class AlarmHostParameterDefinition
{
    /// <summary>
    /// 创建草稿时复制的参数模板，包含名称、类型、默认值和输入规则
    /// </summary>
    private readonly ParasContentDto _template;

    /// <summary>
    /// 特殊参数的显示函数，未提供时使用选项名称或原始值
    /// </summary>
    private readonly Func<string, string>? _formatValue;

    public AlarmHostParameterDefinition(ParasContentDto template,
        IReadOnlyList<KeyValuePair<byte, string>>? options = null, Func<string, string>? formatValue = null,
        double inputWidth = 100, string? inputHint = null)
    {
        _template = template;
        Options = options ?? [];
        _formatValue = formatValue;
        InputWidth = inputWidth;
        InputHint = inputHint ?? template.Name;
    }

    /// <summary>
    /// 参数的稳定编码，用于查找对应定义
    /// </summary>
    public string Code => _template.Code;

    /// <summary>
    /// 编辑及只读预览共用的控件类型，不改变公共参数的保存方式
    /// </summary>
    public ParameterControlType ControlType => _template.ControlType;

    /// <summary>
    /// 多参数项目中单个字段的基准宽度，单位为设备无关像素
    /// </summary>
    public double InputWidth { get; }

    /// <summary>
    /// 编辑及只读控件共用的浮动提示，默认沿用参数名称
    /// </summary>
    public string InputHint { get; }

    /// <summary>
    /// 下拉控件、枚举校验和只读翻译共用的编码与名称
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte, string>> Options { get; }

    /// <summary>
    /// 创建独立的公共参数及校验规则，避免不同草稿共享可变值
    /// </summary>
    public ParasContentDto CreateParameter() =>
        JsonSerializer.Deserialize<ParasContentDto>(JsonSerializer.Serialize(_template))!;

    /// <summary>
    /// 按同一组选项判断枚举值，普通参数继续由 DTO 的输入规则检查
    /// </summary>
    public bool AcceptsOption(string value) => Options.Count == 0 ||
        byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var key) &&
        Options.Any(option => option.Key == key);

    /// <summary>
    /// 将已通过输入校验的值转换为显示文本，保留普通文本及其中空格
    /// </summary>
    public string FormatValue(string value)
    {
        if (_formatValue != null) return _formatValue(value);
        if (Options.Count == 0) return value;
        var key = byte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return Options.Single(option => option.Key == key).Value;
    }
}
