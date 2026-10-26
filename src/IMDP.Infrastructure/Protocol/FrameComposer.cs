// =============================================================================
// 文件名：FrameComposer.cs
// 功能描述：协议无关帧组合与解析静态共用工具
//           基于 FrameFormatConfig 提供帧头/地址/命令/长度/数据/校验码/帧尾的组合与
//           拆解，以及响应匹配判断。具体协议无需继承 FrameParserBase 也可直接调用本类
//           方法完成组帧/解帧；FrameParserBase 亦委托本类以保持逻辑单一来源。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 协议无关帧组合与解析静态共用工具
/// <para>所有方法接收 <see cref="FrameFormatConfig"/> 作为参数，不依赖任何具体协议实例，
/// 可被任意产品协议直接复用（例如不想继承 <see cref="FrameParserBase"/> 时）。</para>
/// </summary>
public static class FrameComposer
{
    /// <summary>
    /// 根据帧格式配置构建一帧数据（组合帧头/地址/命令/长度/数据/校验码/帧尾）
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <param name="deviceAddress">设备地址。</param>
    /// <param name="command">命令数据（含命令码与数据区）。</param>
    /// <returns>组装完成的帧字节数组。</returns>
    public static byte[] BuildFrame(FrameFormatConfig format, byte deviceAddress, byte[] command)
    {
        var header = format.FrameHeader;
        // 校验码长度：CRC16 为 2 字节，其它为 1 字节
        int checksumLen = GetChecksumLength(format.ChecksumType);

        // 数据区起点：取各固定字段（帧头/地址/命令/长度）的最大结束位置，
        // 兼容“长度在前、地址/命令位于长度之后”的布局（如 G2-SAGA 的 68|len|工位|命令|数据）。
        int dataStart = ComputeDataStart(format);

        // 唤醒前导总长（如海外二代水表 549×0x55 + 5×0xFE 同步码；不参与长度字段与校验码计算）
        int preambleLen = (format.Preamble != null && format.PreambleRepeat > 0)
            ? format.Preamble.Length * format.PreambleRepeat
            : 0;
        preambleLen += format.PreambleSuffix?.Length ?? 0;

        // 有效数据区长度：CommandIsPayload 模式下 command 首字节作为命令码写入命令字段，不进数据区
        int payloadLen = format.CommandIsPayload && command.Length > 0 ? command.Length - 1 : command.Length;

        // 计算总长度：帧体长度 = 数据区结束位置（数据区起点 + 数据长度），再补校验码与帧尾
        int totalLen = dataStart + payloadLen;
        totalLen += checksumLen;
        if (format.FrameFooter != null) totalLen += format.FrameFooter.Length;

        var frame = new byte[preambleLen + totalLen];

        // 写入唤醒前导：重复段（549×0x55）+ 尾部同步码（5×0xFE），紧贴帧头之前
        int preambleRepeatLen = preambleLen - (format.PreambleSuffix?.Length ?? 0);
        for (int i = 0; i < format.PreambleRepeat; i++)
        {
            Array.Copy(format.Preamble, 0, frame, i * format.Preamble.Length, format.Preamble.Length);
        }
        if (format.PreambleSuffix != null && format.PreambleSuffix.Length > 0)
        {
            Array.Copy(format.PreambleSuffix, 0, frame, preambleRepeatLen, format.PreambleSuffix.Length);
        }

        // 写入帧头（前导之后）
        Array.Copy(header, 0, frame, preambleLen, header.Length);

        // 写入设备地址（偏移含前导）
        if (format.AddressField != null)
            WriteField(frame, WithPreamble(format.AddressField, preambleLen), deviceAddress);

        // 写入命令码：优先 CommandIsPayload/无固定值时取 command 首字节，其次配置的固定值
        if (format.CommandField != null)
        {
            byte cmdValue = format.CommandIsPayload
                ? (command.Length > 0 ? command[0] : (byte)0)
                : format.CommandFieldValue ?? (command.Length > 0 ? command[0] : (byte)0);
            WriteField(frame, WithPreamble(format.CommandField, preambleLen), cmdValue);
        }

        // 写入长度字段
        if (format.LengthField != null)
        {
            int lengthValue = format.LengthFieldMode == LengthFieldMode.TotalFrameLength
                ? totalLen                                    // 长度字段 = 整帧总长
                : payloadLen;                                  // 长度字段 = 数据区长度
            WriteField(frame, WithPreamble(format.LengthField, preambleLen), lengthValue);
        }

        // 写入数据区（CommandIsPayload 模式跳过首字节命令码，从 command[1] 拷入）
        if (payloadLen > 0)
        {
            Array.Copy(command, command.Length - payloadLen, frame, preambleLen + dataStart, payloadLen);
        }

        // 计算并写入校验码（位于数据区之后、帧尾之前；数据从帧头起算，不含前导）
        if (format.ChecksumType != ChecksumType.None)
        {
            int bodyLen = totalLen - (format.FrameFooter?.Length ?? 0) - checksumLen;
            var dataToCheck = new byte[bodyLen];
            Array.Copy(frame, preambleLen, dataToCheck, 0, bodyLen);
            var checksum = ChecksumCalculator.Calculate(dataToCheck, format.ChecksumType);
            Array.Copy(checksum, 0, frame, preambleLen + bodyLen, checksum.Length);
        }

        // 写入帧尾
        if (format.FrameFooter != null && format.FrameFooter.Length > 0)
        {
            Array.Copy(format.FrameFooter, 0, frame, preambleLen + totalLen - format.FrameFooter.Length, format.FrameFooter.Length);
        }

        return frame;
    }

