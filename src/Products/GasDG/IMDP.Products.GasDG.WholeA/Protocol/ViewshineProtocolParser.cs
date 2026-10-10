// =============================================================================
// 文件名：ViewshineProtocolParser.cs
// 功能描述：威星物联网燃气表生产测试通讯协议 V1.0.0 帧解析器。
//           负责协议帧的构建、解析（支持粘包/拆包）、数据域拆分及 BCD/小端序/ASCII
//           等编码工具方法，是 GasDG.WholeA 协议层的核心组件。
// =============================================================================
using System.Globalization;
using IMDP.Domain.Products;
using IMDP.Infrastructure.Protocol;

namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 威星物联网燃气表生产测试通讯协议帧解析器 V1.0.0
/// <para>
/// 帧格式: [0x68] [A5..A0(6B BCD)] [0x68] [C(1B)] [L0 L1(2B)] [数据域(L字节)] [CS(1B)] [0x16]
/// </para>
/// <para>
/// 与通用 <see cref="FrameParserBase"/> 的差异：
/// <list type="bullet">
///   <item>帧头 0x68 出现两次（偏移 0 和偏移 7）</item>
///   <item>目标地址为 6 字节 BCD 编码（低位在前）</item>
///   <item>数据域长度为 2 字节（低字节在前，高字节在后）</item>
///   <item>校验码为 Sum8（从第一个 0x68 到校验码前所有字节的和模 256）</item>
///   <item>帧尾固定为 0x16</item>
/// </list>
/// </para>
/// <example>
/// 示例：解析一个完整的协议帧
/// <code>
/// var parser = new ViewshineProtocolParser();
/// byte[] buffer = new byte[] { 0x68, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x68, 0x01, 0x0A, 0x00, /* 数据域 10 字节 */, /* CS */, 0x16 };
/// var result = parser.TryParseFrame(buffer, 0, buffer.Length);
/// if (result.IsComplete)
/// {
///     Console.WriteLine($"控制码: 0x{result.CommandCode:X2}");
///     Console.WriteLine($"数据长度: {result.Data?.Length}");
/// }
/// </code>
/// </example>
/// </summary>
public class ViewshineProtocolParser : FrameParserBase
{
    /// <summary>协议标识</summary>
    public override ProtocolIdentifier ProtocolId => new()
    {
        Name = "Viewshine_GasDG_ProductionTest",
        Version = "1.0.0",
        Description = "威星物联网燃气表生产测试通讯协议"
    };

    /// <summary>
    /// 帧格式配置（供基类参考，实际解析逻辑在重写方法中实现）
    /// <para>注意：由于协议帧头出现两次，基类 <see cref="FrameParserBase"/> 无法直接处理，
    /// 需通过重写 <see cref="TryParseFrame"/> 和 <see cref="BuildFrame"/> 实现完整逻辑。</para>
    /// </summary>
    public override FrameFormatConfig FrameFormat => new()
    {
        FrameHeader = new byte[] { ViewshineProtocolConstants.FrameStart },
        AddressField = new FieldConfig
        {
            Offset = ViewshineProtocolConstants.OffsetAddress,
            Length = ViewshineProtocolConstants.AddressLength
        },
        CommandField = new FieldConfig
        {
            Offset = ViewshineProtocolConstants.OffsetControlCode,
            Length = 1
        },
        LengthField = new FieldConfig
        {
            Offset = ViewshineProtocolConstants.OffsetDataLengthLow,
            Length = 2
        },
        ChecksumType = ChecksumType.Sum8,
        FrameFooter = new byte[] { ViewshineProtocolConstants.FrameEnd }
    };

    /// <summary>协议描述</summary>
    public override string Description => "威星物联网燃气表生产测试通讯协议 V1.0.0";

