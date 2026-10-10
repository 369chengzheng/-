using IMDP.Domain.Products;
using IMDP.Infrastructure.Protocol;

namespace IMDP.Products.GasDG.WholeA.Protocol;

/// <summary>
/// 威星物联网燃气表生产测试通讯协议 V1.0.0 — 常量与枚举定义
/// <para>定义帧标记、控制码位域、数据标识、设备类型、表具规格等协议常量。</para>
/// <example>
/// 示例：构建读取出厂配置参数的控制码
/// <code>
/// byte ctrl = ViewshineProtocolConstants.BuildControlCode(
///     DataDirection.MasterToMeter, FunctionCategory.ReadData);
/// // ctrl = 0x01
/// </code>
/// </example>
/// </summary>
public static class ViewshineProtocolConstants
{
    // ===== 帧标记 =====

    /// <summary>帧起始符（出现两次：帧首和地址后各一个）</summary>
    public const byte FrameStart = 0x68;

    /// <summary>帧结束符</summary>
    public const byte FrameEnd = 0x16;

    // ===== 帧结构偏移量 =====

    /// <summary>第一个帧起始符偏移</summary>
    public const int OffsetFirstStart = 0;

    /// <summary>目标地址起始偏移（6 字节 BCD，A5~A0 低位在前）</summary>
    public const int OffsetAddress = 1;

    /// <summary>地址字段长度（字节）</summary>
    public const int AddressLength = 6;

    /// <summary>第二个帧起始符偏移</summary>
    public const int OffsetSecondStart = 7;

    /// <summary>控制码偏移</summary>
    public const int OffsetControlCode = 8;

    /// <summary>数据域长度低字节偏移</summary>
    public const int OffsetDataLengthLow = 9;

    /// <summary>数据域长度高字节偏移</summary>
    public const int OffsetDataLengthHigh = 10;

    /// <summary>数据域起始偏移（系统时钟起始位置）</summary>
    public const int OffsetDataDomain = 11;

    // ===== 数据域内部偏移（相对于数据域起始）=====

    /// <summary>系统时钟长度（年月日时分秒，6B BCD）</summary>
    public const int SystemClockLength = 6;

    /// <summary>设备类型字段长度</summary>
    public const int DeviceTypeLength = 1;

    /// <summary>数据标识长度（2B，ID0 低字节 + ID1 高字节）</summary>
    public const int DataIdentifierLength = 2;

    /// <summary>帧序号长度</summary>
    public const int FrameSequenceLength = 1;

    /// <summary>数据域头部固定长度（系统时钟 + 设备类型 + 数据标识 + 帧序号）</summary>
    public const int DataDomainHeaderLength = SystemClockLength + DeviceTypeLength + DataIdentifierLength + FrameSequenceLength;

    // ===== 控制码位域掩码 =====

    /// <summary>D7: 数据方向（0=主站到表具，1=表具到主站）</summary>
    public const byte DirectionMask = 0x80;

    /// <summary>D6: 从站应答标志（0=正常应答，1=异常应答）</summary>
    public const byte ResponseErrorMask = 0x40;

    /// <summary>D5: 多帧标志（0=单帧通讯，1=多帧通讯）</summary>
    public const byte MultiFrameMask = 0x20;

    /// <summary>D4~D0: 功能分类</summary>
    public const byte FunctionCategoryMask = 0x1F;

    // ===== 数据标识（数据帧识别符，低位在前）=====

    /// <summary>出厂配置参数 — 读(0x01)/写(0x04)</summary>
    public const ushort DataIdFactoryConfig = 0xC518;

    /// <summary>加密平台信息 — 读(0x01)/写(0x04)</summary>
    public const ushort DataIdEncryptionInfo = 0xC519;

    /// <summary>配置参数 — 读(0x01)/写(0x04)</summary>
    public const ushort DataIdConfigParams = 0xC620;

    /// <summary>网络参数及终端号 — 读(0x01)</summary>
    public const ushort DataIdNetworkParams = 0xC525;

