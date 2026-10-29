using System.Globalization;
using System.Text.RegularExpressions;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 报警功能的编码、名称及公共 DTO 参数默认值；设置和检验共用功能目录
/// </summary>
/// <param name="Key">持久化后保持稳定的功能编码</param>
/// <param name="DisplayName">页面显示名称</param>
/// <param name="Parameters">编辑、校验和展示共用的子参数定义，未提供时表示无子参数</param>
/// <param name="ObjectId">设备执行使用的C5F0对象编号；尚未接入执行的项目为空</param>
/// <param name="CanWrite">设备协议是否提供写指令，与设置或检验页面类型分开</param>
/// <param name="ShowParameters">是否展示子参数；隐藏时仍保留原有方案参数，供旧方案回填</param>
public sealed record AlarmHostFunctionDefinition(string Key, string DisplayName,
    IReadOnlyList<AlarmHostParameterDefinition>? Parameters = null, ushort? ObjectId = null, bool CanWrite = false,
    bool ShowParameters = true)
{
    /// <summary>当前页面是否需要展示参数控件，方案数据仍完整保留。</summary>
    public bool HasVisibleParameters => ShowParameters && Parameters is { Count: > 0 };

    /// <summary>
    /// 字段总宽度超出普通组空间时使用宽组，配置与预览共用判断
    /// </summary>
    public bool IsComplex => HasVisibleParameters && Parameters!.Sum(parameter => parameter.InputWidth + 12) > 194;

    /// <summary>
    /// 创建独立且默认未勾选的功能 DTO，子参数不与其他草稿共享
    /// </summary>
    public TestParameterDto CreateItem()
    {
        var parameters = Parameters?.Select(parameter => parameter.CreateParameter()).ToList() ?? [];
        for (var i = 0; i < parameters.Count; i++) parameters[i].Order = i + 1;
        return new TestParameterDto { Code = Key, Name = DisplayName, Parameters = parameters };
    }

    /// <summary>
    /// 按稳定编码取得当前功能的子参数定义
    /// </summary>
    public AlarmHostParameterDefinition GetParameter(string code) =>
        Parameters!.Single(parameter => parameter.Code == code);
}

/// <summary>
/// 本插件的功能目录及输入规则，复用公共参数 DTO 和页面输入模板
/// 编码用于编辑、采集及回填定位，不是设备协议对象号或 MES 码
/// </summary>
public static class AlarmHostFunctionCatalog
{
    /// <summary>
    /// 配置输入及执行页展示共用的生产状态选项
    /// </summary>
    public static IReadOnlyList<KeyValuePair<byte, string>> ProductionStatusOptions { get; } =
        Array.AsReadOnly(new[] { new KeyValuePair<byte, string>(0, "工厂态（生产态）"), new(1, "出厂态（用户态）") });

    /// <summary>
    /// 配置输入及执行页展示共用的服务器地址类型
    /// </summary>
    public static IReadOnlyList<KeyValuePair<byte, string>> AddressTypeOptions { get; } =
        Array.AsReadOnly(new[] { new KeyValuePair<byte, string>(0, "IP"), new(1, "域名") });

    /// <summary>
    /// 配置输入及执行页展示共用的主电上报类型
    /// </summary>
    public static IReadOnlyList<KeyValuePair<byte, string>> ReportTypeOptions { get; } = Array.AsReadOnly(new[]
    {
        new KeyValuePair<byte, string>(0, "每天"), new(1, "单月"), new(2, "双月"), new(3, "每月"),
        new(4, "按分"), new(5, "按时"), new(7, "指定五时"), new(8, "指定五日"), new(9, "按天")
    });

