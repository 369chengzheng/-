namespace IMDP.Products.GasDG.WholeA.Protocol;

// ====================================================================================
// 数据模型类 — 威星物联网燃气表生产测试通讯协议 V1.0.0
// 对应协议文档第 4 章"数据格式"中定义的各数据结构
// ====================================================================================

/// <summary>
/// 系统时钟（数据域前 6 字节 BCD: 年月日时分秒）
/// <para>当 Year=0xFF 时表示时钟无效。</para>
/// </summary>
public class SystemClock
{
    /// <summary>年（BCD，0xFF 表示无效）</summary>
    public byte Year { get; set; }

    /// <summary>月（BCD 01~12）</summary>
    public byte Month { get; set; }

    /// <summary>日（BCD 01~31）</summary>
    public byte Day { get; set; }

    /// <summary>时（BCD 00~23）</summary>
    public byte Hour { get; set; }

    /// <summary>分（BCD 00~59）</summary>
    public byte Minute { get; set; }

    /// <summary>秒（BCD 00~59）</summary>
    public byte Second { get; set; }

    /// <summary>时钟是否有效（Year != 0xFF）</summary>
    public bool IsValid => Year != 0xFF;

    /// <summary>转换为 DateTime（时钟无效时返回 null）</summary>
    public DateTime? ToDateTime()
    {
        if (!IsValid) return null;
        try
        {
            int year = 2000 + BcdToDecimal(Year);
            int month = BcdToDecimal(Month);
            int day = BcdToDecimal(Day);
            int hour = BcdToDecimal(Hour);
            int minute = BcdToDecimal(Minute);
            int second = BcdToDecimal(Second);
            return new DateTime(year, month, day, hour, minute, second);
        }
        catch { return null; }
    }

    public override string ToString()
        => IsValid ? $"{ToDateTime():yyyy-MM-dd HH:mm:ss}" : "时钟无效";

    private static int BcdToDecimal(byte bcd)
        => ((bcd >> 4) & 0x0F) * 10 + (bcd & 0x0F);
}

/// <summary>
/// 抄表时间参数（11 字节）
/// <para>包含抄表类型和最多 5 组抄表时间/日期对。</para>
/// </summary>
public class ReadingTimeParams
{
    /// <summary>抄表类型</summary>
    public ReadingType Type { get; set; }

    /// <summary>5 组抄表周期/时间/日期（每组 2B BCD，多余字节填 0xFF）</summary>
    public List<ReadingTimeSlot> Slots { get; set; } = new();
}

/// <summary>单组抄表时间（周期 + 时间/日期）</summary>
public class ReadingTimeSlot
{
    /// <summary>周期（1B HEX）</summary>
    public byte Period { get; set; }

    /// <summary>时间值（2B BCD，如 01 00 = 1点0分）</summary>
    public byte[] TimeValue { get; set; } = new byte[2];

    /// <summary>日期值（2B BCD，如 01 00 = 1日0点）</summary>
    public byte[] DateValue { get; set; } = new byte[2];
}

/// <summary>
/// 网络配置参数（2+N+n 字节）
/// <para>格式：用户名长度(1B) + 用户名(N ASCII) + 密码长度(1B) + 密码(n ASCII)</para>
/// </summary>
public class NetworkConfig
{
    /// <summary>用户名</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码（接入点）</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// IP 地址配置参数（7 字节）
/// <para>格式：启用标志(1B) + IP(4B) + 端口(2B)</para>
/// </summary>
public class IpConfig
{
    /// <summary>启用标志（0x20=启用域名，其他=启用 IP）</summary>
    public byte EnableFlag { get; set; }

    /// <summary>目标 IP 地址</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>端口号</summary>
    public int Port { get; set; }

    /// <summary>是否启用域名</summary>
    public bool IsDomainEnabled => EnableFlag == 0x20;
}

/// <summary>
/// 月冻结日参数（3 字节 BCD: 日、时、分）
/// </summary>
public class MonthlyFreezeParams
{
    /// <summary>冻结日（BCD 00=天冻结, 1~28=月冻结）</summary>
    public byte Day { get; set; }

