using System.Globalization;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// UGNB功能的配置与展示定义；本阶段不登记设备执行对象，不参与通信或判定
/// </summary>
public static partial class AlarmHostFunctionCatalog
{
    /// <summary>
    /// 表具底码的稳定功能编码
    /// </summary>
    public const string MeterBottom = "MeterBottom";

    /// <summary>
    /// 预留量的稳定功能编码
    /// </summary>
    public const string ReservedAmount = "ReservedAmount";

    /// <summary>
    /// 结算模式的稳定功能编码
    /// </summary>
    public const string SettlementMode = "SettlementMode";

    /// <summary>
    /// 结算功能的稳定功能编码
    /// </summary>
    public const string SettlementFunctions = "SettlementFunctions";

    /// <summary>
    /// 补偿方式的稳定功能编码
    /// </summary>
    public const string Compensation = "Compensation";

    /// <summary>
    /// 计量检定类型的稳定功能编码
    /// </summary>
    public const string MeteringVerification = "MeteringVerification";

    /// <summary>
    /// 脉冲检表分辨率的稳定功能编码
    /// </summary>
    public const string PulseResolution = "PulseResolution";

    /// <summary>
    /// 表具规格的稳定功能编码
    /// </summary>
    public const string MeterSpecification = "MeterSpecification";

    /// <summary>
    /// 月冻结日期的稳定功能编码
    /// </summary>
    public const string MonthlyFreeze = "MonthlyFreeze";

    /// <summary>
    /// 一级不通讯天数的稳定功能编码
    /// </summary>
    public const string NoCommunicationDays = "NoCommunicationDays";

    /// <summary>
    /// 一级不用气天数的稳定功能编码
    /// </summary>
    public const string NoGasDays = "NoGasDays";

    /// <summary>
    /// 二级不用气天数的稳定功能编码
    /// </summary>
    public const string SecondNoGasDays = "SecondNoGasDays";

    /// <summary>
    /// 大流阈值的稳定功能编码
    /// </summary>
    public const string HighFlowThreshold = "HighFlowThreshold";

    /// <summary>
    /// 小流阈值的稳定功能编码
    /// </summary>
    public const string LowFlowThreshold = "LowFlowThreshold";

    /// <summary>
    /// 少额关阀阈值的稳定功能编码
    /// </summary>
    public const string LowBalanceThreshold = "LowBalanceThreshold";

    /// <summary>
    /// 温度上限阈值的稳定功能编码
    /// </summary>
    public const string UpperTemperature = "UpperTemperature";

    /// <summary>
    /// 温度下限阈值的稳定功能编码
    /// </summary>
    public const string LowerTemperature = "LowerTemperature";

    /// <summary>
    /// 压力上限阈值的稳定功能编码
    /// </summary>
    public const string UpperPressure = "UpperPressure";

    /// <summary>
    /// 压力下限阈值的稳定功能编码
    /// </summary>
    public const string LowerPressure = "LowerPressure";

    /// <summary>
    /// 异常恒流阈值的稳定功能编码
    /// </summary>
    public const string ConstantFlowThreshold = "ConstantFlowThreshold";

    /// <summary>
    /// 拆表阀控的稳定功能编码
    /// </summary>
    public const string TamperValve = "TamperValve";

    /// <summary>
    /// 119报警阀控的稳定功能编码
    /// </summary>
    public const string Alarm119Valve = "Alarm119Valve";

    /// <summary>
    /// 大流阀控的稳定功能编码
    /// </summary>
    public const string HighFlowValve = "HighFlowValve";

    /// <summary>
    /// 小流阀控的稳定功能编码
    /// </summary>
    public const string LowFlowValve = "LowFlowValve";

    /// <summary>
    /// 异常恒流阀控的稳定功能编码
    /// </summary>
    public const string ConstantFlowValve = "ConstantFlowValve";

    /// <summary>
    /// 温度上限阀控的稳定功能编码
    /// </summary>
    public const string UpperTemperatureValve = "UpperTemperatureValve";

    /// <summary>
    /// 温度下限阀控的稳定功能编码
    /// </summary>
    public const string LowerTemperatureValve = "LowerTemperatureValve";

    /// <summary>
    /// 压力上限阀控的稳定功能编码
    /// </summary>
    public const string UpperPressureValve = "UpperPressureValve";