    /// <summary>
    /// 为已组装（不含校验码）的帧计算并追加校验码，返回含校验的完整帧。
    /// <para>适用于「先对数据区加密、再补校验」的协议场景。</para>
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <param name="frameWithoutChecksum">尚未写入校验码的帧字节。</param>
    /// <returns>追加校验码后的完整帧。</returns>
    public static byte[] AppendChecksum(FrameFormatConfig format, byte[] frameWithoutChecksum)
    {
        int checksumLen = GetChecksumLength(format.ChecksumType);
        if (checksumLen == 0) return frameWithoutChecksum;

        var full = new byte[frameWithoutChecksum.Length + checksumLen];
        Array.Copy(frameWithoutChecksum, 0, full, 0, frameWithoutChecksum.Length);
        var checksum = ChecksumCalculator.Calculate(frameWithoutChecksum, format.ChecksumType);
        Array.Copy(checksum, 0, full, frameWithoutChecksum.Length, checksum.Length);
        return full;
    }

    /// <summary>
    /// 尝试从缓冲区解析一帧数据（支持粘包/拆包）
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="offset">起始偏移量。</param>
    /// <param name="length">可用数据长度。</param>
    /// <returns>解析结果，包含是否完整、帧数据、设备地址、命令码、数据区等信息。</returns>
    public static FrameParseResult TryParseFrame(FrameFormatConfig format, byte[] buffer, int offset, int length)
    {
        var headerLen = format.FrameHeader.Length;

        // 数据不足以匹配帧头
        if (length < headerLen)
            return new FrameParseResult { IsComplete = false, ErrorMessage = "数据不足帧头长度" };

        // 匹配帧头
        for (int i = 0; i < headerLen; i++)
        {
            if (buffer[offset + i] != format.FrameHeader[i])
                return new FrameParseResult { IsComplete = false, ErrorMessage = "帧头不匹配" };
        }

        // 计算帧总长度：优先使用固定长度，否则根据长度字段计算
        int checksumLen = GetChecksumLength(format.ChecksumType);
        int footerLen = (format.FrameFooter != null) ? format.FrameFooter.Length : 0;
        int frameLen = format.FixedFrameLength;
        if (frameLen <= 0 && format.LengthField != null)
        {
            if (length < format.LengthField.Offset + format.LengthField.Length)
                return new FrameParseResult { IsComplete = false, ErrorMessage = "数据不足长度字段" };

            int lengthFieldValue = ReadField(buffer, offset, format.LengthField);
            if (format.LengthFieldMode == LengthFieldMode.TotalFrameLength)
            {
                // 长度字段 = 整帧总长：已含帧头/帧尾/校验，无需再叠加
                frameLen = lengthFieldValue;
            }
            else
            {
                int dataLen = lengthFieldValue;
                frameLen = format.LengthField.Offset + format.LengthField.Length + dataLen;
            }
        }

        // 累加帧尾与校验码长度（整帧总长模式下已包含，不再叠加）
        if (format.LengthField == null || format.LengthFieldMode != LengthFieldMode.TotalFrameLength)
        {
            frameLen += footerLen + checksumLen;
        }

        // 数据不足完整帧长度
        if (length < frameLen)
            return new FrameParseResult { IsComplete = false, ErrorMessage = "数据不足完整帧长度" };

        // 提取完整帧数据
        var frameData = new byte[frameLen];
        Array.Copy(buffer, offset, frameData, 0, frameLen);

        // 校验码验证（校验位于数据区之后、帧尾之前）
        if (format.ChecksumType != ChecksumType.None)
        {
            int bodyLen = frameLen - footerLen - checksumLen;
            var dataToCheck = new byte[bodyLen];
            Array.Copy(frameData, 0, dataToCheck, 0, bodyLen);
            var receivedChecksum = new byte[checksumLen];
            Array.Copy(frameData, bodyLen, receivedChecksum, 0, checksumLen);

            if (!ChecksumCalculator.Verify(dataToCheck, receivedChecksum, format.ChecksumType))
                return new FrameParseResult { IsComplete = false, ErrorMessage = "校验码错误", FrameData = frameData, ConsumedLength = frameLen };
        }

        // 提取设备地址字段
        byte deviceAddress = 0;
        if (format.AddressField != null)
            deviceAddress = (byte)ReadField(buffer, offset, format.AddressField);

        // 提取命令码字段
        byte commandCode = 0;
        if (format.CommandField != null)
            commandCode = (byte)ReadField(buffer, offset, format.CommandField);

        // 提取数据区（依据长度字段）
        byte[]? data = null;
        if (format.LengthField != null)
        {
            int lengthFieldValue = ReadField(buffer, offset, format.LengthField);
            int dataLen = (format.LengthFieldMode == LengthFieldMode.TotalFrameLength)
                ? lengthFieldValue - ComputeDataStart(format) - checksumLen - footerLen
                : lengthFieldValue;
            int dataStart = ComputeDataStart(format);
            if (dataLen > 0)
            {
                data = new byte[dataLen];
                Array.Copy(buffer, offset + dataStart, data, 0, dataLen);
            }
            else
            {
                data = Array.Empty<byte>();
            }
        }

        return new FrameParseResult
        {
            IsComplete = true,
            FrameData = frameData,
            ConsumedLength = frameLen,
            DeviceAddress = deviceAddress,
            CommandCode = commandCode,
            Data = data
        };
    }