    /// <summary>冻结时（BCD）</summary>
    public byte Hour { get; set; }

    /// <summary>冻结分（BCD）</summary>
    public byte Minute { get; set; }

    /// <summary>是否为天冻结</summary>
    public bool IsDailyFreeze => Day == 0;
}

/// <summary>
/// 表具版本（6 字节）
/// <para>协议版本(1B BCD) + 软件版本(2B BCD) + 硬件版本(2B BCD) + 保留(1B)</para>
/// </summary>
public class MeterVersion
{
    /// <summary>协议版本（BCD，如 0x10 表示 V1.0）</summary>
    public byte ProtocolVersion { get; set; }

    /// <summary>软件版本（2B BCD，低位在前，如 0x01 0x02 表示 V1.0.2）</summary>
    public byte[] SoftwareVersion { get; set; } = new byte[2];

    /// <summary>硬件版本（2B BCD）</summary>
    public byte[] HardwareVersion { get; set; } = new byte[2];

    /// <summary>保留字段（必须为 0）</summary>
    public byte Reserved { get; set; }

    /// <summary>协议版本字符串</summary>
    public string ProtocolVersionString => $"V{BcdToDecimal(ProtocolVersion) / 10.0:F1}";

    /// <summary>软件版本字符串</summary>
    public string SoftwareVersionString =>
        $"V{BcdToDecimal(SoftwareVersion[1])}.{BcdToDecimal(SoftwareVersion[0]) / 10.0:F1}";

    private static int BcdToDecimal(byte bcd) => ((bcd >> 4) & 0x0F) * 10 + (bcd & 0x0F);
}

/// <summary>
/// 表具状态字（4 字节，32 位）
/// <para>包含拆表、大流、恒流、小流、计量、阀门状态、闲置、反向、电压、压力、温度等状态标志。</para>
/// </summary>
public class MeterStatusWord
{
    /// <summary>原始 4 字节状态字</summary>
    public byte[] RawData { get; set; } = new byte[4];

    // ===== 第 0 字节 (B7~B0) =====

    /// <summary>B7: 拆表（0=正常, 1=异常）</summary>
    public bool TamperAlarm { get; set; }

    /// <summary>B6: 大流（0=正常, 1=异常）</summary>
    public bool HighFlowAlarm { get; set; }

    /// <summary>B5: 恒流（0=正常, 1=异常）</summary>
    public bool ConstantFlowAlarm { get; set; }

    /// <summary>B4: 小流（0=正常, 1=异常）</summary>
    public bool LowFlowAlarm { get; set; }

    /// <summary>B3: 计量（0=正常, 1=异常）</summary>
    public bool MeasurementAlarm { get; set; }

    /// <summary>B2~B0: 阀门状态（000=无, 001=开阀, 011=关阀, 101=异常, 111=阀门权关）</summary>
    public ValveStatus ValveState { get; set; }

    // ===== 第 1 字节 (B15~B8) =====

    /// <summary>B15: 闲置（0=正常, 1=异常）</summary>
    public bool IdleAlarm { get; set; }

    /// <summary>B14: 反向（0=正常, 1=异常）</summary>
    public bool ReverseAlarm { get; set; }

    /// <summary>B13~B12: 电压（00=正常, 01=欠压, 10=低压, 11=掉电）</summary>
    public VoltageStatus VoltageState { get; set; }

    /// <summary>B11: 压力失压（0=正常, 1=异常）</summary>
    public bool PressureLossAlarm { get; set; }

    /// <summary>B10: 压力超压（0=正常, 1=异常）</summary>
    public bool PressureOverAlarm { get; set; }

    /// <summary>B9: 压力传感器（0=正常, 1=异常）</summary>
    public bool PressureSensorAlarm { get; set; }

    /// <summary>B8: 温度传感器（0=正常, 1=异常）</summary>
    public bool TemperatureSensorAlarm { get; set; }

    // ===== 第 2 字节 (B23~B16) =====

