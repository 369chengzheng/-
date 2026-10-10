// =============================================================================
// 文件名：ProtocolTypes.cs
// 功能描述：协议相关类型定义，包含协议标识、校验算法、字段配置、帧格式配置与帧解析结果。
//           作为帧解析器与协议管理器的共享数据契约。
// =============================================================================
namespace IMDP.Domain.Products;

/// <summary>
/// 协议标识（通过名称+版本唯一识别）
/// </summary>
public struct ProtocolIdentifier : IEquatable<ProtocolIdentifier>
{
    /// <summary>协议名称（如 Viewshine）</summary>
    public string Name { get; set; }

    /// <summary>协议版本（如 1.0.0）</summary>
    public string Version { get; set; }

    /// <summary>协议描述文本（用于 UI 展示与日志）</summary>
    public string Description { get; set; }

    /// <summary>判断是否与另一个协议标识相等（按名称+版本比较）</summary>
    /// <param name="other">另一个协议标识</param>
    /// <returns>名称与版本均相同返回 true，否则 false</returns>
    public bool Equals(ProtocolIdentifier other)
    {
        return string.Equals(Name, other.Name) && string.Equals(Version, other.Version);
    }

    /// <summary>判断是否与指定对象相等</summary>
    /// <param name="obj">待比较对象</param>
    /// <returns>对象为 ProtocolIdentifier 且名称与版本均相同返回 true，否则 false</returns>
    public override bool Equals(object? obj) => obj is ProtocolIdentifier other && Equals(other);

    /// <summary>获取哈希值（基于名称与版本组合）</summary>
    /// <returns>哈希值</returns>
    public override int GetHashCode() => HashCode.Combine(Name, Version);

    /// <summary>返回协议标识的字符串表示（含描述时附带描述）</summary>
    /// <returns>协议标识字符串</returns>
    public override string ToString() => string.IsNullOrEmpty(Description)
        ? $"{Name} v{Version}"
        : $"{Description} ({Name} v{Version})";

    /// <summary>相等运算符</summary>
    public static bool operator ==(ProtocolIdentifier left, ProtocolIdentifier right) => left.Equals(right);

    /// <summary>不等运算符</summary>
    public static bool operator !=(ProtocolIdentifier left, ProtocolIdentifier right) => !left.Equals(right);
}

/// <summary>
/// 校验算法类型枚举
/// </summary>
public enum ChecksumType
{
    /// <summary>无校验</summary>
    None,

    /// <summary>8 位累加和</summary>
    Sum8,

    /// <summary>8 位异或</summary>
    Xor8,

    /// <summary>CRC-8</summary>
    Crc8,

    /// <summary>CRC-16 Modbus</summary>
    Crc16Modbus,

    /// <summary>CRC-16 CCITT</summary>
    Crc16Ccitt,

    /// <summary>8 位纵向冗余校验（LRC）</summary>
    Lrc8
}

/// <summary>
/// 长度字段语义枚举
/// </summary>
public enum LengthFieldMode
{
    /// <summary>长度字段表示“数据区长度”（不含帧头/帧尾/校验等，默认行为）。</summary>
    DataLength,

    /// <summary>长度字段表示“整帧总长度”（含帧头/长度字段/帧尾/校验等全部字节，如 G2-SAGA 的 0x13）。</summary>
    TotalFrameLength
}

/// <summary>
/// 字段配置（位置和长度）
/// </summary>
public class FieldConfig
{
    /// <summary>字段在帧中的字节偏移</summary>
    public int Offset { get; set; }

    /// <summary>字段字节长度</summary>
    public int Length { get; set; }
}

/// <summary>
/// 帧格式配置
/// </summary>
public class FrameFormatConfig
{
    /// <summary>帧头字节序列（默认 0xAA 0x55）</summary>
    public byte[] FrameHeader { get; set; } = new byte[] { 0xAA, 0x55 };

    /// <summary>帧尾字节序列（可空表示无帧尾）</summary>
    public byte[]? FrameFooter { get; set; }

    /// <summary>地址字段配置（默认偏移 2、长度 1）</summary>
    public FieldConfig AddressField { get; set; } = new FieldConfig { Offset = 2, Length = 1 };

    /// <summary>命令字段配置（可空表示无独立命令字段）</summary>
    public FieldConfig? CommandField { get; set; }

    /// <summary>长度字段配置（默认偏移 3、长度 1）</summary>
    public FieldConfig LengthField { get; set; } = new FieldConfig { Offset = 3, Length = 1 };

    /// <summary>
    /// 长度字段语义（默认 <see cref="LengthFieldMode.DataLength"/>，即长度字段=数据区长度）。
    /// <para>设为 <see cref="LengthFieldMode.TotalFrameLength"/> 时，长度字段写入/解析为整帧总长度，
    /// 用于“长度在前、帧头/地址/命令位于长度之后”的布局（如 G2-SAGA）。</para>
    /// </summary>
    public LengthFieldMode LengthFieldMode { get; set; } = LengthFieldMode.DataLength;

    /// <summary>
    /// 命令字段固定值（可空）。设置后组帧时命令字段写入该常量，而非取 command 首字节；
    /// 适用于命令前缀为固定字节、真正功能码位于数据区的协议（如 G2-SAGA 的命令字节 0x5A）。
    /// 为空时沿用旧行为：取 command[0] 作为命令字段值。
    /// </summary>
    public byte? CommandFieldValue { get; set; }

    /// <summary>
    /// 响应匹配时的命令比对掩码（可空）。非空时 <see cref="FrameComposer.IsResponseMatch"/>
    /// 对请求/响应帧的命令字段做 (value &amp; mask) 比较；为空表示不比对命令（仅比对帧头与地址，向后兼容）。
    /// 例如 0xFF 表示精确比对；当响应命令位含标志位（如 0x5A 与 0xEA）时可用掩码只比有效位。
    /// </summary>
    public byte? CommandMatchMask { get; set; }

    /// <summary>固定帧长度（0 表示可变长帧，由长度字段决定）</summary>
    public int FixedFrameLength { get; set; } = 0;

    /// <summary>校验算法类型（默认 CRC-16 Modbus）</summary>
    public ChecksumType ChecksumType { get; set; } = ChecksumType.Crc16Modbus;
}

/// <summary>
/// 帧解析结果
/// </summary>
public class FrameParseResult
{
    /// <summary>帧是否完整（true 表示已凑齐一帧完整数据）</summary>
    public bool IsComplete { get; set; }

    /// <summary>解析出的完整帧数据（帧不完整时为 null）</summary>
    public byte[]? FrameData { get; set; }

    /// <summary>本次解析消费的字节数（用于从缓冲区移除已处理数据）</summary>
    public int ConsumedLength { get; set; }

    /// <summary>帧中的设备地址</summary>
    public byte DeviceAddress { get; set; }

    /// <summary>帧中的命令码</summary>
    public byte CommandCode { get; set; }

    /// <summary>帧中的数据域（可空）</summary>
    public byte[]? Data { get; set; }

    /// <summary>解析错误信息（解析失败时填充）</summary>
    public string? ErrorMessage { get; set; }
}