    /// <summary>
    /// 压力下限阀控的稳定功能编码
    /// </summary>
    public const string LowerPressureValve = "LowerPressureValve";

    /// <summary>
    /// 少额关阀阀控的稳定功能编码
    /// </summary>
    public const string LowBalanceValve = "LowBalanceValve";

    /// <summary>
    /// 一级不通讯阀控的稳定功能编码
    /// </summary>
    public const string NoCommunicationValve = "NoCommunicationValve";

    /// <summary>
    /// 一级不用气阀控的稳定功能编码
    /// </summary>
    public const string NoGasValve = "NoGasValve";

    /// <summary>
    /// 拆盖阀控的稳定功能编码
    /// </summary>
    public const string CoverValve = "CoverValve";

    /// <summary>
    /// 电池仓开盖阀控的稳定功能编码
    /// </summary>
    public const string BatteryCoverValve = "BatteryCoverValve";

    /// <summary>
    /// 基表号读取的稳定功能编码
    /// </summary>
    public const string BaseMeterNumber = "BaseMeterNumber";

    /// <summary>
    /// 阀门状态的稳定功能编码
    /// </summary>
    public const string ValveStatus = "ValveStatus";

    /// <summary>
    /// 通讯模组状态的稳定功能编码
    /// </summary>
    public const string ModemStatus = "ModemStatus";

    /// <summary>
    /// 主电电压检测的稳定功能编码
    /// </summary>
    public const string MainVoltage = "MainVoltage";

    /// <summary>
    /// 计量电压的稳定功能编码
    /// </summary>
    public const string MeteringVoltage = "MeteringVoltage";

    /// <summary>
    /// 外部压力的稳定功能编码
    /// </summary>
    public const string ExternalPressure = "ExternalPressure";

    /// <summary>
    /// 计量版本的稳定功能编码
    /// </summary>
    public const string MeteringVersion = "MeteringVersion";

    /// <summary>
    /// 网络类型的稳定功能编码
    /// </summary>
    public const string NetworkOperator = "NetworkOperator";

    /// <summary>
    /// 通讯板自检的稳定功能编码
    /// </summary>
    public const string CommunicationSelfCheck = "CommunicationSelfCheck";