    /// <summary>设备运行信息 — 读(0x01)</summary>
    public const ushort DataIdDeviceInfo = 0xC527;

    /// <summary>蓝牙控制 — 写(0x04)</summary>
    public const ushort DataIdBluetoothControl = 0xA027;

    // ===== 长度常量 =====

    /// <summary>表具编号 BCD 长度（6 字节，表示 13 位十进制表号）</summary>
    public const int MeterNumberBcdLength = 7;

    /// <summary>表具编号 ASCII 长度（加密平台信息中使用 32 字节）</summary>
    public const int MeterNumberAsciiLength = 32;

    /// <summary>表具时间长度（6B BCD: YYMMDDhhmmss）</summary>
    public const int MeterTimeLength = 6;

    /// <summary>加密密钥长度</summary>
    public const int EncryptionKeyLength = 32;

    /// <summary>认证密钥长度</summary>
    public const int AuthKeyLength = 32;

    /// <summary>Onenet 平台参数长度</summary>
    public const int OnenetParamsLength = 15;

    /// <summary>IMEI 号长度（15 ASCII）</summary>
    public const int ImeiLength = 15;

    /// <summary>IMSI 号长度（15 或 20 ASCII）</summary>
    public const int ImsiLength = 20;

    /// <summary>ICCID 号长度（20 ASCII）</summary>
    public const int IccidLength = 20;

    /// <summary>表具状态字长度（4B）</summary>
    public const int MeterStatusWordLength = 4;

    /// <summary>表具版本长度（6B: 协议版本1B + 软件版本2B + 硬件版本2B + 保留1B）</summary>
    public const int MeterVersionLength = 6;

    /// <summary>蓝牙模块 SN 号长度（12 ASCII）</summary>
    public const int BluetoothSnLength = 12;

    /// <summary>构建控制码
    /// <para>根据数据方向、应答标志、多帧标志和功能分类组合生成控制码字节。</para>
    /// </summary>
    /// <param name="direction">数据方向：MasterToMeter(主→表) 或 MeterToMaster(表→主)</param>
    /// <param name="function">功能分类：ReadData/ReadRecord/WriteSystemOp/WriteData/ActiveReport</param>
    /// <param name="isError">是否异常应答（默认 false）</param>
    /// <param name="isMultiFrame">是否多帧通讯（默认 false）</param>
    /// <returns>组合后的控制码字节</returns>
    /// <example>
    /// <code>
    /// // 主站读取数据：0x01
    /// byte readCtrl = ViewshineProtocolConstants.BuildControlCode(
    ///     DataDirection.MasterToMeter, FunctionCategory.ReadData);
    ///
    /// // 表具正常应答读数据：0x81
    /// byte respCtrl = ViewshineProtocolConstants.BuildControlCode(
    ///     DataDirection.MeterToMaster, FunctionCategory.ReadData);
    ///
    /// // 主站写数据：0x04
    /// byte writeCtrl = ViewshineProtocolConstants.BuildControlCode(
    ///     DataDirection.MasterToMeter, FunctionCategory.WriteData);
    /// </code>
    /// </example>
    public static byte BuildControlCode(
        DataDirection direction,
        FunctionCategory function,
        bool isError = false,
        bool isMultiFrame = false)
    {
        byte code = 0;
        if (direction == DataDirection.MeterToMaster)
            code |= DirectionMask;
        if (isError)
            code |= ResponseErrorMask;
        if (isMultiFrame)
            code |= MultiFrameMask;
        code |= (byte)((byte)function & FunctionCategoryMask);
        return code;
    }

    /// <summary>从控制码提取数据方向</summary>
    public static DataDirection GetDirection(byte controlCode)
        => (controlCode & DirectionMask) != 0 ? DataDirection.MeterToMaster : DataDirection.MasterToMeter;

    /// <summary>从控制码判断是否异常应答</summary>
    public static bool IsErrorResponse(byte controlCode)
        => (controlCode & ResponseErrorMask) != 0;

    /// <summary>从控制码判断是否多帧通讯</summary>
    public static bool IsMultiFrame(byte controlCode)
        => (controlCode & MultiFrameMask) != 0;