    /// <summary>
    /// 尝试从缓冲区解析一个完整的协议帧（支持粘包/拆包）
    /// <para>
    /// 解析步骤：
    /// 1. 检查第一个帧起始符 0x68
    /// 2. 读取 6 字节 BCD 目标地址
    /// 3. 检查第二个帧起始符 0x68
    /// 4. 读取控制码
    /// 5. 读取 2 字节数据域长度（低位在前）
    /// 6. 检查数据是否足够容纳完整帧
    /// 7. 验证 Sum8 和校验
    /// 8. 提取数据域
    /// 9. 检查帧结束符 0x16
    /// </para>
    /// </summary>
    /// <param name="buffer">接收缓冲区</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="length">可用数据长度</param>
    /// <returns>解析结果，<see cref="FrameParseResult.IsComplete"/> 为 true 时表示成功解析完整帧</returns>
    public override FrameParseResult TryParseFrame(byte[] buffer, int offset, int length)
    {
        // 步骤 1: 检查最小帧长度（帧头1 + 地址6 + 帧头2 1 + 控制码1 + 长度2 + 校验1 + 帧尾1 = 13）
        const int minFrameLength = 13;
        if (length < minFrameLength)
            return new FrameParseResult { IsComplete = false, ErrorMessage = "数据不足最小帧长度" };

        // 步骤 2: 验证第一个帧起始符
        if (buffer[offset + ViewshineProtocolConstants.OffsetFirstStart] != ViewshineProtocolConstants.FrameStart)
            return new FrameParseResult { IsComplete = false, ErrorMessage = "第一个帧起始符不匹配" };

        // 步骤 3: 验证第二个帧起始符
        if (buffer[offset + ViewshineProtocolConstants.OffsetSecondStart] != ViewshineProtocolConstants.FrameStart)
            return new FrameParseResult { IsComplete = false, ErrorMessage = "第二个帧起始符不匹配" };

        // 步骤 4: 读取控制码
        byte controlCode = buffer[offset + ViewshineProtocolConstants.OffsetControlCode];

        // 步骤 5: 读取数据域长度（2 字节，低位在前）
        int dataLength = buffer[offset + ViewshineProtocolConstants.OffsetDataLengthLow]
                       | (buffer[offset + ViewshineProtocolConstants.OffsetDataLengthHigh] << 8);

        // 步骤 6: 计算帧总长度 = 11(固定头) + L(数据域) + 1(校验) + 1(帧尾)
        int frameLength = ViewshineProtocolConstants.OffsetDataDomain + dataLength + 2;

        // 检查数据是否足够
        if (length < frameLength)
            return new FrameParseResult { IsComplete = false, ErrorMessage = "数据不足完整帧长度" };

        // 步骤 7: 提取完整帧数据
        var frameData = new byte[frameLength];
        Array.Copy(buffer, offset, frameData, 0, frameLength);

        // 步骤 8: 验证 Sum8 和校验
        // 校验范围：从第一个帧起始符到校验码之前的所有字节
        int checksumOffset = frameLength - 2; // 倒数第 2 字节是校验码
        byte calculatedChecksum = CalculateSum8(frameData, 0, checksumOffset);
        byte receivedChecksum = frameData[checksumOffset];

        if (calculatedChecksum != receivedChecksum)
            return new FrameParseResult
            {
                IsComplete = false,
                ErrorMessage = $"和校验错误: 计算值=0x{calculatedChecksum:X2}, 接收值=0x{receivedChecksum:X2}",
                FrameData = frameData,
                ConsumedLength = frameLength
            };

        // 步骤 9: 验证帧结束符
        if (frameData[frameLength - 1] != ViewshineProtocolConstants.FrameEnd)
            return new FrameParseResult
            {
                IsComplete = false,
                ErrorMessage = "帧结束符不匹配",
                FrameData = frameData,
                ConsumedLength = frameLength
            };

        // 步骤 10: 提取地址（6 字节 BCD，低位在前）
        var addressBytes = new byte[ViewshineProtocolConstants.AddressLength];
        Array.Copy(frameData, ViewshineProtocolConstants.OffsetAddress, addressBytes, 0, ViewshineProtocolConstants.AddressLength);

        // 步骤 11: 提取数据域（包含系统时钟 + 设备类型 + 数据标识 + 帧序号 + 数据内容）
        byte[]? dataDomain = null;
        if (dataLength > 0)
        {
            dataDomain = new byte[dataLength];
            Array.Copy(frameData, ViewshineProtocolConstants.OffsetDataDomain, dataDomain, 0, dataLength);
        }

        return new FrameParseResult
        {
            IsComplete = true,
            FrameData = frameData,
            ConsumedLength = frameLength,
            DeviceAddress = addressBytes[0], // 基类 DeviceAddress 为 byte，取首字节
            CommandCode = controlCode,
            Data = dataDomain
        };
    }