    /// <summary>
    /// 只登记配置所需字段；后续接入执行时再提供对象号与具体读写判定
    /// </summary>
    private static IEnumerable<AlarmHostFunctionDefinition> CreateAdditionalItems() =>
    [
        new(MeterBottom, "表具底码", [DecimalInput(SubParameterCodes.Value, "表具底码", 0, 4294967295.9999, 4)]),
        new(ReservedAmount, "预留量", [DecimalInput(SubParameterCodes.Value, "预留量", 0, 42949672.95, 2)]),
        new(SettlementMode, "结算模式", [DefaultChoice(SubParameterCodes.Value, "结算模式", Options("表具后付费", "表具预付费", "后台预付费"))]),
        new(SettlementFunctions, "结算功能",
            [Flag("Purge", "启用排空量"), Flag("ShowSettlement", "显示结算信息"), Flag("VirtualBilling", "启用虚拟计费")]),
        new(Compensation, "补偿方式", [DefaultChoice(SubParameterCodes.Value, "补偿方式", [new(0, "不补偿"), new(3, "温压补偿")])]),
        new(MeteringVerification, "计量检定类型",
            [DefaultChoice(SubParameterCodes.Value, "计量检定类型", Options("工况计量", "标况温度计量", "标况压力计量", "标况温压计量"))]),
        new(PulseResolution, "脉冲检表分辨率", [DefaultChoice(SubParameterCodes.Value, "分辨率", Options("原威星", "新国标"))]),
        new(MeterSpecification, "表具规格",
        [
            DefaultChoice("Type", "表具类型", [new(0x10, "皮膜燃气表"), new(0x20, "流量计"), new(0x30, "超声波"), new(0x40, "控制器")]),
            DefaultChoice("Specification", "表具规格", Options("G1.6", "G2.5", "G4", "G4+", "G6", "G10", "G16", "G25", "G40"))
        ]),
        new(MonthlyFreeze, "月冻结日期",
            // C5F0附录A 6.3：日为0表示天冻结，1至28表示月冻结。
            [DecimalInput("Day", "日", 0, 28, 0, "0", 56), DecimalInput("Hour", "时", 0, 23, 0, "0", 56), DecimalInput("Minute", "分", 0, 59, 0, "0", 56)]),
        new(NoCommunicationDays, "一级不通讯天数", [DecimalInput(SubParameterCodes.Value, "天数", 0, 255, 0, "30")]),
        new(NoGasDays, "一级不用气天数", [DecimalInput(SubParameterCodes.Value, "天数", 0, 255, 0)]),
        new(SecondNoGasDays, "二级不用气天数", [DecimalInput(SubParameterCodes.Value, "天数", 0, 255, 0)]),
        new(HighFlowThreshold, "大流阈值", [DecimalInput(SubParameterCodes.Value, "大流阈值(m³)", 0, 655.35, 2)]),
        new(LowFlowThreshold, "小流阈值", [DecimalInput(SubParameterCodes.Value, "小流阈值(L)", 0, 65535, 0)]),
        new(LowBalanceThreshold, "少额关阀阈值", [DecimalInput(SubParameterCodes.Value, "少额关阀阈值", 0, 42949672.95, 2)]),
        new(UpperTemperature, "温度上限阈值", [DecimalInput(SubParameterCodes.Value, "温度上限(℃)", -327.68, 655.35, 2)]),
        new(LowerTemperature, "温度下限阈值", [DecimalInput(SubParameterCodes.Value, "温度下限(℃)", -327.68, 327.67, 2)]),
        new(UpperPressure, "压力上限阈值", [DecimalInput(SubParameterCodes.Value, "压力上限(kPa)", 0, 6553.5, 1)]),
        new(LowerPressure, "压力下限阈值", [DecimalInput(SubParameterCodes.Value, "压力下限(kPa)", 0, 6553.5, 1)]),
        new(ConstantFlowThreshold, "异常恒流阈值",
        [
            DecimalInput("Lower", "最小值(m³/h)", 0, 65.535, 3),
            DecimalInput("Upper", "最大值(m³/h)", 0, 65.535, 3),
            DecimalInput("Duration", "持续时间(分)", 0, 65535, 0)
        ]),
        Valve(TamperValve, "拆表阀控"), Valve(Alarm119Valve, "119报警阀控"),
        Valve(HighFlowValve, "大流阀控"), Valve(LowFlowValve, "小流阀控"),
        Valve(ConstantFlowValve, "异常恒流阀控"),
        Valve(UpperTemperatureValve, "温度上限阀控"), Valve(LowerTemperatureValve, "温度下限阀控"),
        Valve(UpperPressureValve, "压力上限阀控"), Valve(LowerPressureValve, "压力下限阀控"),
        Valve(LowBalanceValve, "少额关阀阀控"),
        Valve(NoCommunicationValve, "一级不通讯阀控"), Valve(NoGasValve, "一级不用气阀控"),
        Valve(CoverValve, "拆盖阀控"), Valve(BatteryCoverValve, "电池仓开盖阀控"),
        new(ValveStatus, "阀门状态",
            [DefaultChoice(SubParameterCodes.Value, "目标状态", Options("无", "开阀", "开阀中", "关阀", "关阀中", "异常", "阀门强开", "阀门强关"))]),
        new(MainVoltage, "主电电压检测", RequiredRange(0, 25.5, "V")),
        new(MeteringVoltage, "计量电压", RequiredRange(0, 25.5, "V")),
        new(ExternalPressure, "外部压力", [DecimalInput("Tolerance", "允许偏差(kPa)", 0, 6553.5, 1)]),
        new(MeteringVersion, "计量版本", [OptionalText(SubParameterCodes.Value, "目标版本")]),
        new(NetworkOperator, "网络类型", [DefaultChoice(SubParameterCodes.Value, "目标运营商", Options("移动", "联通", "电信"))]),
        new(CommunicationSelfCheck, "通讯板自检",
            [DefaultChoice(SubParameterCodes.Value, "自检类型", [new(0x81, "通讯板完整自检"), new(0x82, "只进行通讯测试"), new(0x83, "无NB-IOT的通讯测试")], 180)]),
        new(BaseMeterNumber, "基表号读取"),
        new(ModemStatus, "通讯模组状态")
    ];

    /// <summary>
    /// 共用阀控选项及更改字段，勾选和字段值独立保存，当前尚不生成阀控帧
    /// </summary>
    private static AlarmHostFunctionDefinition Valve(string code, string name) => new(code, name,
        [Flag("Change", "更改", true, 64), DefaultChoice(SubParameterCodes.Value, "阀控方式", Options("禁用", "普通关阀", "权限关阀", "不关阀"), 100)]);