    /// <summary>B19: 开盖（0=正常, 1=异常）</summary>
    public bool OpenCoverAlarm { get; set; }

    /// <summary>B18: 燃气泄漏（0=正常, 1=异常）</summary>
    public bool GasLeakAlarm { get; set; }

    /// <summary>B17: 日用气量上限（0=正常, 1=异常）</summary>
    public bool DailyUsageLimitAlarm { get; set; }

    /// <summary>B16: 失联（0=正常, 1=异常）</summary>
    public bool DisconnectAlarm { get; set; }
}

/// <summary>
/// 读表具参数返回的数据结构
/// <para>包含表具时间、通讯类型、表具类型、运行模式、表存上限、抄表时间、月冻结、网络参数等。</para>
/// </summary>
public class MeterParameters
{
    /// <summary>表具时间（6B BCD）</summary>
    public SystemClock MeterTime { get; set; } = new();

    /// <summary>通讯类型（0=移动流量, 1=电信流量）</summary>
    public CommunicationType CommType { get; set; }

    /// <summary>表具类型（0=霍尔, 1=光电, 2=超声波）</summary>
    public MeterType MeterType { get; set; }

    /// <summary>运行模式</summary>
    public RunMode RunMode { get; set; }

    /// <summary>表存上限（4B HEX，低位在前，2 位小数）</summary>
    public decimal StorageLimit { get; set; }

    /// <summary>预付费参数</summary>
    public byte[] PrepaidParams { get; set; } = Array.Empty<byte>();

    /// <summary>抄表时间参数（11B）</summary>
    public ReadingTimeParams ReadingTime { get; set; } = new();

    /// <summary>月冻结日参数（3B）</summary>
    public MonthlyFreezeParams MonthlyFreeze { get; set; } = new();

    /// <summary>网络配置参数（2+N+n）</summary>
    public NetworkConfig NetworkConfig { get; set; } = new();

    /// <summary>IP 地址配置参数（7B）</summary>
    public IpConfig IpConfig { get; set; } = new();

    /// <summary>模块终端号（20 ASCII）</summary>
    public string TerminalNumber { get; set; } = string.Empty;

    /// <summary>软件版本号（6B）</summary>
    public MeterVersion SoftwareVersion { get; set; } = new();

    /// <summary>唤醒周期是否生效（0=不生效, 1=生效）</summary>
    public bool WakeCycleEnabled { get; set; }

    /// <summary>唤醒周期参数（11B）</summary>
    public ReadingTimeParams WakeCycle { get; set; } = new();

    /// <summary>备用电池抄表时间（11B）</summary>
    public ReadingTimeParams BackupBatteryReadingTime { get; set; } = new();
}

/// <summary>
/// 读出厂配置参数返回的数据结构（4.1.2）
/// </summary>
public class FactoryConfigResponse
{
    /// <summary>表具编号（7B BCD，13 位，低位在前）</summary>
    public string MeterNumber { get; set; } = string.Empty;

    /// <summary>表具参数</summary>
    public MeterParameters MeterParameters { get; set; } = new();

    /// <summary>剩余金额（4B HEX，低位在前，2 位小数）</summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>表具出厂配置参数（14B）</summary>
    public byte[] FactoryParams { get; set; } = Array.Empty<byte>();

    /// <summary>表具底码（4B HEX，低位在前，2 位小数）</summary>
    public decimal BaseCode { get; set; }

    /// <summary>阀门配置参数（6B）</summary>
    public byte[] ValveConfig { get; set; } = Array.Empty<byte>();

    /// <summary>电源管理配置参数（10B）</summary>
    public byte[] PowerConfig { get; set; } = Array.Empty<byte>();

    /// <summary>表计费类型（0=金额式, 1=气量）</summary>
    public BillingType BillingType { get; set; }

    /// <summary>GPRS 模块状态（0xFF=无状态, 0x00=异常, 0x01=正常）</summary>
    public byte GprsModuleStatus { get; set; }

