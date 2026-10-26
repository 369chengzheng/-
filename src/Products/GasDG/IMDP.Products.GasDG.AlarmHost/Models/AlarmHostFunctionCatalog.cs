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
/// <param name="ObjectId">已接入设备执行的C5F0对象编号；仅展示的新项目及广播为空</param>
/// <param name="CanWrite">设备协议是否提供写指令，与设置或检验页面类型分开</param>
public sealed record AlarmHostFunctionDefinition(string Key, string DisplayName,
    IReadOnlyList<AlarmHostParameterDefinition>? Parameters = null, ushort? ObjectId = null, bool CanWrite = false)
{
    /// <summary>
    /// 字段总宽度超出普通组空间时使用宽组，配置与预览共用判断
    /// </summary>
    public bool IsComplex => Parameters?.Sum(parameter => parameter.InputWidth + 12) > 194;

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
public static partial class AlarmHostFunctionCatalog
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
    /// 功能及子参数的唯一默认定义；保持保存采集的目录顺序，页面使用 DisplayOrder
    /// 普通新增项目由共用模板展示，不按方案类型限制勾选
    /// </summary>
    public static IReadOnlyList<AlarmHostFunctionDefinition> All { get; } = Array.AsReadOnly(new[]
    {
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
        new(MainPowerReport, "主电上报",
        [
            Choice("Type", "上报类型", ReportTypeOptions, "请选择上报类型。"),
            Number("Cycle", byte.MaxValue, "周期"), Number("Day", 31, "日"),
            Number("Hour", 23, "时"), Number("Minute", 59, "分"),
            new(new ParasContentDto { Code = "Slot1", Name = "时段1" }, inputWidth: 96), new(new ParasContentDto { Code = "Slot2", Name = "时段2" }, inputWidth: 96),
            new(new ParasContentDto { Code = "Slot3", Name = "时段3" }, inputWidth: 96), new(new ParasContentDto { Code = "Slot4", Name = "时段4" }, inputWidth: 96),
            new(new ParasContentDto { Code = "Slot5", Name = "时段5" }, inputWidth: 96)
        ], ObjectId: 0x000C, CanWrite: true),
        new(MeterNumber, "表具编号", ObjectId: 0x0002, CanWrite: true), new(MeterTime, "表具时间", ObjectId: 0x0003, CanWrite: true),
        new(Imei, "IMEI", ObjectId: 0x1102), new(Imsi, "IMSI", ObjectId: 0x1103), new(Iccid, "ICCID", ObjectId: 0x1104),
        new(Csq, "CSQ", OptionalRange(0, 99, "CSQ"), ObjectId: 0x1105),
        new(Version, "版本号", [OptionalText("Software", "嵌软版本"), OptionalText("Hardware", "硬件版本"), OptionalText("CompileTime", "编译时间")], ObjectId: 0x1000),
        new(BluetoothSnOrSparkMac, "蓝牙SN/星闪MAC", ObjectId: 0x1101), new(BluetoothBroadcast, "蓝牙广播")
    }.Concat(CreateAdditionalItems()).OrderBy(item => item.Parameters is { Count: > 0 } ? 0 : 1).ToArray());

    /// <summary>
    /// 页面阅读顺序：普通有参数项、宽组有参数项、无参数项；保留各组内目录顺序
    /// 仅供配置、预览和结果行显示，不改变方案保存的执行 Order
    /// </summary>
    public static IReadOnlyList<AlarmHostFunctionDefinition> DisplayOrder { get; } = Array.AsReadOnly(All
        .OrderBy(item => item.Parameters is not { Count: > 0 } ? 2 : item.IsComplex ? 1 : 0).ToArray());

    /// <summary>
    /// 保留既有无目标读取；新增范围或版本目标尚未接判定时，仅允许展示方案
    /// </summary>
    public static bool CanExecute(string code, IReadOnlyDictionary<string, string> parameters) =>
        Get(code).ObjectId.HasValue &&
        (code is not (Csq or Version) || parameters.Values.All(string.IsNullOrWhiteSpace));

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
}