    /// <summary>
    /// 判断响应帧是否与请求帧匹配（通过帧头与设备地址比对）
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <param name="response">响应帧数据。</param>
    /// <param name="request">请求帧数据。</param>
    /// <returns>匹配返回 true；否则返回 false。</returns>
    public static bool IsResponseMatch(FrameFormatConfig format, byte[] response, byte[] request)
    {
        if (response.Length < format.FrameHeader.Length) return false;

        // 检查帧头
        for (int i = 0; i < format.FrameHeader.Length; i++)
        {
            if (response[i] != format.FrameHeader[i]) return false;
        }

        // 检查地址字段是否与请求一致
        if (format.AddressField != null && request.Length > format.AddressField.Offset)
        {
            var reqAddr = request[format.AddressField.Offset];
            var respAddr = response[format.AddressField.Offset];
            if (reqAddr != respAddr) return false;
        }

        // 检查命令字段（仅当配置了命令比对掩码时启用，向后兼容：默认不比对）
        if (format.CommandMatchMask != null && format.CommandField != null)
        {
            if (request.Length <= format.CommandField.Offset || response.Length <= format.CommandField.Offset)
                return false;
            byte mask = format.CommandMatchMask.Value;
            byte reqCmd = (byte)(request[format.CommandField.Offset] & mask);
            byte respCmd = (byte)(response[format.CommandField.Offset] & mask);
            if (reqCmd != respCmd) return false;
        }

        return true;
    }