    /// <summary>
    /// 构建协议发送帧
    /// <para>
    /// 帧结构: [0x68] [地址 6B] [0x68] [控制码] [L0 L1] [数据域] [CS] [0x16]
    /// </para>
    /// </summary>
    /// <param name="meterAddress">表具地址（6 字节 BCD，低位在前）。若不足 6 字节则右补 0x00。</param>
    /// <param name="dataDomain">数据域内容（系统时钟 + 设备类型 + 数据标识 + 帧序号 + 数据内容）</param>
    /// <param name="controlCode">控制码</param>
    /// <returns>完整的协议帧字节数组</returns>
    /// <example>
    /// <code>
    /// var parser = new ViewshineProtocolParser();
    /// byte[] addr = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
    /// byte[] dataDomain = parser.BuildDataDomain(null, DeviceType.CivilIotMeter, 0xC518, 0, null);
    /// byte[] frame = parser.BuildFrame(addr, dataDomain,
    ///     ViewshineProtocolConstants.BuildControlCode(DataDirection.MasterToMeter, FunctionCategory.ReadData));
    /// </code>
    /// </example>
    public byte[] BuildFrame(byte[] meterAddress, byte[] dataDomain, byte controlCode)
    {
        // 确保地址为 6 字节
        var address = new byte[ViewshineProtocolConstants.AddressLength];
        if (meterAddress != null)
        {
            int copyLen = Math.Min(meterAddress.Length, ViewshineProtocolConstants.AddressLength);
            Array.Copy(meterAddress, address, copyLen);
        }

        int dataLength = dataDomain?.Length ?? 0;
        // 帧总长度 = 1(头) + 6(地址) + 1(头2) + 1(控制码) + 2(长度) + L(数据域) + 1(校验) + 1(帧尾)
        int frameLength = ViewshineProtocolConstants.OffsetDataDomain + dataLength + 2;
        var frame = new byte[frameLength];

        // 写入第一个帧起始符
        frame[ViewshineProtocolConstants.OffsetFirstStart] = ViewshineProtocolConstants.FrameStart;

        // 写入目标地址（6B BCD，低位在前）
        Array.Copy(address, 0, frame, ViewshineProtocolConstants.OffsetAddress, ViewshineProtocolConstants.AddressLength);

        // 写入第二个帧起始符
        frame[ViewshineProtocolConstants.OffsetSecondStart] = ViewshineProtocolConstants.FrameStart;

        // 写入控制码
        frame[ViewshineProtocolConstants.OffsetControlCode] = controlCode;

        // 写入数据域长度（2B，低位在前）
        frame[ViewshineProtocolConstants.OffsetDataLengthLow] = (byte)(dataLength & 0xFF);
        frame[ViewshineProtocolConstants.OffsetDataLengthHigh] = (byte)((dataLength >> 8) & 0xFF);

        // 写入数据域
        if (dataDomain != null && dataLength > 0)
        {
            Array.Copy(dataDomain, 0, frame, ViewshineProtocolConstants.OffsetDataDomain, dataLength);
        }

        // 计算并写入和校验（从第一个 0x68 到校验码前所有字节的和模 256）
        int checksumOffset = frameLength - 2;
        frame[checksumOffset] = CalculateSum8(frame, 0, checksumOffset);

        // 写入帧结束符
        frame[frameLength - 1] = ViewshineProtocolConstants.FrameEnd;

        return frame;
    }

