// =============================================================================
// 文件名：ChecksumCalculator.cs
// 功能描述：校验码计算器（静态工具类）
//           支持 Sum8/Xor8/CRC8/CRC16-Modbus/CRC16-CCITT/LRC8 等多种校验算法，
//           用于协议帧的校验码计算与验证。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 校验算法计算器（静态工具类）
/// <para>提供多种校验码的计算与验证方法，供帧解析器在构建/解析帧时使用。</para>
/// </summary>
public static class ChecksumCalculator
{
    /// <summary>
    /// 根据指定校验类型计算给定数据的校验码。
    /// </summary>
    /// <param name="data">待计算校验码的数据字节。</param>
    /// <param name="type">校验算法类型。</param>
    /// <returns>校验码字节数组（None 类型返回空数组）。</returns>
    public static byte[] Calculate(byte[] data, ChecksumType type)
    {
        return type switch
        {
            ChecksumType.None => Array.Empty<byte>(),
            ChecksumType.Sum8 => new byte[] { CalculateSum8(data) },
            ChecksumType.Xor8 => new byte[] { CalculateXor8(data) },
            ChecksumType.Crc8 => new byte[] { CalculateCrc8(data) },
            ChecksumType.Crc16Modbus => CalculateCrc16Modbus(data),
            ChecksumType.Crc16Ccitt => CalculateCrc16Ccitt(data),
            ChecksumType.Lrc8 => new byte[] { CalculateLrc8(data) },
            _ => throw new ArgumentException($"不支持的校验类型: {type}")
        };
    }

    /// <summary>
    /// 计算 Sum8 校验：对全部字节求和取低 8 位。
    /// </summary>
    /// <param name="data">待计算的数据字节。</param>
    /// <returns>Sum8 校验值。</returns>
    private static byte CalculateSum8(byte[] data)
    {
        byte sum = 0;
        foreach (byte b in data) sum += b;
        return sum;
    }

    /// <summary>
    /// 计算 Xor8 校验：对全部字节依次异或。
    /// </summary>
    /// <param name="data">待计算的数据字节。</param>
    /// <returns>Xor8 校验值。</returns>
    private static byte CalculateXor8(byte[] data)
    {
        byte xor = 0;
        foreach (byte b in data) xor ^= b;
        return xor;
    }

    /// <summary>
    /// 计算 CRC8 校验（多项式默认 0x07）。
    /// </summary>
    /// <param name="data">待计算的数据字节。</param>
    /// <param name="polynomial">CRC 多项式，默认 0x07。</param>
    /// <returns>CRC8 校验值。</returns>
    private static byte CalculateCrc8(byte[] data, byte polynomial = 0x07)
    {
        byte crc = 0xFF;
        foreach (byte b in data)
        {
            crc ^= b;
            // 逐位进行 CRC 计算
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ polynomial) : (byte)(crc << 1);
        }
        return crc;
    }

    /// <summary>
    /// 计算 CRC16-Modbus 校验（多项式 0xA001，低字节在前）。
    /// </summary>
    /// <param name="data">待计算的数据字节。</param>
    /// <returns>2 字节 CRC16 校验值（高字节在前）。</returns>
    private static byte[] CalculateCrc16Modbus(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            // 逐位进行 CRC 计算（Modbus 反向多项式 0xA001）
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x0001) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return new byte[] { (byte)(crc >> 8), (byte)crc };
    }

    /// <summary>
    /// 计算 CRC16-CCITT 校验（多项式 0x1021）。
    /// </summary>
    /// <param name="data">待计算的数据字节。</param>
    /// <returns>2 字节 CRC16 校验值（高字节在前）。</returns>
    private static byte[] CalculateCrc16Ccitt(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);
            // 逐位进行 CRC 计算（CCITT 多项式 0x1021）
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return new byte[] { (byte)(crc >> 8), (byte)crc };
    }

    /// <summary>
    /// 计算 LRC8 校验：对全部字节依次异或（与 Xor8 等价实现）。
    /// </summary>
    /// <param name="data">待计算的数据字节。</param>
    /// <returns>LRC8 校验值。</returns>
    private static byte CalculateLrc8(byte[] data)
    {
        byte lrc = 0;
        foreach (byte b in data) lrc ^= b;
        return lrc;
    }

    /// <summary>验证校验码</summary>
    /// <param name="data">原始数据字节。</param>
    /// <param name="checksum">待验证的校验码。</param>
    /// <param name="type">校验算法类型。</param>
    /// <returns>校验通过返回 true；否则返回 false。</returns>
    public static bool Verify(byte[] data, byte[] checksum, ChecksumType type)
    {
        var calculated = Calculate(data, type);
        // 长度不一致直接判定为不匹配
        if (calculated.Length != checksum.Length)
            return false;

        // 逐字节比较，任一不相同即校验失败
        for (int i = 0; i < calculated.Length; i++)
        {
            if (calculated[i] != checksum[i])
                return false;
        }
        return true;
    }
}
