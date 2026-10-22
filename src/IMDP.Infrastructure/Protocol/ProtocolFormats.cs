// =============================================================================
// 文件名：ProtocolFormats.cs
// 功能描述：常用协议帧格式命名常量表。
//           把 FrameFormatConfig 的手写字面量收敛为命名只读实例，
//           供组帧/解帧/串口会话门面/自测用例统一引用，避免各调用点重复定义。
//           注意：实例为共享只读对象，调用方不得修改其属性；
//           需要定制时请用 Clone() 派生新实例。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 常用协议帧格式命名常量表
/// <para>每个属性对应一种已验证的帧布局；新增协议布局时在此追加命名项，
/// 而不是在调用点手写 FrameFormatConfig 字面量。</para>
/// </summary>
public static class ProtocolFormats
{
    /// <summary>
    /// G2-SAGA 系基础布局（无唤醒前导）：
    /// 68 | len(整帧总长) | 工位 | 命令 | 数据区 | Sum8 | 16。
    /// <para>典型帧：68 13 00 5A 01 31..3C 64 16（写表号）、68 06 00 D0 3E 16（单命令码）。
    /// 单命令码场景请用 Clone() 后置 CommandIsPayload=true。</para>
    /// </summary>
    public static FrameFormatConfig G2Saga { get; } = new()
    {
        FrameHeader = new byte[] { 0x68 },
        FrameFooter = new byte[] { 0x16 },
        AddressField = new FieldConfig { Offset = 2, Length = 1 },
        CommandField = new FieldConfig { Offset = 3, Length = 1 },
        LengthField = new FieldConfig { Offset = 1, Length = 1 },
        LengthFieldMode = LengthFieldMode.TotalFrameLength,
        ChecksumType = ChecksumType.Sum8
    };

    /// <summary>
    /// G2-SAGA 唤醒前导版（海外二代表读参数）：
    /// [549×0x55 唤醒 + 5×0xFE 同步] 68 | len(整帧总长) | 工位 | 0x5A | 数据区 | Sum8 | 16。
    /// <para>命令字节固定 0x5A 前缀，功能码位于数据区（如 63 55 00 读参数）；
    /// 前导不参与长度字段与校验码计算。</para>
    /// </summary>
    public static FrameFormatConfig G2SagaWakeup549 { get; } = new()
    {
        FrameHeader = new byte[] { 0x68 },
        FrameFooter = new byte[] { 0x16 },
        AddressField = new FieldConfig { Offset = 2, Length = 1 },
        CommandField = new FieldConfig { Offset = 3, Length = 1 },
        CommandFieldValue = 0x5A,
        Preamble = new byte[] { 0x55 },
        PreambleRepeat = 549,
        PreambleSuffix = new byte[] { 0xFE, 0xFE, 0xFE, 0xFE, 0xFE },
        LengthField = new FieldConfig { Offset = 1, Length = 1 },
        LengthFieldMode = LengthFieldMode.TotalFrameLength,
        ChecksumType = ChecksumType.Sum8
    };

    /// <summary>
    /// 返回配置的浅拷贝（允许调用方在其上安全定制字段）。
    /// </summary>
    public static FrameFormatConfig Clone(this FrameFormatConfig source) => new()
    {
        FrameHeader = source.FrameHeader,
        FrameFooter = source.FrameFooter,
        AddressField = source.AddressField,
        CommandField = source.CommandField,
        LengthField = source.LengthField,
        LengthFieldMode = source.LengthFieldMode,
        CommandFieldValue = source.CommandFieldValue,
        Preamble = source.Preamble,
        PreambleRepeat = source.PreambleRepeat,
        PreambleSuffix = source.PreambleSuffix,
        CommandIsPayload = source.CommandIsPayload,
        CommandMatchMask = source.CommandMatchMask,
        FixedFrameLength = source.FixedFrameLength,
        ChecksumType = source.ChecksumType,
        PreambleGapMs = source.PreambleGapMs
    };
}