    /// <summary>
    /// 构建协议发送帧（兼容基类接口）
    /// <para>注意：基类 <see cref="FrameParserBase.BuildFrame(byte, byte[])"/> 的 command 参数
    /// 在此实现中被解释为完整的数据域内容。建议使用 <see cref="BuildFrame(byte[], byte[], byte)"/> 重载。</para>
    /// </summary>
    public override byte[] BuildFrame(byte deviceAddress, byte[] command)
    {
        // 将单字节地址扩展为 6 字节 BCD 地址
        var address = new byte[ViewshineProtocolConstants.AddressLength];
        address[0] = deviceAddress;

        // command 在此上下文中被解释为数据域
        return BuildFrame(address, command, ViewshineProtocolConstants.BuildControlCode(
            DataDirection.MasterToMeter, FunctionCategory.ReadData));
    }

    /// <summary>
    /// 判断响应帧是否与请求帧匹配
    /// <para>匹配规则：帧头匹配、地址匹配、数据标识匹配。</para>
    /// </summary>
    /// <param name="response">响应帧完整数据</param>
    /// <param name="request">请求帧完整数据</param>
    /// <returns>匹配返回 true，否则返回 false</returns>
    public override bool IsResponseMatch(byte[] response, byte[] request)
    {
        if (response == null || request == null) return false;
        if (response.Length < 13 || request.Length < 13) return false;

        // 检查第一个帧起始符
        if (response[0] != ViewshineProtocolConstants.FrameStart) return false;

        // 检查第二个帧起始符
        if (response[7] != ViewshineProtocolConstants.FrameStart) return false;

        // 检查目标地址是否匹配（6 字节）
        for (int i = 0; i < ViewshineProtocolConstants.AddressLength; i++)
        {
            if (response[ViewshineProtocolConstants.OffsetAddress + i]
                != request[ViewshineProtocolConstants.OffsetAddress + i])
                return false;
        }

        // 检查数据标识是否匹配（数据域偏移 7~8，即系统时钟6B + 设备类型1B 之后）
        int respDataLen = response[9] | (response[10] << 8);
        int reqDataLen = request[9] | (request[10] << 8);

        if (respDataLen < ViewshineProtocolConstants.DataDomainHeaderLength
            || reqDataLen < ViewshineProtocolConstants.DataDomainHeaderLength)
            return false;

        // 数据标识位于数据域偏移 7（系统时钟 6B + 设备类型 1B 之后）
        int respIdOffset = ViewshineProtocolConstants.OffsetDataDomain + 7;
        int reqIdOffset = ViewshineProtocolConstants.OffsetDataDomain + 7;

        if (respIdOffset + 1 >= response.Length || reqIdOffset + 1 >= request.Length)
            return false;

        return response[respIdOffset] == request[reqIdOffset]
            && response[respIdOffset + 1] == request[reqIdOffset + 1];
    }