    /// <summary>
    /// 报警主机插件的唯一产品标识，用于方案归属校验
    /// </summary>
    public const string ProductKey = "GasDG.AlarmHost";
    /// <summary>
    /// 表具编号项目的稳定标识
    /// </summary>
    public const string MeterNumber = "MeterNumber";
    /// <summary>
    /// 表具时间项目的稳定标识
    /// </summary>
    public const string MeterTime = "MeterTime";
    /// <summary>
    /// 生产状态项目的稳定标识
    /// </summary>
    public const string ProductionStatus = "ProductionStatus";
    /// <summary>
    /// 有无阀门项目的稳定标识
    /// </summary>
    public const string HasValve = "HasValve";
    /// <summary>
    /// 寿命时间项目的稳定标识
    /// </summary>
    public const string Lifetime = "Lifetime";
    /// <summary>
    /// 报警浓度阈值项目的稳定标识
    /// </summary>
    public const string AlarmThreshold = "AlarmThreshold";
    /// <summary>
    /// 报警恢复阈值项目的稳定标识
    /// </summary>
    public const string AlarmRecoveryThreshold = "AlarmRecoveryThreshold";
    /// <summary>
    /// 主服务器地址及端口配置项目的稳定标识
    /// </summary>
    public const string MainServer = "MainServer";
    /// <summary>
    /// 主卡网络账号、密码及 APN 配置项目的稳定标识
    /// </summary>
    public const string MainNetwork = "MainNetwork";
    /// <summary>
    /// 主电上报规则配置项目的稳定标识
    /// </summary>
    public const string MainPowerReport = "MainPowerReport";
    /// <summary>
    /// IMEI 读取项目的稳定标识
    /// </summary>
    public const string Imei = "Imei";
    /// <summary>
    /// IMSI 读取项目的稳定标识
    /// </summary>
    public const string Imsi = "Imsi";
    /// <summary>
    /// ICCID 读取项目的稳定标识
    /// </summary>
    public const string Iccid = "Iccid";
    /// <summary>
    /// CSQ 信号质量读取项目的稳定标识
    /// </summary>
    public const string Csq = "Csq";
    /// <summary>
    /// 版本号读取项目的稳定标识
    /// </summary>
    public const string Version = "Version";
    /// <summary>
    /// 蓝牙 SN / 星闪 MAC 读取项目的稳定标识
    /// </summary>
    public const string BluetoothSnOrSparkMac = "BluetoothSnOrSparkMac";
    /// <summary>
    /// 蓝牙广播项目的稳定标识
    /// </summary>
    public const string BluetoothBroadcast = "BluetoothBroadcast";

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

    #region 功能目录登记

    /// <summary>
    /// 功能及子参数的唯一默认定义；按参数控件数量分段，页面使用 DisplayOrder
    /// 普通新增项目由共用模板展示，不按方案类型限制勾选
    /// </summary>
    public static IReadOnlyList<AlarmHostFunctionDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        #region 仅 MES

        new(MeterNumber, "表具编号", ObjectId: 0x0002, CanWrite: true),

        new(MeterTime, "表具时间", ObjectId: 0x0003, CanWrite: true),

        new(Imei, "IMEI", ObjectId: 0x1102),

        new(Imsi, "IMSI", ObjectId: 0x1103),

        new(Iccid, "ICCID", ObjectId: 0x1104),

        new(BluetoothSnOrSparkMac, "蓝牙SN/星闪MAC", ObjectId: 0x1101),

        new(BluetoothBroadcast, "蓝牙广播"),

        new(BaseMeterNumber, "基表号读取", ObjectId: 0x100C),

        new(ModemStatus, "通讯模组状态", ObjectId: 0x1002),

        // 现场标准压力输入暂缓；隐藏尚未生效的偏差控件，保留原参数以兼容已有方案。
        new(ExternalPressure, "外部压力", [DecimalInput("Tolerance", "允许偏差(kPa)", 0, 6553.5, 1)], ObjectId: 0x1007, ShowParameters: false),

        #endregion

        #region MES + 1 个参数控件

        new AlarmHostFunctionDefinition(ProductionStatus, "生产状态",
            [Choice(SubParameterCodes.Value, "目标状态", ProductionStatusOptions, "请选择生产状态。")], ObjectId: 0x0015, CanWrite: true),

        new(HasValve, "有无阀门", [new(new ParasContentDto
        {
            Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName,
            Value = "false", DefaultValue = "false", ControlType = ParameterControlType.CheckBox,
            Validator = new() { IsRequired = true, RegexPattern = "^(?i:true|false)$", ErrorMessage = "请选择有效的有无阀门值。" }
        }, formatValue: static value => bool.Parse(value) ? "有阀门" : "无阀门")], ObjectId: 0x0021, CanWrite: true),

