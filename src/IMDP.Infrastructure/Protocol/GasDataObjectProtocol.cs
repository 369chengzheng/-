using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 燃气 C5F0 单数据对象读写；仅处理协议字节，不依赖插件、方案或界面
/// 公共帧和对象格式依据 V2.1.9，加密启用时沿用既有报警主机的内容区 AES-ECB/PKCS7
/// </summary>
public sealed class GasDataObjectProtocol
{
    /// <summary>
    /// 公共头中的两字节数据域长度，按小端读写
    /// </summary>
    private static readonly FieldConfig LengthField = new() { Offset = 9, Length = 2 };

    /// <summary>
    /// 单对象内容中的两字节对象编号，位于对象数量之后
    /// </summary>
    private static readonly FieldConfig ObjectIdField = new() { Offset = 1, Length = 2 };

    /// <summary>
    /// 已明确线序的六字节设备地址
    /// </summary>
    private readonly byte[] _address;

    /// <summary>
    /// 由产品传入的内容区密钥；空值表示明文，不在公共协议中内置产品密钥
    /// </summary>
    private readonly byte[]? _encryptionKey;

    public GasDataObjectProtocol(byte[] address, byte[]? encryptionKey = null)
    {
        if (address.Length != 6) throw new ArgumentException("燃气设备地址必须为六字节。", nameof(address));
        _address = address.ToArray();
        _encryptionKey = encryptionKey?.ToArray();
    }

    /// <summary>
    /// 读取一个对象，系统时钟使用本机当前时间，序号为零
    /// </summary>
    public byte[] BuildReadFrame(ushort objectId)
    {
        byte[] content = new byte[3];
        content[0] = 1;
        FrameComposer.WriteFieldLittleEndian(content, ObjectIdField, objectId);
        return BuildFrame(0x01, content);
    }

    /// <summary>
    /// 设置一个对象；单对象应答结果固定为一字节，避免套用有歧义的多对象结果长度
    /// </summary>
    public byte[] BuildWriteFrame(ushort objectId, byte[] value)
    {
        byte[] content = new byte[3 + value.Length];
        content[0] = 1;
        FrameComposer.WriteFieldLittleEndian(content, ObjectIdField, objectId);
        value.CopyTo(content, 3);
        return BuildFrame(0x04, content);
    }

    /// <summary>
    /// 共用头、时钟、加密及和校验；前导由设备通信配置负责，不写进协议帧体
    /// </summary>
    private byte[] BuildFrame(byte control, byte[] content)
    {
        if (_encryptionKey != null) content = ProtocolCryptoHelper.AesEcbEncrypt(content, _encryptionKey);
        var now = DateTime.Now;
        byte[] header = [Bcd(now.Year % 100), Bcd(now.Month), Bcd(now.Day), Bcd(now.Hour), Bcd(now.Minute), Bcd(now.Second), 0, 0xF0, 0xC5, 0];
        var frame = new byte[content.Length + 23];
        frame[0] = frame[7] = 0x68;
        _address.CopyTo(frame, 1);
        frame[8] = control;
        // C5F0 明确选择公共小端方法，原有组帧器的大端入口不参与分流。
        FrameComposer.WriteFieldLittleEndian(frame, LengthField, header.Length + content.Length);
        header.CopyTo(frame, 11);
        content.CopyTo(frame, 21);
        frame[^2] = ChecksumCalculator.Calculate(frame[..^2], ChecksumType.Sum8)[0];
        frame[^1] = 0x16;
        return frame;
    }

    /// <summary>
    /// 从含噪声、半帧或粘包的快照中提取完整且和校验正确的帧，供公共匹配缓冲消费
    /// </summary>
    public static byte[]? TryExtractFrame(byte[] buffer)
    {
        for (var offset = 0; offset + 11 <= buffer.Length; offset++)
        {
            if (buffer[offset] != 0x68 || buffer[offset + 7] != 0x68) continue;
            var length = FrameComposer.ReadFieldLittleEndian(buffer, offset, LengthField);
            if (length < 10) continue;
            var frameLength = length + 13;
            if (frameLength > buffer.Length - offset || buffer[offset + frameLength - 1] != 0x16) continue;
            var frame = buffer.AsSpan(offset, frameLength).ToArray();
            if (ChecksumCalculator.Verify(frame[..^2], [frame[^2]], ChecksumType.Sum8)) return frame;
        }
        return null;
    }