    /// <summary>
    /// 构建数据域内容
    /// <para>
    /// 数据域 = [系统时钟 6B] [设备类型 1B] [数据标识 2B] [帧序号 1B] [数据内容...]
    /// </para>
    /// </summary>
    /// <param name="systemClock">系统时钟（null 时使用当前时间，年=0xFF 表示无效）</param>
    /// <param name="deviceType">设备类型</param>
    /// <param name="dataIdentifier">数据标识（如 0xC518）</param>
    /// <param name="frameSequence">帧序号（0=最后一帧，多帧时递减）</param>
    /// <param name="dataContent">数据内容（可为 null）</param>
    /// <returns>完整的数据域字节数组</returns>
    /// <example>
    /// <code>
    /// var parser = new ViewshineProtocolParser();
    /// // 构建读取出厂配置参数的数据域（无数据内容）
    /// byte[] dataDomain = parser.BuildDataDomain(
    ///     DateTime.Now, DeviceType.CivilIotMeter,
    ///     ViewshineProtocolConstants.DataIdFactoryConfig, 0, null);
    /// </code>
    /// </example>
    public byte[] BuildDataDomain(
        DateTime? systemClock,
        DeviceType deviceType,
        ushort dataIdentifier,
        byte frameSequence,
        byte[]? dataContent)
    {
        int contentLength = dataContent?.Length ?? 0;
        int totalLength = ViewshineProtocolConstants.DataDomainHeaderLength + contentLength;
        var dataDomain = new byte[totalLength];

        int offset = 0;

        // 系统时钟 6B BCD (年月日时分秒)
        var clock = systemClock ?? DateTime.Now;
        dataDomain[offset++] = DecimalToBcd(clock.Year - 2000);
        dataDomain[offset++] = DecimalToBcd(clock.Month);
        dataDomain[offset++] = DecimalToBcd(clock.Day);
        dataDomain[offset++] = DecimalToBcd(clock.Hour);
        dataDomain[offset++] = DecimalToBcd(clock.Minute);
        dataDomain[offset++] = DecimalToBcd(clock.Second);

        // 设备类型 1B
        dataDomain[offset++] = (byte)deviceType;

        // 数据标识 2B (低位在前)
        dataDomain[offset++] = (byte)(dataIdentifier & 0xFF);
        dataDomain[offset++] = (byte)((dataIdentifier >> 8) & 0xFF);

        // 帧序号 1B
        dataDomain[offset++] = frameSequence;

        // 数据内容
        if (dataContent != null && contentLength > 0)
        {
            Array.Copy(dataContent, 0, dataDomain, offset, contentLength);
        }

        return dataDomain;
    }

    /// <summary>
    /// 从解析结果中提取数据域的各组成部分
    /// </summary>
    /// <param name="dataDomain">数据域字节数组（来自 <see cref="FrameParseResult.Data"/>）</param>
    /// <returns>解析后的数据域结构</returns>
    public DataDomainParts ParseDataDomain(byte[] dataDomain)
    {
        var parts = new DataDomainParts();

        if (dataDomain == null || dataDomain.Length < ViewshineProtocolConstants.DataDomainHeaderLength)
            return parts;

        int offset = 0;

        // 系统时钟
        parts.SystemClock = new SystemClock
        {
            Year = dataDomain[offset],
            Month = dataDomain[offset + 1],
            Day = dataDomain[offset + 2],
            Hour = dataDomain[offset + 3],
            Minute = dataDomain[offset + 4],
            Second = dataDomain[offset + 5]
        };
        offset += ViewshineProtocolConstants.SystemClockLength;

        // 设备类型
        parts.DeviceType = (DeviceType)dataDomain[offset];
        offset += ViewshineProtocolConstants.DeviceTypeLength;

        // 数据标识（低位在前）
        parts.DataIdentifier = (ushort)(dataDomain[offset] | (dataDomain[offset + 1] << 8));
        offset += ViewshineProtocolConstants.DataIdentifierLength;

        // 帧序号
        parts.FrameSequence = dataDomain[offset];
        offset += ViewshineProtocolConstants.FrameSequenceLength;

        // 数据内容
        if (dataDomain.Length > offset)
        {
            parts.DataContent = new byte[dataDomain.Length - offset];
            Array.Copy(dataDomain, offset, parts.DataContent, 0, parts.DataContent.Length);
        }

        return parts;
    }

    /// <summary>
    /// 将 13 位十进制表号转换为 7 字节 BCD 编码（低位在前）
    /// <para>示例："0011234567890" → [90, 78, 56, 34, 12, 01, 00]</para>
    /// </summary>
    /// <param name="meterNumber">13 位十进制表号字符串</param>
    /// <returns>7 字节 BCD 编码</returns>
    public static byte[] MeterNumberToBcd(string meterNumber)
    {
        // 补齐到 14 位（7 字节 BCD = 14 位数字）
        string padded = meterNumber.PadLeft(14, '0');
        var bcd = new byte[7];

        // 低位在前：bcd[0] 存储最低 2 位
        for (int i = 0; i < 7; i++)
        {
            int hi = padded[13 - i * 2] - '0';
            int lo = padded[12 - i * 2] - '0';
            bcd[i] = (byte)((hi << 4) | lo);
        }

        return bcd;
    }