    /// <summary>信号强度值（0xFF=无信号值, 0x01~0x31=有效, 0x99=无信号）</summary>
    public byte SignalStrength { get; set; }

    /// <summary>TCP 连接状态（0xFF=无状态, 0x00=异常, 0x01=正常）</summary>
    public byte TcpConnectionStatus { get; set; }

    /// <summary>SIM 卡状态（0xFF=无状态, 0x00=异常, 0x01=正常）</summary>
    public byte SimCardStatus { get; set; }

    /// <summary>温度压力（10B: 温度状态1B + 压力状态1B + 温度值4B + 压力值4B）</summary>
    public TemperaturePressureData TemperaturePressure { get; set; } = new();

    /// <summary>IMSI 号（20 ASCII）</summary>
    public string Imsi { get; set; } = string.Empty;

    /// <summary>域名启用标志（0x20=启用域名）</summary>
    public byte DomainEnableFlag { get; set; }

    /// <summary>域名</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>域名端口</summary>
    public int DomainPort { get; set; }

    /// <summary>单价（4B HEX，低位在前，4 位小数）</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>卡类型（0x56=设置卡）</summary>
    public byte CardType { get; set; }

    /// <summary>安全表是否加密（0=否, 1=是）</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>表具规格</summary>
    public MeterSpecification MeterSpec { get; set; }

    /// <summary>温压传感器类型（2B: 温度传感器类型 + 压力传感器类型）</summary>
    public TempPressureSensorType SensorTypes { get; set; } = new();

    /// <summary>通讯电池电压（1B HEX，如 0x2D 表示 4.5V）</summary>
    public decimal CommBatteryVoltage { get; set; }

    /// <summary>计量电池电压（1B HEX，如 0x2D 表示 4.5V）</summary>
    public decimal MeasureBatteryVoltage { get; set; }

    /// <summary>表具状态字（4B）</summary>
    public MeterStatusWord StatusWord { get; set; } = new();
}

/// <summary>温度压力数据（10 字节）</summary>
public class TemperaturePressureData
{
    /// <summary>温度状态标志（0=正常, 1=异常）</summary>
    public bool TemperatureAbnormal { get; set; }

    /// <summary>压力状态标志（0=正常, 1=异常）</summary>
    public bool PressureAbnormal { get; set; }

    /// <summary>温度值（4B HEX，低位在前，2 位小数）</summary>
    public decimal Temperature { get; set; }

    /// <summary>压力值（4B HEX，低位在前，2 位小数）</summary>
    public decimal Pressure { get; set; }
}

/// <summary>温压传感器类型（2 字节）</summary>
public class TempPressureSensorType
{
    /// <summary>温度传感器类型</summary>
    public TemperatureSensorType TemperatureSensor { get; set; }

    /// <summary>压力传感器类型</summary>
    public PressureSensorType PressureSensor { get; set; }
}

/// <summary>
/// Onenet 平台参数（15 字节，4.2.3）
/// </summary>
public class OnenetPlatformParams
{
    /// <summary>ONENET 通信实例的引用 ID（2B，默认 0）</summary>
    public ushort ReferenceId { get; set; }

    /// <summary>对象标识（2B，默认 3202）</summary>
    public ushort ObjectId { get; set; }

    /// <summary>实例标识（2B，默认 0）</summary>
    public ushort InstanceId { get; set; }

    /// <summary>资源标识上行（2B，默认 5500）</summary>
    public ushort ResourceIdUp { get; set; }

    /// <summary>资源标识下行（2B，默认 1001）</summary>
    public ushort ResourceIdDown { get; set; }

    /// <summary>数据格式类型（1B，默认 1）</summary>
    public byte DataValueType { get; set; }

    /// <summary>数据帧结束标志（1B，默认 1）</summary>
    public byte Flag { get; set; }

    /// <summary>回应 ID（2B，默认 0）</summary>
    public ushort AckId { get; set; }