    /// <summary>
    /// 匹配 C5F0 读应答；生产零地址请求接受设备实际地址，指定地址请求仍逐字节核对
    /// 跳过上报、写回包和其他对象；异常应答交给解码明确报错
    /// </summary>
    public bool MatchesReadResponse(byte[] frame, ushort objectId)
        => MatchesResponse(frame, objectId, 0x81);

    /// <summary>
    /// 按同一地址及对象规则匹配单对象写应答，与读取和上报分开
    /// </summary>
    public bool MatchesWriteResponse(byte[] frame, ushort objectId)
        => MatchesResponse(frame, objectId, 0x84);

    /// <summary>
    /// 读写共用匹配规则；异常应答交由解码器给出失败，不等待到超时
    /// </summary>
    private bool MatchesResponse(byte[] frame, ushort objectId, byte control)
    {
        if (frame.Length < 23 ||
            (_address.Any(value => value != 0) && !frame.AsSpan(1, 6).SequenceEqual(_address)) ||
            (frame[8] & 0x9F) != control || frame[17] != 0 || frame[18] != 0xF0 || frame[19] != 0xC5) return false;
        if ((frame[8] & 0x40) != 0) return true;
        try
        {
            var content = ReadContent(frame);
            return content.Length < 3 || FrameComposer.ReadFieldLittleEndian(content, 0, ObjectIdField) == objectId;
        }
        catch (System.Security.Cryptography.CryptographicException) { return true; }
    }

    /// <summary>
    /// 解出本次单对象值；错误控制码、分帧、对象数量或编号不符均不作为成功结果
    /// </summary>
    public byte[] ReadObjectResponse(byte[] frame, ushort objectId)
    {
        var parsed = TryExtractFrame(frame);
        if (parsed == null || parsed.Length != frame.Length)
            throw new FormatException("燃气读取应答帧不完整或校验失败。");
        if (!MatchesReadResponse(frame, objectId)) throw new FormatException("应答地址、命令或对象与本次读取不符。");
        if ((frame[8] & 0x40) != 0) throw new FormatException("设备返回读取异常应答。");
        if (frame[8] != 0x81 || frame[20] != 0) throw new FormatException("本次单对象读取不接受后续分帧。");
        var content = ReadContent(frame);
        if (content.Length < 3 || content[0] != 1 || FrameComposer.ReadFieldLittleEndian(content, 0, ObjectIdField) != objectId)
            throw new FormatException("读取应答的对象数量或编号不符。");
        return content[3..];
    }

    /// <summary>
    /// 核对单对象写应答并返回原始结果码；旧设备的空内容应答返回空值
    /// 是否按回读最终值判定由产品流程决定，非零码不改写为成功
    /// </summary>
    public byte? ReadWriteResponse(byte[] frame, ushort objectId)
    {
        var parsed = TryExtractFrame(frame);
        if (parsed == null || parsed.Length != frame.Length || !MatchesWriteResponse(frame, objectId))
            throw new FormatException("燃气设置应答帧不完整、校验失败或对象不符。");
        if ((frame[8] & 0x40) != 0) throw new FormatException("设备返回设置异常应答。");
        if (frame[8] != 0x84 || frame[20] != 0) throw new FormatException("本次单对象设置不接受后续分帧。");
        var content = ReadContent(frame);
        if (content.Length == 0) return null;
        if (content.Length != 4 || content[0] != 1 || FrameComposer.ReadFieldLittleEndian(content, 0, ObjectIdField) != objectId)
            throw new FormatException("设置应答的对象数量、编号或结果长度不符。");
        return content[3];
    }

    /// <summary>
    /// 公共十字节头保留明文，只对其后的对象内容区解密，并由公共 AES 严格检查填充
    /// </summary>
    private byte[] ReadContent(byte[] frame)
    {
        var content = frame[21..^2];
        return _encryptionKey == null || content.Length == 0 ? content : ProtocolCryptoHelper.AesEcbDecrypt(content, _encryptionKey);
    }

    /// <summary>
    /// 将当前时钟的单个分量转为 BCD
    /// </summary>
    private static byte Bcd(int value) => (byte)((value / 10 << 4) | value % 10);
}
