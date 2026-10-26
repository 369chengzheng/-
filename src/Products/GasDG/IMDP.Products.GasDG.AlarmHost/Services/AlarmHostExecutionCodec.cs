using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using IMDP.Application.DTOs;
using IMDP.Products.GasDG.AlarmHost.Models;
using static IMDP.Products.GasDG.AlarmHost.Models.AlarmHostFunctionCatalog;

namespace IMDP.Products.GasDG.AlarmHost.Services;

/// <summary>
/// 报警项目与协议值之间的转换；对象编号和读写权限只在功能目录登记
/// 不参与串口、页面状态或方案保存，不修改方案快照
/// </summary>
public static class AlarmHostExecutionCodec
{
    /// <summary>
    /// 根据方案原始参数及本次扫码、时钟生成可写对象的目标值
    /// 表号使用扫码值，表时使用本次设备操作时间，均不回写配置
    /// </summary>
    public static byte[] Encode(AlarmHostResultRow item, string scanCode, DateTime now)
    {
        string P(string code) => item.Parameters[code].Trim();
        byte B(string code) => byte.Parse(P(code), CultureInfo.InvariantCulture);
        switch (item.Code)
        {
            case MeterNumber: return Prefix(scanCode, 32, false);
            case MeterTime: return EncodeTime(now);
            case ProductionStatus: return [B(SubParameterCodes.Value)];
            case HasValve: return [bool.Parse(P(SubParameterCodes.Value)) ? (byte)1 : (byte)0];
            case Lifetime:
                var life = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(life, uint.Parse(P(SubParameterCodes.Value), CultureInfo.InvariantCulture));
                return life;
            case AlarmThreshold:
            case AlarmRecoveryThreshold: return [B(SubParameterCodes.Value)];
            case MainServer:
                var server = Prefix(P("Address"), 32, false);
                var port = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(port, ushort.Parse(P("Port"), CultureInfo.InvariantCulture));
                return [B("AddressType"), .. server, .. port];
            case MainNetwork:
                // 网络内容保留用户原值，长度按实际ASCII字节计算。
                return [.. Prefix(item.Parameters["UserName"], 32), .. Prefix(item.Parameters["Password"], 32), .. Prefix(item.Parameters["Apn"], 48)];
            case MainPowerReport:
                var report = Enumerable.Repeat((byte)0xFF, 11).ToArray();
                report[0] = B("Type");
                if (report[0] is 7 or 8)
                {
                    for (var index = 0; index < 5; index++)
                    {
                        var slot = P($"Slot{index + 1}");
                        report[index * 2 + 1] = Bcd(int.Parse(slot[..2], CultureInfo.InvariantCulture));
                        report[index * 2 + 2] = Bcd(int.Parse(slot[2..], CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    report[1] = B("Cycle");
                    report[2] = Bcd(B("Day")); report[3] = Bcd(B("Hour")); report[4] = Bcd(B("Minute"));
                }
                return report;
            default: throw new InvalidOperationException($"{item.Name}没有写入指令。");
        }
    }

    /// <summary>
    /// 解码实际回读并按启用项目核对目标；只读标识项目不强加方案类型限制
    /// 表时沿用旧报警检验的三分钟差值，其余可写参数逐值比较
    /// </summary>
    public static AlarmHostItemResult Evaluate(AlarmHostResultRow item, byte[] value, string scanCode, DateTime now, byte[]? expected = null)
    {
        var (display, mes) = Decode(item.Code, value);
        var pass = true;
        var message = "读取成功";
        if (item.Code == MeterTime)
        {
            pass = Math.Abs((now - ReadTime(value)).TotalMinutes) <= 3;
            message = pass ? "时间核对通过" : $"目标时间：{now:yyyy-MM-dd HH:mm:ss}, 实际时间：{display}, 相差超过3分钟";
        }
        else if (Get(item.Code).CanWrite)
        {
            expected ??= Encode(item, scanCode, now);
            // 上报尾部填充不参与核对；按时上报只核对类型及周期，沿用既有设备规则。
            var length = item.Code == MainPowerReport ? expected[0] switch { 5 => 2, 7 or 8 => 11, _ => 5 } : expected.Length;
            pass = value.Length == expected.Length && value.AsSpan(0, length).SequenceEqual(expected.AsSpan(0, length));
            message = pass ? "回读核对通过" : $"目标值：{Decode(item.Code, expected).Display}, 实际值：{display}";
        }
        return new(item.Code, display, mes, pass, message);
    }

    /// <summary>
    /// 严格按对象长度解码；输出实际值，复合值统一采用英文逗号
    /// </summary>
    private static (string Display, string Mes) Decode(string code, byte[] value)
    {
        switch (code)
        {
            case MeterNumber:
                var index = 0;
                var number = ReadText(value, ref index, 32);
                End(value, index);
                return (number, number);
            case MeterTime:
                var time = ReadTime(value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return (time, time);
            case ProductionStatus:
            case HasValve:
                Length(value, 1);
                if (value[0] > 1) throw new FormatException("设备返回无效的状态值。");
                var state = code == HasValve ? value[0] == 1 ? "有阀门" : "无阀门" : ProductionStatusOptions.Single(option => option.Key == value[0]).Value;
                return (state, value[0].ToString(CultureInfo.InvariantCulture));
            case Lifetime:
                Length(value, 4);
                var life = BinaryPrimitives.ReadUInt32LittleEndian(value).ToString(CultureInfo.InvariantCulture);
                return (life, life);
            case AlarmThreshold:
            case AlarmRecoveryThreshold:
            case Csq:
                Length(value, 1);
                if (code == Csq && value[0] > 31 && value[0] != 99) throw new FormatException("CSQ只允许0至31或99。");
                var scalar = value[0].ToString(CultureInfo.InvariantCulture);
                return (scalar, scalar);
            case Imei: return FixedText(value, 15);
            case Imsi: return FixedText(value, 15);
            case Iccid: return FixedText(value, 20);
            case BluetoothSnOrSparkMac: return FixedText(value, 12);
            case AlarmHostFunctionCatalog.Version:
                Length(value, 12);
                var software = string.Join(".", value[..3].Select(Decimal));
                var hardware = string.Join(".", value[3..6].Select(Decimal));
                var compile = ReadTime(value[6..]).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return ($"V{software},V{hardware},{compile}", $"{software},{hardware},{compile}");
            case MainServer:
                if (value.Length < 4 || value[0] > 1) throw new FormatException("服务器地址类型或长度无效。");
                var offset = 1;
                var address = ReadText(value, ref offset, 32);
                if (value.Length != offset + 2) throw new FormatException("服务器端口长度无效。");
                var port = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(offset));
                return ($"{AddressTypeOptions.Single(option => option.Key == value[0]).Value},{address},{port}", $"{value[0]},{address},{port}");
            case MainNetwork:
                var cursor = 0;
                var user = ReadText(value, ref cursor, 32);
                var password = ReadText(value, ref cursor, 32);
                var apn = ReadText(value, ref cursor, 48);
                End(value, cursor);
                return ($"{user},{password},{apn}", $"{user},{password},{apn}");
            case MainPowerReport:
                Length(value, 11);
                var type = ReportTypeOptions.FirstOrDefault(option => option.Key == value[0]);
                if (type.Value == null) throw new FormatException("无效的主电上报类型。");
                var values = value[0] == 5 ? new[] { value[1].ToString(CultureInfo.InvariantCulture) } : value[0] is 7 or 8
                    ? Enumerable.Range(0, 5).Select(i => $"{Decimal(value[i * 2 + 1]):D2}{Decimal(value[i * 2 + 2]):D2}")
                    : new[] { value[1].ToString(CultureInfo.InvariantCulture), Decimal(value[2]).ToString(), Decimal(value[3]).ToString(), Decimal(value[4]).ToString() };
                var parts = string.Join(",", values);
                return ($"{type.Value},{parts}", $"{value[0]},{parts}");
            default: throw new InvalidOperationException($"未登记的设备项目：{code}。");
        }
    }

    /// <summary>
    /// 编码有长度前缀的ASCII字段，拒绝静默替换中文和超出协议上限的内容
    /// </summary>
    private static byte[] Prefix(string value, int maximum, bool allowEmpty = true)
    {
        if ((!allowEmpty && value.Length == 0) || value.Length > maximum || value.Any(character => character > 0x7F))
            throw new FormatException($"设备文本必须为{(allowEmpty ? "0" : "1")}至{maximum}字节ASCII。");
        return [(byte)value.Length, .. Encoding.ASCII.GetBytes(value)];
    }

    /// <summary>
    /// 按长度前缀读取一个文本字段，返回实际消费位置
    /// </summary>
    private static string ReadText(byte[] value, ref int index, int maximum)
    {
        if (index >= value.Length) throw new FormatException("设备文本缺少长度字段。");
        var length = value[index++];
        if (length > maximum || index + length > value.Length) throw new FormatException("设备文本长度与实际内容不符。");
        var text = Ascii(value.AsSpan(index, length));
        index += length;
        return text;
    }

    /// <summary>
    /// 固定宽度模块标识只去除末尾00/FF填充，实际文本仍严格按ASCII解码
    /// </summary>
    private static (string, string) FixedText(byte[] value, int length)
    {
        Length(value, length);
        var used = value.Length;
        while (used > 0 && value[used - 1] is 0 or 0xFF) used--;
        if (used == 0) throw new FormatException("设备标识为空或全为填充字节。");
        var text = Ascii(value.AsSpan(0, used));
        return (text, text);
    }

    /// <summary>
    /// 明确拒绝非ASCII回包，避免转换为问号后误判成功
    /// </summary>
    private static string Ascii(ReadOnlySpan<byte> value)
    {
        foreach (var character in value) if (character > 0x7F) throw new FormatException("设备返回非ASCII文本。");
        return Encoding.ASCII.GetString(value);
    }

    /// <summary>
    /// 固定对象长度不包含帧头、对象个数和对象编号
    /// </summary>
    private static void Length(byte[] value, int length)
    {
        if (value.Length != length) throw new FormatException($"设备对象应为{length}字节，实际{value.Length}字节。");
    }

    /// <summary>
    /// 变长对象必须完整消费，尾部数据不能丢弃后判成功
    /// </summary>
    private static void End(byte[] value, int index)
    {
        if (value.Length != index) throw new FormatException("设备对象尾部存在多余字节。");
    }

    /// <summary>
    /// 表具时钟使用六字节BCD，不经过字符串或UTC转换
    /// </summary>
    private static byte[] EncodeTime(DateTime value) =>
        [Bcd(value.Year % 100), Bcd(value.Month), Bcd(value.Day), Bcd(value.Hour), Bcd(value.Minute), Bcd(value.Second)];

    /// <summary>
    /// 核对BCD及实际日历范围，错误时间作为本项失败
    /// </summary>
    private static DateTime ReadTime(byte[] value)
    {
        Length(value, 6);
        return new DateTime(2000 + Decimal(value[0]), Decimal(value[1]), Decimal(value[2]), Decimal(value[3]), Decimal(value[4]), Decimal(value[5]));
    }

    /// <summary>
    /// 单个BCD分量的编码
    /// </summary>
    private static byte Bcd(int value) => (byte)((value / 10 << 4) | value % 10);

    /// <summary>
    /// 单个BCD分量的解码，非法半字节不能作为有效数字
    /// </summary>
    private static int Decimal(byte value) => (value & 15) <= 9 && (value >> 4) <= 9
        ? (value >> 4) * 10 + (value & 15) : throw new FormatException("设备返回无效的BCD值。");
}