    /// <summary>
    /// 将 7 字节 BCD 编码转换为 13 位十进制表号字符串
    /// <para>示例：[90, 78, 56, 34, 12, 01, 00] → "0011234567890"</para>
    /// </summary>
    /// <param name="bcd">7 字节 BCD 编码（低位在前）</param>
    /// <returns>13 位十进制表号字符串</returns>
    public static string BcdToMeterNumber(byte[] bcd)
    {
        if (bcd == null || bcd.Length < 7) return string.Empty;

        // 高位在前重建字符串
        var chars = new char[14];
        for (int i = 0; i < 7; i++)
        {
            byte b = bcd[6 - i];
            chars[i * 2] = (char)('0' + ((b >> 4) & 0x0F));
            chars[i * 2 + 1] = (char)('0' + (b & 0x0F));
        }
        return new string(chars).TrimStart('0').PadLeft(1, '0');
    }

    /// <summary>
    /// 将 4 字节小端序 HEX 转换为带小数位的 decimal
    /// <para>示例：[0x4E, 0x61, 0xBC, 0x00] + 2 位小数 → 123456.78</para>
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="decimalPlaces">小数位数</param>
    /// <returns>转换后的 decimal 值</returns>
    public static decimal ReadLittleEndianDecimal(byte[] data, int offset, int decimalPlaces)
    {
        if (data == null || offset < 0 || offset + 4 > data.Length) return 0m;
        uint value = (uint)(data[offset] | (data[offset + 1] << 8)
            | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        return value / (decimal)Math.Pow(10, decimalPlaces);
    }

    /// <summary>
    /// 将带小数位的 decimal 转换为 4 字节小端序 HEX
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="decimalPlaces">小数位数</param>
    /// <returns>4 字节小端序 HEX</returns>
    public static byte[] WriteLittleEndianDecimal(decimal value, int decimalPlaces)
    {
        uint scaled = (uint)Math.Round(value * (decimal)Math.Pow(10, decimalPlaces));
        return new byte[]
        {
            (byte)(scaled & 0xFF),
            (byte)((scaled >> 8) & 0xFF),
            (byte)((scaled >> 16) & 0xFF),
            (byte)((scaled >> 24) & 0xFF)
        };
    }

    /// <summary>
    /// 读取 ASCII 字符串（去除尾部空格和空字符）
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="length">字节长度</param>
    /// <returns>ASCII 字符串</returns>
    public static string ReadAsciiString(byte[] data, int offset, int length)
    {
        if (data == null || offset < 0 || offset + length > data.Length) return string.Empty;
        return System.Text.Encoding.ASCII.GetString(data, offset, length)
            .TrimEnd(' ', '\0', '\r', '\n');
    }

    /// <summary>
    /// 计算和校验（Sum8）：指定范围内所有字节的和模 256
    /// </summary>
    /// <param name="data">数据缓冲区</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="length">计算长度</param>
    /// <returns>校验和字节</returns>
    private static byte CalculateSum8(byte[] data, int offset, int length)
    {
        int sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += data[offset + i];
        }
        return (byte)(sum & 0xFF);
    }

    /// <summary>
    /// 十进制数转 BCD 编码
    /// <para>示例：25 → 0x25</para>
    /// </summary>
    private static byte DecimalToBcd(int value)
    {
        return (byte)(((value / 10) << 4) | (value % 10));
    }
}

/// <summary>
/// 数据域解析后的组成部分
/// </summary>
public class DataDomainParts
{
    /// <summary>系统时钟（6B BCD）</summary>
    public SystemClock SystemClock { get; set; } = new();

    /// <summary>设备类型</summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>数据标识（如 0xC518）</summary>
    public ushort DataIdentifier { get; set; }

    /// <summary>帧序号（0=最后一帧）</summary>
    public byte FrameSequence { get; set; }

    /// <summary>数据内容（数据域中去除固定头部后的部分）</summary>
    public byte[]? DataContent { get; set; }
}