    /// <summary>从控制码提取功能分类</summary>
    public static FunctionCategory GetFunctionCategory(byte controlCode)
        => (FunctionCategory)(controlCode & FunctionCategoryMask);
}

/// <summary>
/// 数据方向枚举（控制码 D7 位）
/// </summary>
public enum DataDirection
{
    /// <summary>主站到表具（D7=0）</summary>
    MasterToMeter = 0,

    /// <summary>表具到主站（D7=1）</summary>
    MeterToMaster = 1
}

/// <summary>
/// 功能分类枚举（控制码 D4~D0 位）
/// </summary>
public enum FunctionCategory
{
    /// <summary>保留（0000）</summary>
    Reserved = 0x00,

    /// <summary>读数据（0001）</summary>
    ReadData = 0x01,

    /// <summary>读记录（0010）</summary>
    ReadRecord = 0x02,

    /// <summary>写系统操作（0011）</summary>
    WriteSystemOperation = 0x03,

    /// <summary>写数据（0100）</summary>
    WriteData = 0x04,

    /// <summary>主动上报（0101）</summary>
    ActiveReport = 0x05
}

/// <summary>
/// 设备类型枚举（数据域中的 T 字段）
/// </summary>
public enum DeviceType : byte
{
    /// <summary>保留</summary>
    Reserved = 0x00,

    /// <summary>民用表（0011）</summary>
    CivilMeter = 0x03,

    /// <summary>民用物联网表（1000）</summary>
    CivilIotMeter = 0x08,

    /// <summary>所有设备（1111）</summary>
    AllDevices = 0x0F
}

/// <summary>
/// 表具规格枚举（出厂配置参数中的表具规格字段）
/// </summary>
public enum MeterSpecification : byte
{
    /// <summary>G6</summary>
    G6 = 0x03,

    /// <summary>G10</summary>
    G10 = 0x04,

    /// <summary>G16</summary>
    G16 = 0x05,

    /// <summary>G25</summary>
    G25 = 0x06
}

/// <summary>
/// 运行模式枚举
/// </summary>
public enum RunMode : byte
{
    /// <summary>表具后付费</summary>
    PostPaid = 0x00,

    /// <summary>表具预付费</summary>
    PrePaid = 0x01,

    /// <summary>后台预付费</summary>
    BackendPrePaid = 0x02
}

/// <summary>
/// 表计费类型枚举
/// </summary>
public enum BillingType : byte
{
    /// <summary>金额式</summary>
    Amount = 0x00,

    /// <summary>气量</summary>
    Volume = 0x01
}

/// <summary>
/// 通讯板的网络类型枚举
/// </summary>
public enum NetworkType : byte
{
    /// <summary>TCP</summary>
    Tcp = 0x00,

    /// <summary>UDP</summary>
    Udp = 0x01,

    /// <summary>COAP</summary>
    Coap = 0x02,

    /// <summary>MQTT</summary>
    Mqtt = 0x03,

    /// <summary>LwM2M (OneNet)</summary>
    LwM2mOneNet = 0x04,

    /// <summary>LwM2M (AEP)</summary>
    LwM2mAep = 0x05
}

/// <summary>
/// 表具类型枚举（读表具参数中的表具类型字段）
/// </summary>
public enum MeterType : byte
{
    /// <summary>霍尔</summary>
    Hall = 0x00,

    /// <summary>光电</summary>
    Photoelectric = 0x01,

    /// <summary>超声波</summary>
    Ultrasonic = 0x02
}

/// <summary>
/// 通讯类型枚举（读表具参数中的通讯类型字段）
/// </summary>
public enum CommunicationType : byte
{
    /// <summary>移动流量</summary>
    ChinaMobile = 0x00,

    /// <summary>电信流量</summary>
    ChinaTelecom = 0x01
}

/// <summary>
/// 温度传感器类型枚举
/// </summary>
public enum TemperatureSensorType : byte
{
    /// <summary>TI tmp102</summary>
    TiTmp102 = 0x00,