        new(Lifetime, "寿命时间", [Number(SubParameterCodes.Value, uint.MaxValue, "寿命时间", inputHint: "秒")], ObjectId: 0x0022, CanWrite: true),

        new(AlarmThreshold, "报警浓度阈值", [Number(SubParameterCodes.Value, byte.MaxValue, "报警浓度阈值", inputHint: "%LEL")], ObjectId: 0x0023, CanWrite: true),

        new(AlarmRecoveryThreshold, "报警恢复阈值", [Number(SubParameterCodes.Value, byte.MaxValue, "报警恢复阈值", inputHint: "%LEL")], ObjectId: 0x0024, CanWrite: true),

        new(MeterBottom, "表具底码", [DecimalInput(SubParameterCodes.Value, "表具底码", 0, 4294967295.9999, 4)], ObjectId: 0x0004, CanWrite: true),

        new(ReservedAmount, "预留量", [DecimalInput(SubParameterCodes.Value, "预留量", 0, 42949672.95, 2)], ObjectId: 0x0005, CanWrite: true),

        new(SettlementMode, "结算模式", [DefaultChoice(SubParameterCodes.Value, "结算模式", Options("表具后付费", "表具预付费", "后台预付费"))], ObjectId: 0x0007, CanWrite: true),

        new(Compensation, "补偿方式", [DefaultChoice(SubParameterCodes.Value, "补偿方式", [new(0, "不补偿"), new(3, "温压补偿")])], ObjectId: 0x001B, CanWrite: true),

        new(MeteringVerification, "计量检定类型",
            [DefaultChoice(SubParameterCodes.Value, "计量检定类型", Options("工况计量", "标况温度计量", "标况压力计量", "标况温压计量"))], ObjectId: 0x001A, CanWrite: true),

        // 001D的0/1枚举来自指定旧UGNB设备实现，非V2通用预留字段的新定义。
        new(PulseResolution, "脉冲检表分辨率", [DefaultChoice(SubParameterCodes.Value, "分辨率", Options("原威星", "新国标"))], ObjectId: 0x001D, CanWrite: true),

        new(NoCommunicationDays, "一级不通讯天数", [DecimalInput(SubParameterCodes.Value, "天数", 0, 255, 0, "30")], ObjectId: 0x0102, CanWrite: true),

        new(NoGasDays, "一级不用气天数", [DecimalInput(SubParameterCodes.Value, "天数", 0, 255, 0)], ObjectId: 0x0104, CanWrite: true),

        new(SecondNoGasDays, "二级不用气天数", [DecimalInput(SubParameterCodes.Value, "天数", 0, 255, 0)], ObjectId: 0x0105, CanWrite: true),

        new(HighFlowThreshold, "大流阈值", [DecimalInput(SubParameterCodes.Value, "大流阈值(m³)", 0, 655.35, 2)], ObjectId: 0x0100, CanWrite: true),

        new(LowFlowThreshold, "小流阈值", [DecimalInput(SubParameterCodes.Value, "小流阈值(L)", 0, 65535, 0)], ObjectId: 0x0101, CanWrite: true),

        new(LowBalanceThreshold, "少额关阀阈值", [DecimalInput(SubParameterCodes.Value, "少额关阀阈值", 0, 42949672.95, 2)], ObjectId: 0x0008, CanWrite: true),

        new(UpperTemperature, "温度上限阈值", [DecimalInput(SubParameterCodes.Value, "温度上限(℃)", 0, 655.35, 2)], ObjectId: 0x010A, CanWrite: true),

        new(LowerTemperature, "温度下限阈值", [DecimalInput(SubParameterCodes.Value, "温度下限(℃)", -327.68, 327.67, 2)], ObjectId: 0x010B, CanWrite: true),

        new(UpperPressure, "压力上限阈值", [DecimalInput(SubParameterCodes.Value, "压力上限(kPa)", 0, 6553.5, 1)], ObjectId: 0x010C, CanWrite: true),

        new(LowerPressure, "压力下限阈值", [DecimalInput(SubParameterCodes.Value, "压力下限(kPa)", 0, 6553.5, 1)], ObjectId: 0x010D, CanWrite: true),

