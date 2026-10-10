namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 协议枚举选项提供者
/// <para>根据参数 Code（唯一识别码）返回协议定义的枚举选项列表，用于 UI 下拉选择框。</para>
/// <para>所有选项严格遵循协议文档定义，不得擅自修改。</para>
/// </summary>
public static class ProtocolEnumOptions
{
    /// <summary>枚举选项（Key=协议值, Display=显示文本）</summary>
    public sealed record EnumOption(string Key, string Display);

    private static readonly Dictionary<string, List<EnumOption>> _options = new()
    {
        // ===== WholeSet 枚举参数 =====

        ["RESET_FACTORY"] = new() { new("0", "不更改"), new("1", "更改") },
        ["RUN_MODE"] = new() { new("0", "表具后付费"), new("1", "表具预付费"), new("2", "后台预付费") },
        ["BILLING_TYPE"] = new() { new("0", "金额式"), new("1", "气量") },
        ["METER_SPEC"] = new()
        {
            new("0x03", "G6"),
            new("0x04", "G10"),
            new("0x05", "G16"),
            new("0x06", "G25"),
        },
        ["READING_TYPE"] = new()
        {
            new("0", "每天"),
            new("1", "单月"),
            new("2", "双月"),
            new("3", "每月"),
            new("4", "按分"),
            new("5", "按时"),
            new("7", "指定5时"),
            new("8", "指定5日"),
            new("9", "按天"),
        },
        ["ENCRYPT"] = new() { new("0", "不加密"), new("1", "加密") },
        ["WAKE_CYCLE"] = new() { new("0", "不生效"), new("1", "生效") },
        ["STATUS_VALVE"] = new() { new("0", "有阀门"), new("1", "无阀门") },

        // ===== WholeGet 枚举参数 =====

        ["GPRS_STATUS"] = new() { new("0", "异常"), new("1", "正常") },
        ["TCP_STATUS"] = new() { new("0", "异常"), new("1", "正常") },
        ["SIM_STATUS"] = new() { new("0", "异常"), new("1", "正常") },
        ["VALVE_STATUS"] = new()
        {
            new("001", "开阀"),
            new("011", "关阀"),
            new("101", "异常"),
            new("111", "阀门权关"),
        },
        ["ALARM_STATUS"] = new() { new("0", "已触发"), new("1", "未触发") },
        ["RS485_STATUS"] = new() { new("0", "异常"), new("1", "正常") },
        ["BT_STATUS"] = new() { new("0", "未连接"), new("1", "已连接") },
        ["COMM_TYPE"] = new() { new("0", "移动流量"), new("1", "电信流量") },
        ["METER_TYPE"] = new() { new("0", "霍尔"), new("1", "光电"), new("2", "超声波") },
        ["IS_ENCRYPTED"] = new() { new("0", "否"), new("1", "是") },
        ["VOLTAGE_STATUS"] = new()
        {
            new("0", "正常(00)"),
            new("1", "欠压(01)"),
            new("2", "低压(10)"),
            new("3", "掉电(11)"),
        },
        ["TEMP_SENSOR_TYPE"] = new()
        {
            new("0x00", "TI tmp102"),
            new("0x01", "华普HP6862I"),
            new("0x02", "智感as55"),
            new("0x03", "龙威lwp5200ad"),
            new("0xFF", "无"),
        },

        // ===== 表具状态字 16 位报警标志（0=正常, 1=异常） =====
        ["TAMPER"] = new() { new("0", "正常"), new("1", "异常") },
        ["HIGH_FLOW"] = new() { new("0", "正常"), new("1", "异常") },
        ["CONST_FLOW"] = new() { new("0", "正常"), new("1", "异常") },
        ["LOW_FLOW"] = new() { new("0", "正常"), new("1", "异常") },
        ["MEAS_ALARM"] = new() { new("0", "正常"), new("1", "异常") },
        ["IDLE_ALARM"] = new() { new("0", "正常"), new("1", "异常") },
        ["REVERSE_ALARM"] = new() { new("0", "正常"), new("1", "异常") },
        ["PRESSURE_LOSS"] = new() { new("0", "正常"), new("1", "异常") },
        ["PRESSURE_OVER"] = new() { new("0", "正常"), new("1", "异常") },
        ["PRESSURE_SENSOR"] = new() { new("0", "正常"), new("1", "异常") },
        ["TEMP_SENSOR"] = new() { new("0", "正常"), new("1", "异常") },
        ["OPEN_COVER"] = new() { new("0", "正常"), new("1", "异常") },
        ["GAS_LEAK"] = new() { new("0", "正常"), new("1", "异常") },
        ["DAILY_LIMIT"] = new() { new("0", "正常"), new("1", "异常") },
        ["DISCONNECT"] = new() { new("0", "正常"), new("1", "异常") },
    };