    /// <summary>
    /// 匹配缓冲区起始偏移处的帧头
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="offset">起始偏移。</param>
    /// <returns>帧头匹配返回 true；否则返回 false。</returns>
    public static bool MatchHeader(FrameFormatConfig format, byte[] buffer, int offset)
    {
        if (buffer.Length - offset < format.FrameHeader.Length) return false;
        for (int i = 0; i < format.FrameHeader.Length; i++)
        {
            if (buffer[offset + i] != format.FrameHeader[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// 根据校验类型计算校验码字节长度
    /// </summary>
    /// <param name="type">校验算法类型。</param>
    /// <returns>校验码长度（None=0，CRC16=2，其它=1）。</returns>
    private static int GetChecksumLength(ChecksumType type)
    {
        return type == ChecksumType.None ? 0 :
            (type == ChecksumType.Crc16Modbus || type == ChecksumType.Crc16Ccitt) ? 2 : 1;
    }

    /// <summary>
    /// 将字段偏移加上前导长度（供含唤醒前导的帧内字段定位；前导由调用方按需开启）。
    /// <para>注意：返回的 FieldConfig 仅为写入定位用临时对象，Offset 已含前导偏移。</para>
    /// </summary>
    private static FieldConfig WithPreamble(FieldConfig field, int preambleLen)
        => preambleLen == 0 ? field : new FieldConfig { Offset = field.Offset + preambleLen, Length = field.Length };

    /// <summary>
    /// 计算数据区起点（取帧头/地址/命令/长度各固定字段的最大结束位置）
    /// <para>兼容“长度在前、地址/命令位于长度之后”的布局，避免数据区覆盖地址/命令字段。</para>
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <returns>数据区起始偏移。</returns>
    private static int ComputeDataStart(FrameFormatConfig format)
    {
        int dataStart = format.FrameHeader.Length;
        if (format.AddressField != null)
            dataStart = Math.Max(dataStart, format.AddressField.Offset + format.AddressField.Length);
        if (format.CommandField != null)
            dataStart = Math.Max(dataStart, format.CommandField.Offset + format.CommandField.Length);
        if (format.LengthField != null)
            dataStart = Math.Max(dataStart, format.LengthField.Offset + format.LengthField.Length);
        return dataStart;
    }

    /// <summary>
    /// 从缓冲区读取指定字段（大端序）
    /// </summary>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="offset">帧起始偏移量。</param>
    /// <param name="field">字段配置。</param>
    /// <returns>字段整数值。</returns>
    private static int ReadField(byte[] buffer, int offset, FieldConfig field)
    {
        int value = 0;
        for (int i = 0; i < field.Length; i++)
        {
            value = (value << 8) | buffer[offset + field.Offset + i];
        }
        return value;
    }

    /// <summary>
    /// 从缓冲区读取指定字段（小端序），由协议调用方明确选用
    /// </summary>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="offset">帧起始偏移量。</param>
    /// <param name="field">字段配置。</param>
    /// <returns>字段整数值。</returns>
    public static int ReadFieldLittleEndian(byte[] buffer, int offset, FieldConfig field)
    {
        int value = 0;
        for (int i = field.Length - 1; i >= 0; i--)
        {
            value = (value << 8) | buffer[offset + field.Offset + i];
        }
        return value;
    }

    /// <summary>
    /// 向缓冲区写入指定字段（大端序）
    /// </summary>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="field">字段配置。</param>
    /// <param name="value">待写入的整数值。</param>
    private static void WriteField(byte[] buffer, FieldConfig field, int value)
    {
        for (int i = field.Length - 1; i >= 0; i--)
        {
            buffer[field.Offset + i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }

    /// <summary>
    /// 向缓冲区写入指定字段（小端序），由协议调用方明确选用
    /// </summary>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="field">字段配置。</param>
    /// <param name="value">待写入的整数值。</param>
    public static void WriteFieldLittleEndian(byte[] buffer, FieldConfig field, int value)
    {
        for (int i = 0; i < field.Length; i++)
        {
            buffer[field.Offset + i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }
}