        new(ValveStatus, "阀门状态",
            [DefaultChoice(SubParameterCodes.Value, "目标状态", Options("无", "开阀", "开阀中", "关阀", "关阀中", "异常", "阀门强开", "阀门强关"))], ObjectId: 0x1002),

        new(MeteringVersion, "计量版本", [OptionalText(SubParameterCodes.Value, "目标版本")], ObjectId: 0x100E),

        new(NetworkOperator, "网络类型", [DefaultChoice(SubParameterCodes.Value, "目标运营商", Options("移动", "联通", "电信"))], ObjectId: 0x1103),

        new(CommunicationSelfCheck, "通讯板自检",
            [DefaultChoice(SubParameterCodes.Value, "自检类型", [new(0x81, "通讯板完整自检"), new(0x82, "只进行通讯测试"), new(0x83, "无NB-IOT的通讯测试")], 180)], ObjectId: 0xFC01, CanWrite: true),

        #endregion

        #region MES + 2 个参数控件

        new(Csq, "CSQ", OptionalRange(0, 99, "CSQ"), ObjectId: 0x1105),

        new(MeterSpecification, "表具规格",
        [
            DefaultChoice("Type", "表具类型", [new(0x10, "皮膜燃气表"), new(0x20, "流量计"), new(0x30, "超声波"), new(0x40, "控制器")]),
            DefaultChoice("Specification", "表具规格", Options("G1.6", "G2.5", "G4", "G4+", "G6", "G10", "G16", "G25", "G40"))
        ], ObjectId: 0x0019, CanWrite: true),

        Valve(TamperValve, "拆表阀控"),

        Valve(Alarm119Valve, "119报警阀控"),

        Valve(HighFlowValve, "大流阀控"),

        Valve(LowFlowValve, "小流阀控"),

        Valve(ConstantFlowValve, "异常恒流阀控"),

        Valve(UpperTemperatureValve, "温度上限阀控"),

        Valve(LowerTemperatureValve, "温度下限阀控"),

        Valve(UpperPressureValve, "压力上限阀控"),

        Valve(LowerPressureValve, "压力下限阀控"),

        Valve(LowBalanceValve, "少额关阀阀控"),

        Valve(NoCommunicationValve, "一级不通讯阀控"),

        Valve(NoGasValve, "一级不用气阀控"),

        Valve(CoverValve, "拆盖阀控"),

        Valve(BatteryCoverValve, "电池仓开盖阀控"),

        // V2原图13：100F为通讯主电；旧UGNB的同名项目读100B，不能沿旧名称误接计量备电。
        new(MainVoltage, "主电电压检测", RequiredRange(0, 25.5, "V"), ObjectId: 0x100F),

        new(MeteringVoltage, "计量电压", RequiredRange(0, 25.5, "V"), ObjectId: 0x100D),

        #endregion

        #region MES + 3 个参数控件

        new(MainServer, "主服务器",
        [
            Choice("AddressType", "地址类型", AddressTypeOptions, "请选择服务器地址类型。", inputWidth: 90),
            new(new ParasContentDto { Code = "Address", Name = "地址", Validator = new() { IsRequired = true, ErrorMessage = "请填写服务器地址。" } }, inputWidth: 180),
            Number("Port", ushort.MaxValue, "端口")
        ], ObjectId: 0x0011, CanWrite: true),

        new(MainNetwork, "主卡网络",
        [
            new(new ParasContentDto { Code = "UserName", Name = "网络账号" }, inputWidth: 140),
            new(new ParasContentDto { Code = "Password", Name = "网络密码" }, inputWidth: 140),
            new(new ParasContentDto { Code = "Apn", Name = "APN" }, inputWidth: 140)
        ], ObjectId: 0x000F, CanWrite: true),

        new(Version, "版本号", [OptionalText("Software", "嵌软版本"), OptionalText("Hardware", "硬件版本"), OptionalText("CompileTime", "编译时间")], ObjectId: 0x1000),

        new(SettlementFunctions, "结算功能",
            [Flag("Purge", "启用排空量"), Flag("ShowSettlement", "显示结算信息"), Flag("VirtualBilling", "启用虚拟计费")], ObjectId: 0x000A, CanWrite: true),