    /// <summary>华普 HP6862I</summary>
    HuapuHp6862i = 0x01,

    /// <summary>智感 as55</summary>
    ZhiganAs55 = 0x02,

    /// <summary>龙威 lwp5200ad</summary>
    LongweiLwp5200ad = 0x03,

    /// <summary>无</summary>
    None = 0xFF
}

/// <summary>
/// 压力传感器类型枚举
/// </summary>
public enum PressureSensorType : byte
{
    /// <summary>保留</summary>
    Reserved = 0x00,

    /// <summary>华普 HP6862I</summary>
    HuapuHp6862i = 0x01,

    /// <summary>智感 as55</summary>
    ZhiganAs55 = 0x02,

    /// <summary>龙威 lwp5200ad</summary>
    LongweiLwp5200ad = 0x03,

    /// <summary>无</summary>
    None = 0xFF
}

/// <summary>
/// 表具生产状态枚举
/// </summary>
public enum ProductionStatus : byte
{
    /// <summary>工厂态（生产态）</summary>
    Factory = 0x00,

    /// <summary>出厂态（用户态）</summary>
    Shipped = 0x01,

    /// <summary>未校表</summary>
    Uncalibrated = 0x02,

    /// <summary>已校表</summary>
    Calibrated = 0x03,

    /// <summary>校表出厂</summary>
    CalibratedAndShipped = 0x04
}

/// <summary>
/// 加密芯片状态枚举
/// </summary>
public enum EncryptionChipStatus : byte
{
    /// <summary>加密芯片不存在或异常</summary>
    NotExistOrError = 0x00,

    /// <summary>加密芯片空片</summary>
    Blank = 0x01,

    /// <summary>加密芯片未灌装，未发行</summary>
    NotProgrammed = 0x02,

    /// <summary>加密芯片已灌装，已发行</summary>
    Programmed = 0x03
}

/// <summary>
/// 阀门状态枚举（表具状态字 B2~B0）
/// </summary>
public enum ValveStatus : byte
{
    /// <summary>状态无（000）</summary>
    None = 0b000,

    /// <summary>开阀（001）</summary>
    Open = 0b001,

    /// <summary>关阀（011）</summary>
    Closed = 0b011,

    /// <summary>异常（101）</summary>
    Abnormal = 0b101,

    /// <summary>阀门权关（111）</summary>
    ForceClosed = 0b111
}

/// <summary>
/// 电压状态枚举（表具状态字 B10~B9）
/// </summary>
public enum VoltageStatus : byte
{
    /// <summary>正常（00）</summary>
    Normal = 0b00,

    /// <summary>欠压（01）</summary>
    UnderVoltage = 0b01,

    /// <summary>低压（10）</summary>
    LowVoltage = 0b10,

    /// <summary>掉电（11）</summary>
    PowerOff = 0b11
}

/// <summary>
/// 蓝牙控制操作枚举
/// </summary>
public enum BluetoothOperation : byte
{
    /// <summary>关闭蓝牙</summary>
    Close = 0x00,

    /// <summary>打开蓝牙</summary>
    Open = 0x01,

    /// <summary>搜索设备</summary>
    Search = 0x02
}

/// <summary>
/// 抄表类型枚举（抄表时间参数中的类型字段）
/// </summary>
public enum ReadingType : byte
{
    /// <summary>每天（0x00）</summary>
    Daily = 0x00,

    /// <summary>单月（0x01）</summary>
    OddMonth = 0x01,

    /// <summary>双月（0x02）</summary>
    EvenMonth = 0x02,

    /// <summary>每月（0x03）</summary>
    Monthly = 0x03,

    /// <summary>按分（0x04）</summary>
    ByMinute = 0x04,

    /// <summary>按时（0x05）</summary>
    ByHour = 0x05,

    /// <summary>指定5时（0x07）</summary>
    Specified5Hours = 0x07,

    /// <summary>指定5日（0x08）</summary>
    Specified5Days = 0x08,

    /// <summary>按天（0x09）</summary>
    ByDay = 0x09
}