    /// <summary>Onenet 平台类型（0=各省平台, 1=全国平台）</summary>
    public byte PlatformType { get; set; }
}

/// <summary>
/// 表型号（2 字节）
/// <para>第一个字节: 表类型（0x10=皮膜燃气表, 0x20=流量计, 0x30=超声波）</para>
/// <para>第二个字节: 量程（0x00=2.5方, 0x01=4.0方, 0x02=6.0方, 0x03=10方, 0x04=16方, 0x05=25方, 0x06=1.6方, 0x07=40方, 0x08=65方）</para>
/// </summary>
public class MeterModel
{
    /// <summary>表类型</summary>
    public byte MeterTypeCode { get; set; }

    /// <summary>量程代码</summary>
    public byte RangeCode { get; set; }

    /// <summary>表类型描述</summary>
    public string MeterTypeDescription => MeterTypeCode switch
    {
        0x10 => "皮膜燃气表",
        0x20 => "流量计",
        0x30 => "超声波",
        _ => $"未知(0x{MeterTypeCode:X2})"
    };

    /// <summary>量程描述</summary>
    public string RangeDescription => RangeCode switch
    {
        0x00 => "2.5方",
        0x01 => "4.0方",
        0x02 => "6.0方",
        0x03 => "10方",
        0x04 => "16方",
        0x05 => "25方",
        0x06 => "1.6方",
        0x07 => "40方",
        0x08 => "65方",
        _ => $"未知(0x{RangeCode:X2})"
    };
}

/// <summary>
/// 读取加密及平台信息返回的数据结构（4.2.2）
/// </summary>
public class EncryptionPlatformInfoResponse
{
    /// <summary>表具编号（32 ASCII）</summary>
    public string MeterNumber { get; set; } = string.Empty;

    /// <summary>加密密钥长度（默认 24）</summary>
    public byte EncryptionKeyLength { get; set; }

    /// <summary>加密密钥（32 HEX）</summary>
    public byte[] EncryptionKey { get; set; } = Array.Empty<byte>();

    /// <summary>认证密钥长度（默认 24）</summary>
    public byte AuthKeyLength { get; set; }

    /// <summary>认证密钥（32 HEX）</summary>
    public byte[] AuthKey { get; set; } = Array.Empty<byte>();

    /// <summary>Onenet 平台参数（15 HEX）</summary>
    public OnenetPlatformParams OnenetParams { get; set; } = new();

    /// <summary>PSM 模式（0=掉电模式, 1=PSM 模式）</summary>
    public byte PsmMode { get; set; }

    /// <summary>反向累积量（4B HEX，低位在前，2 位小数）</summary>
    public decimal ReverseAccumulation { get; set; }

    /// <summary>表型号（2B）</summary>
    public MeterModel MeterModel { get; set; } = new();

    /// <summary>表具生产状态</summary>
    public ProductionStatus ProductionStatus { get; set; }

    /// <summary>加密芯片状态结果</summary>
    public EncryptionChipStatus ChipStatus { get; set; }

    /// <summary>保留（1B）</summary>
    public byte Reserved { get; set; }

    /// <summary>IMEI 号（15 ASCII）</summary>
    public string Imei { get; set; } = string.Empty;

    /// <summary>硬件加密芯片 ID 号（8 HEX）</summary>
    public byte[] HardwareChipId { get; set; } = Array.Empty<byte>();

    /// <summary>参数配置关阀类型（1B: B0 B1 = 00-不关阀, 01-普通关阀, 10-强制关阀）</summary>
    public byte ValveCloseType { get; set; }

    /// <summary>调齿比系数 K 值（4B HEX，低位在前，6 位小数）</summary>
    public decimal KValue { get; set; }

    /// <summary>调齿比系数 B 值（4B HEX，低位在前，6 位小数）</summary>
    public decimal BValue { get; set; }

    /// <summary>通讯板的网络类型</summary>
    public NetworkType NetworkType { get; set; }
}

/// <summary>
/// 配置参数返回的数据结构（4.3.2）
/// </summary>
public class ConfigurationParamsResponse
{
    /// <summary>甲烷低报报警参数（2B，百分比 %LEL）</summary>
    public ushort MethaneLowAlarm { get; set; }