        new(MonthlyFreeze, "月冻结日期",
            // C5F0附录A 6.3：日为0表示天冻结，1至28表示月冻结。
            [DecimalInput("Day", "日", 0, 28, 0, "0", 56), DecimalInput("Hour", "时", 0, 23, 0, "0", 56), DecimalInput("Minute", "分", 0, 59, 0, "0", 56)], ObjectId: 0x000E, CanWrite: true),

        new(ConstantFlowThreshold, "异常恒流阈值",
        [
            DecimalInput("Lower", "最小值(m³/h)", 0, 65.535, 3),
            DecimalInput("Upper", "最大值(m³/h)", 0, 65.535, 3),
            DecimalInput("Duration", "持续时间(分)", 0, 65535, 0)
        ], ObjectId: 0x0109, CanWrite: true),

        #endregion

        #region MES + 5/6 个参数控件（主电上报，按类型切换）

        new(MainPowerReport, "主电上报",
        [
            Choice("Type", "上报类型", ReportTypeOptions, "请选择上报类型。"),
            Number("Cycle", byte.MaxValue, "周期"), Number("Day", 31, "日"),
            Number("Hour", 23, "时"), Number("Minute", 59, "分"),
            // 四位时段使用紧凑宽度，类型及五时段含间距共572，宽组扣除滚动条后仍可同行。
            new(new ParasContentDto { Code = "Slot1", Name = "时段1" }, inputWidth: 80), new(new ParasContentDto { Code = "Slot2", Name = "时段2" }, inputWidth: 80),
            new(new ParasContentDto { Code = "Slot3", Name = "时段3" }, inputWidth: 80), new(new ParasContentDto { Code = "Slot4", Name = "时段4" }, inputWidth: 80),
            new(new ParasContentDto { Code = "Slot5", Name = "时段5" }, inputWidth: 80)
        ], ObjectId: 0x000C, CanWrite: true),

