using System.Globalization;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 执行方案参数的只读控件投影，复用目录中的选项、提示及宽度
/// 仅接收已通过报警解码的参数，不改动公共 DTO 或复制业务校验
/// </summary>
public sealed class AlarmHostPlanParameter
{
    /// <summary>
    /// 已格式化的参数显示值，普通文本及网络密码保留原值
    /// </summary>
    public string Value { get; private init; } = "";

    /// <summary>
    /// 与配置页相同的字段浮动提示，包含单位等显示信息
    /// </summary>
    public string Hint { get; private init; } = "";

    /// <summary>
    /// 复用目录中的下拉选项，不另维护执行页选项表
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte, string>> Options { get; private init; } = [];

    /// <summary>
    /// 方案已选择的下拉编码，其他类型为空
    /// </summary>
    public byte? SelectedValue { get; private init; }

    /// <summary>
    /// 当前参数是否按只读下拉框展示
    /// </summary>
    public bool IsChoice { get; private init; }

    /// <summary>
    /// 当前参数是否按只读复选框展示
    /// </summary>
    public bool IsBoolean { get; private init; }

    /// <summary>
    /// 布尔参数的勾选状态，其他类型为默认未勾选
    /// </summary>
    public bool IsChecked { get; private init; }

    /// <summary>
    /// 复选框固定显示其勾选含义，与配置页标签一致
    /// </summary>
    public string CheckedText { get; private init; } = "";

    /// <summary>
    /// 多参数项目的字段宽度，与配置输入共用同一份定义
    /// </summary>
    public double InputWidth { get; private init; }

    /// <summary>
    /// 将已解码参数映射到控件显示状态，不修改来源参数
    /// </summary>
    internal static AlarmHostPlanParameter FromParameter(ParasContentDto parameter,
        AlarmHostParameterDefinition definition)
    {
        var isChoice = definition.ControlType == ParameterControlType.ComboBox;
        var isBoolean = definition.ControlType == ParameterControlType.CheckBox;
        return new AlarmHostPlanParameter
        {
            Value = definition.FormatValue(parameter.Value),
            Hint = definition.InputHint,
            Options = definition.Options,
            SelectedValue = isChoice ? byte.Parse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture) : null,
            IsChoice = isChoice,
            IsBoolean = isBoolean,
            IsChecked = isBoolean && bool.Parse(parameter.Value),
            CheckedText = isBoolean ? definition.FormatValue("true") : "",
            InputWidth = definition.InputWidth
        };
    }
}
