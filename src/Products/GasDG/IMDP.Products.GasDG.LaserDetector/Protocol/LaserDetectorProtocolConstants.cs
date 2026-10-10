// =============================================================================
// 文件名：LaserDetectorProtocolConstants.cs
// 功能描述：家用可燃气体激光检测仪生产测试通讯协议常量定义。
//           帧结构：68 + 指令位 + 整包长度 + 工位 + 数据域 + 和校验 + 16。
// =============================================================================
namespace IMDP.Products.GasDG.LaserDetector.Protocol;

/// <summary>
/// 激光检测仪协议常量
/// <para>集中定义帧标记、指令位、响应位与数据长度等协议常量，供指令构建与响应解析共用。</para>
/// </summary>
public static class LaserDetectorProtocolConstants
{
    /// <summary>帧头字节</summary>
    public const byte FrameHeader = 0x68;

    /// <summary>帧尾字节</summary>
    public const byte FrameTail = 0x16;

    /// <summary>开始测试指令位</summary>
    public const byte StartTestFlag = 0xAA;

    /// <summary>开始测试响应指令位</summary>
    public const byte StartTestResponseFlag = 0xAB;

    /// <summary>查询测试结果指令位</summary>
    public const byte QueryTestResultFlag = 0xAC;

    /// <summary>测试结果响应指令位</summary>
    public const byte TestResultResponseFlag = 0xAD;

    /// <summary>最短协议帧整包长度（帧头 + 指令 + 长度 + 工位 + 校验 + 帧尾）</summary>
    public const byte ShortFrameLength = 0x06;

    /// <summary>测试结果响应数据域固定长度</summary>
    public const int TestResultDataLength = 91;

    /// <summary>测试结果响应数据域在帧中的起始偏移</summary>
    public const int TestResultDataOffset = 4;

    /// <summary>协议支持的工位数上限（工位 1～3）</summary>
    public const int StationCount = 3;

    /// <summary>判断字节是否为协议支持的工装响应指令位
    /// <para>仅 0xAB（开始测试响应）与 0xAD（测试结果响应）为合法响应。</para>
    /// </summary>
    /// <param name="responseFlag">待判断的响应指令位</param>
    /// <returns>是支持的响应指令位时返回 true，否则返回 false</returns>
    public static bool IsSupportedResponseFlag(byte responseFlag)
    {
        return responseFlag == StartTestResponseFlag || responseFlag == TestResultResponseFlag;
    }
}
