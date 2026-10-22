using System.Globalization;
using System.Text.RegularExpressions;
using IMDP.Application.DTOs;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 报警功能的编码、名称、适用类型及公共 DTO 参数默认值
/// </summary>
/// <param name="Key">持久化后保持稳定的功能编码</param>
/// <param name="DisplayName">页面显示名称</param>
/// <param name="InspectionOnly">是否仅用于检验方案</param>
/// <param name="CreateParameters">为每份草稿分别创建子参数，未提供时表示无子参数</param>
public sealed record AlarmHostFunctionDefinition(string Key, string DisplayName, bool InspectionOnly,
    Func<List<ParasContentDto>>? CreateParameters = null)
{
    /// <summary>
    /// 按平台 0=设置、1=检验判断功能适用性
    /// </summary>
    public bool Supports(int type) => type switch
    {
        0 => !InspectionOnly,
        1 => true,
        _ => false
    };

    /// <summary>
    /// 创建独立且默认未勾选的功能 DTO，子参数不与其他草稿共享
    /// </summary>
    public TestParameterDto CreateItem()
    {
        var parameters = CreateParameters?.Invoke() ?? [];
        for (var i = 0; i < parameters.Count; i++) parameters[i].Order = i + 1;
        return new TestParameterDto { Code = Key, Name = DisplayName, Parameters = parameters };
    }
}

/// <summary>
/// 本插件的固定功能目录及输入规则，复用公共参数 DTO，不生成动态表单
/// 编码用于编辑、采集及回填定位，不是设备协议对象号或 MES 码
/// </summary>
public static class AlarmHostFunctionCatalog
{
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
    /// IMEI 检验项目的稳定标识
    /// </summary>
    public const string Imei = "Imei";
    /// <summary>
    /// IMSI 检验项目的稳定标识
    /// </summary>
    public const string Imsi = "Imsi";
    /// <summary>
    /// ICCID 检验项目的稳定标识
    /// </summary>
    public const string Iccid = "Iccid";
    /// <summary>
    /// CSQ 信号质量检验项目的稳定标识
    /// </summary>
    public const string Csq = "Csq";
    /// <summary>
    /// 版本号检验项目的稳定标识
    /// </summary>
    public const string Version = "Version";
    /// <summary>
    /// 蓝牙 SN / 星闪 MAC 检验项目的稳定标识
    /// </summary>
    public const string BluetoothSnOrSparkMac = "BluetoothSnOrSparkMac";
    /// <summary>
    /// 蓝牙广播检验项目的稳定标识
    /// </summary>
    public const string BluetoothBroadcast = "BluetoothBroadcast";

    /// <summary>
    /// 功能及子参数的唯一默认定义；新增普通项目在此登记并补充对应页面控件
    /// </summary>
    public static IReadOnlyList<AlarmHostFunctionDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new AlarmHostFunctionDefinition(MeterNumber, "表具编号", false),
        new(MeterTime, "表具时间", false),
        new(ProductionStatus, "生产状态", false, () => [Choice(SubParameterCodes.Value, "请选择生产状态。", "[01]")]),
        new(HasValve, "有无阀门", false, () => [new ParasContentDto
        {
            Code = SubParameterCodes.Value, Name = SubParameterCodes.ValueDisplayName,
            Value = "false", DefaultValue = "false", ControlType = ParameterControlType.CheckBox,
            Validator = new() { RegexPattern = "^(?i:true|false)$", ErrorMessage = "请选择有效的有无阀门值。" }
        }]),
        new(Lifetime, "寿命时间", false, () => [Number(SubParameterCodes.Value, uint.MaxValue, "寿命时间")]),
        new(AlarmThreshold, "报警浓度阈值", false, () => [Number(SubParameterCodes.Value, byte.MaxValue, "报警浓度阈值")]),
        new(AlarmRecoveryThreshold, "报警恢复阈值", false, () => [Number(SubParameterCodes.Value, byte.MaxValue, "报警恢复阈值")]),
        new(MainServer, "主服务器", false, () =>
        [
            Choice("AddressType", "请选择服务器地址类型。", "[01]"),
            new() { Code = "Address", Name = "Address", Validator = new() { IsRequired = true, ErrorMessage = "请填写服务器地址。" } },
            Number("Port", ushort.MaxValue, "服务器端口")
        ]),
        new(MainNetwork, "主卡网络", false, () =>
        [
            new() { Code = "UserName", Name = "UserName" },
            new() { Code = "Password", Name = "Password" },
            new() { Code = "Apn", Name = "Apn" }
        ]),
        new(MainPowerReport, "主电上报", false, () =>
        [
            Choice("Type", "请选择上报类型。", "[0-5789]"),
            Number("Cycle", byte.MaxValue, "上报周期"), Number("Day", 31, "上报日"),
            Number("Hour", 23, "上报时"), Number("Minute", 59, "上报分"),
            new() { Code = "Slot1", Name = "Slot1" }, new() { Code = "Slot2", Name = "Slot2" },
            new() { Code = "Slot3", Name = "Slot3" }, new() { Code = "Slot4", Name = "Slot4" },
            new() { Code = "Slot5", Name = "Slot5" }
        ]),
        new(Imei, "IMEI", true), new(Imsi, "IMSI", true), new(Iccid, "ICCID", true),
        new(Csq, "CSQ", true), new(Version, "版本号", true),
        new(BluetoothSnOrSparkMac, "蓝牙SN/星闪MAC", true), new(BluetoothBroadcast, "蓝牙广播", true)
    });

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
            if (rule == null) continue;
            var value = parameter.Value.Trim();
            var valid = !rule.IsRequired || value.Length > 0;
            if (rule.MaxValue.HasValue)
                valid &= uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    && number >= rule.MinValue && number <= rule.MaxValue;
            if (rule.RegexPattern != null) valid &= Regex.IsMatch(value, rule.RegexPattern);
            if (!valid) yield return rule.ErrorMessage ?? $"{item.Name}的 {parameter.Code} 输入无效。";
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
    private static ParasContentDto Number(string code, uint maximum, string label) => new()
    {
        Code = code, Name = code == SubParameterCodes.Value ? SubParameterCodes.ValueDisplayName : code,
        ControlType = ParameterControlType.NumericUpDown,
        Validator = new() { MinValue = 0, MaxValue = maximum, IsRequired = true, ErrorMessage = $"{label}应为 0–{maximum} 的整数。" }
    };

    /// <summary>
    /// 创建尚未选择的枚举参数；选项显示仍使用现有页面控件
    /// </summary>
    private static ParasContentDto Choice(string code, string message, string pattern) => new()
    {
        Code = code, Name = code == SubParameterCodes.Value ? SubParameterCodes.ValueDisplayName : code,
        ControlType = ParameterControlType.ComboBox,
        Validator = new() { IsRequired = true, RegexPattern = "^" + pattern + "$", ErrorMessage = message }
    };
}