    /// <summary>甲烷高报报警参数（2B，百分比 %LEL）</summary>
    public ushort MethaneHighAlarm { get; set; }

    /// <summary>压力传感器 1 参数（8B）</summary>
    public PressureSensorParams PressureSensor1 { get; set; } = new();

    /// <summary>压力传感器 2 参数（8B）</summary>
    public PressureSensorParams PressureSensor2 { get; set; } = new();

    /// <summary>温压传感器 1 配置（0=无, 1~3=有）</summary>
    public byte TempPressureSensor1Config { get; set; }

    /// <summary>温压传感器 2 配置（0=无, 1~3=有）</summary>
    public byte TempPressureSensor2Config { get; set; }

    /// <summary>风机配置（0=无, 1=有）</summary>
    public bool HasFan { get; set; }

    /// <summary>报警器配置（0=无, 1=有）</summary>
    public bool HasAlarm { get; set; }

    /// <summary>开盖配置（0=关闭, 1=开启）</summary>
    public bool OpenCoverEnabled { get; set; }
}

/// <summary>
/// 压力传感器参数（8 字节，4.3.3）
/// <para>型号(1B) + 类型(1B) + 量程(2B) + 保留(4B)</para>
/// </summary>
public class PressureSensorParams
{
    /// <summary>压力传感器型号（0=无, 1=MPM3801, 2=HP5834）</summary>
    public byte Model { get; set; }

    /// <summary>压力传感器类型（0=无, 1=差压, 2=绝压）</summary>
    public byte Type { get; set; }

    /// <summary>压力传感器量程（单位 hPa/百帕）</summary>
    public ushort Range { get; set; }

    /// <summary>保留（4B）</summary>
    public byte[] Reserved { get; set; } = new byte[4];

    /// <summary>型号描述</summary>
    public string ModelDescription => Model switch
    {
        0 => "无",
        1 => "MPM3801",
        2 => "HP5834",
        _ => $"未知(0x{Model:X2})"
    };

    /// <summary>类型描述</summary>
    public string TypeDescription => Type switch
    {
        0 => "无",
        1 => "差压",
        2 => "绝压",
        _ => $"未知(0x{Type:X2})"
    };
}

/// <summary>
/// 读取网络参数及终端号返回的数据结构（4.4）
/// </summary>
public class NetworkParamsTerminalInfo
{
    /// <summary>IMEI 号（15 ASCII）</summary>
    public string Imei { get; set; } = string.Empty;

    /// <summary>IMSI 号（15 ASCII）</summary>
    public string Imsi { get; set; } = string.Empty;

    /// <summary>ICCID 号（20 ASCII）</summary>
    public string Iccid { get; set; } = string.Empty;

    /// <summary>CSQ 值（1B HEX）</summary>
    public byte Csq { get; set; }

    /// <summary>RSRP 值（2B HEX，低位在前，0x7EFF 表示 -130）</summary>
    public short Rsrp { get; set; }

    /// <summary>SNR 值（2B HEX，低位在前，0x7EFF 表示 -130）</summary>
    public short Snr { get; set; }

    /// <summary>ECL 信号等级（0~6）</summary>
    public byte Ecl { get; set; }

    /// <summary>CellID 小区号（4B HEX）</summary>
    public uint CellId { get; set; }

    /// <summary>第二张卡 ICCID 号（20 ASCII）</summary>
    public string Iccid2 { get; set; } = string.Empty;

    /// <summary>第二张卡 IMSI 号（15 ASCII）</summary>
    public string Imsi2 { get; set; } = string.Empty;

    /// <summary>第二张卡 CSQ 值（1B HEX）</summary>
    public byte Csq2 { get; set; }

    /// <summary>软件版本编译时间（6B BCD: YYMMDDhhmmss）</summary>
    public SystemClock SoftwareCompileTime { get; set; } = new();

    /// <summary>外部压力传感状态（0=正常, 1=异常）</summary>
    public bool ExternalPressureAbnormal { get; set; }

