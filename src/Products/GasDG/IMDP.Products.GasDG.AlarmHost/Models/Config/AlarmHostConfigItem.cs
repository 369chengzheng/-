using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 公共功能 DTO 的页面通知适配，不另存一套功能值
/// </summary>
public sealed class AlarmHostConfigItem : ObservableObject
{
    /// <summary>
    /// 本草稿独占的公共功能数据
    /// </summary>
    private readonly TestParameterDto _item;

    public AlarmHostConfigItem(AlarmHostFunctionDefinition definition)
    {
        _item = definition.CreateItem();
        Values = new System.Collections.ObjectModel.ReadOnlyDictionary<string, AlarmHostParameterValue>(
            _item.Parameters.ToDictionary(parameter => parameter.Code,
                parameter => new AlarmHostParameterValue(parameter, definition.GetParameter(parameter.Code)), StringComparer.Ordinal));
        foreach (var value in Values.Values)
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AlarmHostParameterValue.Value)) OnPropertyChanged(nameof(Values));
            };
    }

    /// <summary>
    /// 功能的稳定编码
    /// </summary>
    public string Code => _item.Code;

    /// <summary>
    /// 与保存及执行展示共用的功能名称
    /// </summary>
    public string Name => _item.Name;

    /// <summary>
    /// 字段总宽度超出普通组剩余空间时使用宽组，与只读预览同一计算
    /// </summary>
    public bool IsComplex => AlarmHostFunctionCatalog.Get(Code).IsComplex;

    /// <summary>
    /// 当前方案是否勾选此功能
    /// </summary>
    public bool IsEnabled
    {
        get => _item.IsEnabled;
        set => SetProperty(_item.IsEnabled, value, _item, static (item, enabled) => item.IsEnabled = enabled);
    }

    /// <summary>
    /// 当前功能的 MES 项目码，不在本阶段上传
    /// </summary>
    public string MesCode
    {
        get => _item.MesCode;
        set => SetProperty(_item.MesCode, value, _item, static (item, code) => item.MesCode = code);
    }

    /// <summary>
    /// 按稳定参数编码定位的输入值，不依赖集合位置
    /// </summary>
    public IReadOnlyDictionary<string, AlarmHostParameterValue> Values { get; }

    /// <summary>
    /// 采集独立副本，调用方修改 DTO 及其规则不会回写页面
    /// </summary>
    public TestParameterDto ToDto() => JsonSerializer.Deserialize<TestParameterDto>(JsonSerializer.Serialize(_item))!;
}

/// <summary>
/// 公共子参数的通知及控件类型转换，原始文本仍直接保存在 DTO.Value
/// </summary>
public sealed class AlarmHostParameterValue : ObservableObject
{
    /// <summary>
    /// 本草稿独占的子参数数据
    /// </summary>
    private readonly ParasContentDto _parameter;

    /// <summary>
    /// 当前参数的选项及显示定义，数值仍独立保存在本草稿的公共 DTO 中
    /// </summary>
    private readonly AlarmHostParameterDefinition _definition;

    internal AlarmHostParameterValue(ParasContentDto parameter, AlarmHostParameterDefinition definition)
    {
        _parameter = parameter;
        _definition = definition;
    }

    /// <summary>
    /// 参数目录生成的中文名称，供配置页标签使用
    /// </summary>
    public string Name => _parameter.Name;

    /// <summary>
    /// 该参数共用的下拉选项，无需在页面 VM 再维护选项属性
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte, string>> Options => _definition.Options;

    /// <summary>
    /// 普通参数模板是否使用下拉框
    /// </summary>
    public bool IsChoice => _parameter.ControlType == ParameterControlType.ComboBox;

    /// <summary>
    /// 普通参数模板是否使用复选框
    /// </summary>
    public bool IsBoolean => _parameter.ControlType == ParameterControlType.CheckBox;

    /// <summary>
    /// 编辑和执行预览共用的字段基准宽度
    /// </summary>
    public double InputWidth => _definition.InputWidth;

    /// <summary>
    /// 编辑和执行预览共用的浮动提示，包含显示单位
    /// </summary>
    public string InputHint => _definition.InputHint;

    /// <summary>
    /// 布尔控件勾选时的显示名称，与只读展示共用格式定义；其他参数为空
    /// </summary>
    public string CheckedText => _parameter.ControlType == ParameterControlType.CheckBox ? _definition.FormatValue("true") : "";

    /// <summary>
    /// 原始字符串输入，保留数字未完成输入及密码两端空格
    /// </summary>
    public string Value
    {
        get => _parameter.Value;
        set
        {
            if (_parameter.ControlType == ParameterControlType.ComboBox &&
                byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                value = number.ToString(CultureInfo.InvariantCulture);
            else if (_parameter.ControlType == ParameterControlType.CheckBox && bool.TryParse(value, out var flag))
                value = flag ? "true" : "false";
            if (!SetProperty(_parameter.Value, value, _parameter, static (parameter, text) => parameter.Value = text)) return;
            OnPropertyChanged(nameof(ByteValue));
            OnPropertyChanged(nameof(BooleanValue));
        }
    }

    /// <summary>
    /// 适配现有 byte 选项下拉框，空值表示未选择
    /// </summary>
    public byte? ByteValue
    {
        get => byte.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
        set => Value = value?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>
    /// 适配布尔复选框，序列化值沿用 true/false
    /// </summary>
    public bool BooleanValue
    {
        get => bool.TryParse(Value, out var result) && result;
        set => Value = value ? "true" : "false";
    }
}