    /// <summary>参数说明（按 MES 代码索引），用于 UI Tooltip 提示</summary>
    private static readonly Dictionary<string, string> _descriptions = new()
    {
        ["RESET_FACTORY"] = "协议 4.1.1: 1B HEX，0=不更改，1=更改",
        ["METER_NO"] = "协议 4.1.1: 7B BCD，13 位表具编号，低位在前高位在后",
        ["METER_TIME"] = "协议 4.1.1: 6B BCD，格式 YYMMDDhhmmss（年月日时分秒）",
        ["BASE_CODE"] = "协议 4.1.1: 4B HEX，低位在前，2 位小数",
        ["RUN_MODE"] = "协议 4.1.1: 1B HEX，0=表具后付费，1=表具预付费，2=后台预付费",
        ["PREPAID_AMT"] = "协议 4.1.1: 4B HEX，低位在前，2 位小数",
        ["STORAGE_LIMIT"] = "协议 4.1.1: 4B HEX，低位在前，2 位小数",
        ["BILLING_TYPE"] = "协议 4.1.1: 1B HEX，0=金额式，1=气量",
        ["METER_SPEC"] = "协议 4.1.1: 1B HEX，0x03=G6，0x04=G10，0x05=G16，0x06=G25",
        ["READING_TYPE"] = "协议 4.1.4: 抄表类型 1B，0=每天，1=单月，2=双月，3=每月，4=按分，5=按时，7=指定5时，8=指定5日，9=按天",
        ["FREEZE_DAY"] = "协议 4.1.7: 3B BCD 中的日字段，0=天冻结，1~28=月冻结",
        ["FREEZE_HOUR"] = "协议 4.1.7: 3B BCD 中的时字段，0~23",
        ["FREEZE_MINUTE"] = "协议 4.1.7: 3B BCD 中的分字段，0~59",
        ["DOMAIN"] = "协议 4.1.1: ASCII 编码，最长 30 字节",
        ["DOMAIN_PORT"] = "协议 4.1.1: 2B HEX，如 8900=0x22 0xC4",
        ["IP_ADDR"] = "协议 4.1.6: 4B HEX，如 192.168.1.1=C0 A8 01 01",
        ["IP_PORT"] = "协议 4.1.6: 2B HEX，如 8888=0x22 0xB8",
        ["NET_USER"] = "协议 4.1.5: 网络配置参数中的用户名，ASCII 编码，最长 30 字节",
        ["NET_PWD"] = "协议 4.1.5: 网络配置参数中的密码，ASCII 编码，最长 30 字节",
        ["ENCRYPT"] = "协议 4.1.1: 1B HEX，0=不加密，1=加密",
        ["WAKE_CYCLE"] = "协议 4.1.1: 1B HEX，0=不生效，1=生效",
        ["STATUS_VALVE"] = "协议 4.1.1: 状态配置 2B HEX 的 B0 位，0=有阀门，1=无阀门",

        ["COMM_BATT_V"] = "协议 4.1.2: 1B HEX，值/10=V，如 0x2D=4.5V",
        ["MEAS_BATT_V"] = "协议 4.1.2: 1B HEX，值/10=V",
        ["SIGNAL_STR"] = "协议 4.1.2: 1B HEX，0x01~0x31=有效，0x99=无信号，0xFF=无状态",
        ["TEMP"] = "协议 4.1.2: 4B HEX，低位在前，2 位小数",
        ["PRESSURE"] = "协议 4.1.2: 4B HEX，低位在前，2 位小数",
        ["ALARM_CONC"] = "协议 4.5: 1B，百分比 0~100，单位 %LEL",
        ["CSQ"] = "协议 4.4: 1B HEX，CSQ 信号质量值",
        ["RSRP"] = "协议 4.4: 2B HEX，低位在前，有符号，0x7EFF=-130dBm",
        ["SNR"] = "协议 4.4: 2B HEX，低位在前，有符号",
        ["GPRS_STATUS"] = "协议 4.1.2: 1B HEX，0xFF=无状态，0x00=异常，0x01=正常",
        ["TCP_STATUS"] = "协议 4.1.2: 1B HEX，0xFF=无状态，0x00=异常，0x01=正常",
        ["SIM_STATUS"] = "协议 4.1.2: 1B HEX，0xFF=无状态，0x00=异常，0x01=正常",
        ["VALVE_STATUS"] = "协议 4.1.2: 3 位，000=无，001=开阀，011=关阀，101=异常，111=阀门权关",
        ["ALARM_STATUS"] = "协议 4.5: 1B，0=已触发，1=未触发",
        ["RS485_STATUS"] = "协议 4.5: 1B，0=异常，1=正常",
        ["BT_STATUS"] = "协议 4.5: 1B，0=未连接，1=已连接",
        ["COMM_TYPE"] = "协议 4.1.3: 1B HEX，0=移动流量，1=电信流量",
        ["METER_TYPE"] = "协议 4.1.3: 1B HEX，0=霍尔，1=光电，2=超声波",
        ["IS_ENCRYPTED"] = "协议 4.1.2: 1B HEX，0=否，1=是",
        ["VOLTAGE_STATUS"] = "协议 4.1.2: 2 位，00=正常，01=欠压，10=低压，11=掉电",
        ["TAMPER"] = "协议 4.1.2: 表具状态字 B7 位，0=正常，1=异常",
        ["HIGH_FLOW"] = "协议 4.1.2: 表具状态字 B6 位，0=正常，1=异常",
        ["CONST_FLOW"] = "协议 4.1.2: 表具状态字 B5 位，0=正常，1=异常",
        ["LOW_FLOW"] = "协议 4.1.2: 表具状态字 B4 位，0=正常，1=异常",
        ["MEAS_ALARM"] = "协议 4.1.2: 表具状态字 B3 位，0=正常，1=异常",
        ["IDLE_ALARM"] = "协议 4.1.2: 表具状态字 B15 位，0=正常，1=异常",
        ["REVERSE_ALARM"] = "协议 4.1.2: 表具状态字 B14 位，0=正常，1=异常",
        ["PRESSURE_LOSS"] = "协议 4.1.2: 表具状态字 B11 位，0=正常，1=异常",
        ["PRESSURE_OVER"] = "协议 4.1.2: 表具状态字 B10 位，0=正常，1=异常",
        ["PRESSURE_SENSOR"] = "协议 4.1.2: 表具状态字 B9 位，0=正常，1=异常",
        ["TEMP_SENSOR"] = "协议 4.1.2: 表具状态字 B8 位，0=正常，1=异常",
        ["OPEN_COVER"] = "协议 4.1.2: 表具状态字 B19 位，0=正常，1=异常",
        ["GAS_LEAK"] = "协议 4.1.2: 表具状态字 B18 位，0=正常，1=异常",
        ["DAILY_LIMIT"] = "协议 4.1.2: 表具状态字 B17 位，0=正常，1=异常",
        ["DISCONNECT"] = "协议 4.1.2: 表具状态字 B16 位，0=正常，1=异常",
        ["REMAINING_AMT"] = "协议 4.1.2: 4B HEX，低位在前，2 位小数",
        ["UNIT_PRICE"] = "协议 4.1.2: 4B HEX，低位在前，4 位小数",
        ["ECL"] = "协议 4.4: 1B HEX，ECL 信号等级 0~6",
        ["IMEI"] = "协议 4.4: 15 ASCII 字符",
        ["IMSI"] = "协议 4.4: 20 ASCII 字符，不足补空格",
        ["ICCID"] = "协议 4.4: 20 ASCII 字符",
        ["BT_SN"] = "协议 4.4: 12 ASCII 字符，蓝牙模块 SN 号",
        ["TEMP_SENSOR_TYPE"] = "协议 4.1.2: 1B HEX，0x00=TI tmp102，0x01=华普HP6862I，0x02=智感as55，0x03=龙威lwp5200ad，0xFF=无",
    };

    /// <summary>判断指定 Code 是否为枚举类型参数</summary>
    public static bool IsEnumParameter(string code) => _options.ContainsKey(code);

    /// <summary>获取枚举参数的选项列表</summary>
    public static List<EnumOption>? GetOptions(string code)
        => _options.TryGetValue(code, out var options) ? options : null;

    /// <summary>获取参数的协议说明文本（用于 Tooltip）</summary>
    public static string GetDescription(string code)
        => _descriptions.TryGetValue(code, out var desc) ? desc : string.Empty;
}