    /// <summary>外部压力传感器值（4B HEX，低位在前，2 位小数）</summary>
    public decimal ExternalPressureValue { get; set; }

    /// <summary>蓝牙模块 SN 号（12 ASCII）</summary>
    public string BluetoothSn { get; set; } = string.Empty;
}

/// <summary>
/// 读取设备运行信息返回的数据结构（4.5）
/// </summary>
public class DeviceRunningInfo
{
    /// <summary>报警器状态（0=已触发, 1=未触发；开关量报警器）</summary>
    public bool AlarmTriggered { get; set; }

    /// <summary>报警器浓度（百分比 0%~100%，单位 %LEL；4~20mA 报警器）</summary>
    public byte AlarmConcentration { get; set; }

    /// <summary>RS485 接口状态（0=异常, 1=正常）</summary>
    public bool Rs485Normal { get; set; }

    /// <summary>蓝牙连接状态（0=未连接, 1=已连接）</summary>
    public bool BluetoothConnected { get; set; }
}

/// <summary>
/// 蓝牙控制结果（上行应答，4.6）
/// </summary>
public class BluetoothControlResult
{
    /// <summary>操作结果（0=成功, 1=不支持）</summary>
    public bool Success { get; set; }

    /// <summary>保留（4B）</summary>
    public byte[] Reserved { get; set; } = new byte[4];
}

/// <summary>
/// 写出厂配置参数请求数据（4.1.1）
/// <para>包含多个"是否更改"标志 + 参数块，仅序列化需要更改的字段。</para>
/// </summary>
public class WriteFactoryConfigRequest
{
    /// <summary>是否恢复出厂设置（0=不更改, 1=更改）</summary>
    public bool ResetToFactory { get; set; }

    /// <summary>是否更改表号</summary>
    public bool ChangeMeterNumber { get; set; }

    /// <summary>表具编号（13 位 BCD，7B，低位在前）</summary>
    public string? MeterNumber { get; set; }

    /// <summary>是否更改表具时间</summary>
    public bool ChangeMeterTime { get; set; }

    /// <summary>表具时间（6B BCD: YYMMDDhhmmss）</summary>
    public DateTime? MeterTime { get; set; }

    /// <summary>是否更改表具出厂参数</summary>
    public bool ChangeFactoryParams { get; set; }

    /// <summary>表具出厂配置参数（14B）</summary>
    public byte[]? FactoryParams { get; set; }

    /// <summary>是否更改表具底码</summary>
    public bool ChangeBaseCode { get; set; }

    /// <summary>表具底码（4B HEX，2 位小数）</summary>
    public decimal? BaseCode { get; set; }

    /// <summary>是否更改运行模式</summary>
    public bool ChangeRunMode { get; set; }

    /// <summary>运行模式</summary>
    public RunMode? RunMode { get; set; }

    /// <summary>预充金额/排空量（4B HEX，2 位小数）</summary>
    public decimal? PrepaidAmount { get; set; }

    /// <summary>是否更改表存上限</summary>
    public bool ChangeStorageLimit { get; set; }

    /// <summary>表存上限（4B HEX，2 位小数）</summary>
    public decimal? StorageLimit { get; set; }

    /// <summary>是否更改域名</summary>
    public bool ChangeDomain { get; set; }

    /// <summary>启用标志（0x20=启用域名, 其他=启用IP）</summary>
    public byte? DomainEnableFlag { get; set; }

    /// <summary>域名（ASCII，最长 30 字节）</summary>
    public string? Domain { get; set; }

    /// <summary>域名端口</summary>
    public int? DomainPort { get; set; }

    /// <summary>信息安全是否加密</summary>
    public bool ChangeEncryption { get; set; }

    /// <summary>是否加密（0=不加密, 1=加密）</summary>
    public bool? IsEncrypted { get; set; }

    /// <summary>是否更改表具规格</summary>
    public bool ChangeMeterSpec { get; set; }