    /// <summary>
    /// 按旧配置展示顺序创建从零开始的选项，不另维护校验枚举
    /// </summary>
    private static IReadOnlyList<KeyValuePair<byte, string>> Options(params string[] labels) =>
        labels.Select((label, index) => new KeyValuePair<byte, string>((byte)index, label)).ToArray();

    /// <summary>
    /// 新项目选项使用首项作默认值，未勾选功能不参与保存及校验
    /// </summary>
    private static AlarmHostParameterDefinition DefaultChoice(string code, string name,
        IReadOnlyList<KeyValuePair<byte, string>> options, double width = 160) => new(new ParasContentDto
        {
            Code = code, Name = name, Value = options[0].Key.ToString(CultureInfo.InvariantCulture),
            DefaultValue = options[0].Key.ToString(CultureInfo.InvariantCulture), ControlType = ParameterControlType.ComboBox,
            Validator = new() { IsRequired = true, ErrorMessage = $"请选择有效的{name}。" }
        }, options, inputWidth: width);

    /// <summary>
    /// 普通十进制输入只检查保存形态、精度和范围，不进行设备字节编码
    /// </summary>
    private static AlarmHostParameterDefinition DecimalInput(string code, string name, double minimum, double maximum,
        int precision, string initial = "0", double width = 100, bool required = true) => new(new ParasContentDto
        {
            Code = code, Name = code == SubParameterCodes.Value ? SubParameterCodes.ValueDisplayName : name,
            Value = initial, DefaultValue = initial, ControlType = ParameterControlType.NumericUpDown,
            Validator = new()
            {
                MinValue = minimum, MaxValue = maximum, IsRequired = required,
                RegexPattern = precision == 0 ? "^-?[0-9]+$" : @"^-?[0-9]+(?:\.[0-9]{1," + precision + "})?$",
                ErrorMessage = $"{name}应为{minimum.ToString(CultureInfo.InvariantCulture)}–{maximum.ToString(CultureInfo.InvariantCulture)}内的{(precision == 0 ? "整数" : $"数值（最多{precision}位小数）")}。"
            }
        }, inputWidth: width, inputHint: name);

    /// <summary>
    /// 已有只读CSQ项目的可选目标，空值继续沿用原读取行为
    /// </summary>
    private static IReadOnlyList<AlarmHostParameterDefinition> OptionalRange(double minimum, double maximum, string unit) =>
    [
        DecimalInput("Lower", "下限" + unit, minimum, maximum, 0, "", 80, false),
        DecimalInput("Upper", "上限" + unit, minimum, maximum, 0, "", 80, false)
    ];

    /// <summary>
    /// 范围项目的上下限均可维护，顺序关系由共用输入校验核对
    /// </summary>
    private static IReadOnlyList<AlarmHostParameterDefinition> RequiredRange(double minimum, double maximum, string unit) =>
    [
        DecimalInput("Lower", "下限(" + unit + ")", minimum, maximum, 1, "0", 80),
        DecimalInput("Upper", "上限(" + unit + ")", minimum, maximum, 1, "0", 80)
    ];

    /// <summary>
    /// 版本等纯文本目标允许留空；原始值与JSON保持一致
    /// </summary>
    private static AlarmHostParameterDefinition OptionalText(string code, string name) => new(new ParasContentDto
    {
        Code = code, Name = name, Value = "", DefaultValue = "",
        Validator = new() { MaxLength = 100, ErrorMessage = $"{name}不能超过100个字符。" }
    }, inputWidth: 128);

    /// <summary>
    /// 多选组合中的一个布尔参数，编辑与预览使用同一标签
    /// </summary>
    private static AlarmHostParameterDefinition Flag(string code, string name, bool initial = false, double width = 120) => new(new ParasContentDto
    {
        Code = code, Name = name, Value = initial ? "true" : "false", DefaultValue = initial ? "true" : "false",
        ControlType = ParameterControlType.CheckBox,
        Validator = new() { IsRequired = true, RegexPattern = "^(true|false)$", ErrorMessage = $"{name}应为有效勾选值。" }
    }, formatValue: value => bool.Parse(value) ? name : "不" + name, inputWidth: width, inputHint: name);
}
