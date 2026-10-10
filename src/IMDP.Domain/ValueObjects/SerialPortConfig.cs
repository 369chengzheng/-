// =============================================================================
// 文件名：SerialPortConfig.cs
// 功能描述：串口配置值对象，封装波特率、数据位、停止位、校验位等串口通讯参数。
//           作为全局配置串口参数的内存表示与传递载体。
// =============================================================================
namespace IMDP.Domain.ValueObjects;

/// <summary>
/// 串口配置值对象
/// <para>封装串口通讯所需的核心参数，对应全局配置 GlobalConfig 中的串口字段。</para>
/// <para>各字段取值需与 System.IO.Ports.SerialPort 接受的枚举字符串一致。</para>
/// </summary>
public class SerialPortConfig
{
    /// <summary>波特率（默认 9600）</summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>数据位（默认 8）</summary>
    public int DataBits { get; set; } = 8;

    /// <summary>停止位（One/OnePointFive/Two，默认 One）</summary>
    public string StopBits { get; set; } = "One";

    /// <summary>校验位（None/Even/Odd/Mark/Space，默认 None）</summary>
    public string Parity { get; set; } = "None";
}