    /// <summary>表具规格</summary>
    public MeterSpecification? MeterSpec { get; set; }
}

/// <summary>
/// 蓝牙控制请求参数（4.6 下行）
/// </summary>
public class BluetoothControlRequest
{
    /// <summary>操作类型</summary>
    public BluetoothOperation Operation { get; set; }

    /// <summary>表号（5B，当操作为搜索设备时使用）</summary>
    public byte[]? TargetMeterNumber { get; set; }
}

/// <summary>
/// 设置加密及平台信息请求数据（4.2.1，数据标识 0xC519）
/// <para>包含多个"是否更改"标志 + 参数块，仅序列化需要更改的字段。</para>
/// <para>字段顺序严格遵循协议 4.2.1 节定义。</para>
/// </summary>
public class WriteEncryptionInfoRequest
{
    /// <summary>是否更改表具编号</summary>
    public bool ChangeMeterNumber { get; set; }

    /// <summary>表具编号（32B ASCII，有效位 17 位或 16 位，不足补 '0'）</summary>
    public string? MeterNumber { get; set; }

    /// <summary>是否更改加密密钥</summary>
    public bool ChangeEncryptionKey { get; set; }

    /// <summary>加密密钥长度（默认 24）</summary>
    public byte EncryptionKeyLength { get; set; } = 24;

    /// <summary>加密密钥（32B HEX）</summary>
    public byte[]? EncryptionKey { get; set; }

    /// <summary>是否更改认证密钥</summary>
    public bool ChangeAuthKey { get; set; }

    /// <summary>认证密钥长度（默认 24）</summary>
    public byte AuthKeyLength { get; set; } = 24;

    /// <summary>认证密钥（32B HEX）</summary>
    public byte[]? AuthKey { get; set; }

    /// <summary>是否更改 Onenet 平台参数</summary>
    public bool ChangeOnenetParams { get; set; }

    /// <summary>Onenet 平台参数（15B HEX）</summary>
    public OnenetPlatformParams? OnenetParams { get; set; }

    /// <summary>是否更改进入 PSM 模式</summary>
    public bool ChangePsmMode { get; set; }

    /// <summary>PSM 模式（0=掉电模式, 1=PSM 模式）</summary>
    public byte? PsmMode { get; set; }

    /// <summary>是否更改反向累积量</summary>
    public bool ChangeReverseAccumulation { get; set; }

    /// <summary>反向累积量（4B HEX，低位在前，2 位小数）</summary>
    public decimal? ReverseAccumulation { get; set; }

    /// <summary>是否更改表型号</summary>
    public bool ChangeMeterModel { get; set; }

    /// <summary>表型号（2B: 表类型 1B + 量程 1B）</summary>
    public MeterModel? MeterModel { get; set; }

    /// <summary>表具生产状态（0=工厂态, 1=出厂态, 2=未校表, 3=已校表, 4=校表出厂）</summary>
    public ProductionStatus? ProductionStatus { get; set; }

    /// <summary>参数配置开关（B0=通讯电池低压关阀使能, B1~B7=保留）</summary>
    public byte? ConfigSwitch { get; set; }

    /// <summary>参数配置关阀类型（B0B1: 00=不关阀, 01=普通关阀, 10=强制关阀）</summary>
    public byte? ValveCloseType { get; set; }

    /// <summary>是否更改调齿比系数（K/B 值）</summary>
    public bool ChangeKBValue { get; set; }

    /// <summary>调齿比系数 K 值（4B HEX，低位在前，6 位小数）</summary>
    public decimal? KValue { get; set; }

    /// <summary>调齿比系数 B 值（4B HEX，低位在前，6 位小数）</summary>
    public decimal? BValue { get; set; }

    /// <summary>是否更改通讯板的网络类型</summary>
    public bool ChangeNetworkType { get; set; }

    /// <summary>通讯板的网络类型（0=TCP, 1=UDP, 2=COAP, 3=MQTT, 4=LwM2M(OneNet), 5=LwM2M(AEP)）</summary>
    public NetworkType? NetworkType { get; set; }
}