        #endregion
    }.OrderBy(item => item.HasVisibleParameters ? 0 : 1).ToArray());

    #endregion

    #region 页面显示顺序

    /// <summary>
    /// 页面阅读顺序：普通参数项、宽组参数项、仅MES项；保留各组内目录顺序
    /// 仅供配置、预览和结果行显示，不改变方案保存的执行 Order
    /// </summary>
    public static IReadOnlyList<AlarmHostFunctionDefinition> DisplayOrder { get; } = Array.AsReadOnly(All
        .OrderBy(item => !item.HasVisibleParameters ? 2 : item.IsComplex ? 1 : 0).ToArray());

    #endregion

    /// <summary>
    /// 已登记对象的项目共用设备执行链；蓝牙广播尚未接入
    /// </summary>
    public static bool CanExecute(string code) =>
        Get(code).ObjectId.HasValue;

    /// <summary>
    /// 按区分大小写的功能编码取得本插件定义
    /// </summary>
    public static AlarmHostFunctionDefinition Get(string key) =>
        All.FirstOrDefault(item => item.Key == key)
        ?? throw new ArgumentException("未知的报警主机功能标识。", nameof(key));

    /// <summary>
    /// 取得当前分支实际使用的参数；主电上报的另一分支仅留在编辑草稿中
    /// </summary>
    public static IEnumerable<ParasContentDto> ActiveParameters(TestParameterDto item)
    {
        if (item.Code != MainPowerReport) return item.Parameters;
        var type = item.Parameters.Single(parameter => parameter.Code == "Type").Value.Trim();
        var specified = type is "7" or "8";
        return item.Parameters.Where(parameter => parameter.Code == "Type" ||
            parameter.Code.StartsWith("Slot", StringComparison.Ordinal) == specified);
    }

    /// <summary>
    /// 按公共参数的规则检查输入，仅检查已启用功能及其当前分支
    /// </summary>
    public static IEnumerable<string> GetInputErrors(TestParameterDto item)
    {
        if (!item.IsEnabled) yield break;
        foreach (var parameter in ActiveParameters(item))
        {
            var rule = parameter.Validator;
            var value = parameter.Value.Trim();
            // 文本按原值保存，长度校验也包含空格；可选空白值仍须遵守长度上限。
            if (rule?.MaxLength is int maxLength && parameter.Value.Length > maxLength)
            {
                yield return rule.ErrorMessage ?? $"{item.Name}的 {parameter.Code} 超出长度限制。";
                continue;
            }
            if (value.Length == 0 && rule?.IsRequired != true) continue;
            var valid = (rule?.IsRequired != true || value.Length > 0) && Get(item.Code).GetParameter(parameter.Code).AcceptsOption(value);
            if (rule?.MinValue != null || rule?.MaxValue != null)
                valid &= decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                    && (rule.MinValue == null || number >= (decimal)rule.MinValue.Value)
                    && (rule.MaxValue == null || number <= (decimal)rule.MaxValue.Value);
            if (rule?.RegexPattern != null) valid &= Regex.IsMatch(value, rule.RegexPattern);
            if (!valid) yield return rule?.ErrorMessage ?? $"{item.Name}的 {parameter.Code} 输入无效。";
        }
        if (item.Parameters.Any(parameter => parameter.Code == "Lower") && item.Parameters.Any(parameter => parameter.Code == "Upper"))
        {
            var lower = item.Parameters.Single(parameter => parameter.Code == "Lower").Value;
            var upper = item.Parameters.Single(parameter => parameter.Code == "Upper").Value;
            if (decimal.TryParse(lower, NumberStyles.Number, CultureInfo.InvariantCulture, out var min) &&
                decimal.TryParse(upper, NumberStyles.Number, CultureInfo.InvariantCulture, out var max) && min > max)
                yield return $"{item.Name}下限不能大于上限。";
        }
        if (item.Code != MainPowerReport) yield break;
        var type = item.Parameters.Single(parameter => parameter.Code == "Type").Value.Trim();
        if (type is not ("7" or "8")) yield break;
        foreach (var parameter in ActiveParameters(item).Where(parameter => parameter.Code != "Type"))
        {
            var slot = parameter.Value.Trim();
            var valid = slot.Length == 4 && slot.All(c => c is >= '0' and <= '9');
            if (valid)
            {
                var first = int.Parse(slot[..2], CultureInfo.InvariantCulture);
                var second = int.Parse(slot[2..], CultureInfo.InvariantCulture);
                valid = type == "7" ? first <= 23 && second <= 59 : first is >= 1 and <= 31 && second <= 23;
            }
            if (!valid) yield return $"上报时段 {parameter.Code[4..]} 应为有效的 {(type == "7" ? "HHmm" : "ddHH")}。";
        }
    }

    /// <summary>
    /// 创建以原始字符串编辑的非负整数参数及范围提示
    /// </summary>
    private static AlarmHostParameterDefinition Number(string code, uint maximum, string label,
        double inputWidth = 100, string? inputHint = null) => new(new ParasContentDto
    {
        Code = code, Name = code == SubParameterCodes.Value ? SubParameterCodes.ValueDisplayName : label,
        ControlType = ParameterControlType.NumericUpDown,
        Validator = new() { MinValue = 0, MaxValue = maximum, RegexPattern = "^[0-9]+$", IsRequired = true, ErrorMessage = $"{label}应为 0–{maximum} 的整数。" }
    }, inputWidth: inputWidth, inputHint: inputHint);

    /// <summary>
    /// 创建尚未选择的枚举参数，输入选项、允许值及只读翻译使用同一份定义
    /// </summary>
    private static AlarmHostParameterDefinition Choice(string code, string name,
        IReadOnlyList<KeyValuePair<byte, string>> options, string message, double inputWidth = 100) => new(new ParasContentDto
    {
        Code = code, Name = name,
        ControlType = ParameterControlType.ComboBox,
        Validator = new() { IsRequired = true, ErrorMessage = message }
    }, options, inputWidth: inputWidth);

    /// <summary>
    /// 共用阀控选项及更改字段，启用项合并为一个0200对象
    /// </summary>
    private static AlarmHostFunctionDefinition Valve(string code, string name) => new(code, name,
        [Flag("Change", "更改", true, 64), DefaultChoice(SubParameterCodes.Value, "阀控方式", Options("禁用", "普通关阀", "权限关阀", "不关阀"), 100)], ObjectId: 0x0200, CanWrite: true);

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
