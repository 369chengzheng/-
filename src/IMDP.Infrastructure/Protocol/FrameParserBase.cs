// =============================================================================
// 文件名：FrameParserBase.cs
// 功能描述：通用帧解析器基类
//           实现 IFrameParser 接口，组帧/解帧/响应匹配等通用逻辑委托给
//           FrameComposer 静态共用工具，避免与具体协议耦合。子类仅需提供
//           ProtocolId / FrameFormat / Description，如需自定义帧结构可重写对应虚方法。
// =============================================================================
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 通用帧解析器基类（产品插件可继承并自定义协议细节）
/// <para>实现 <see cref="IFrameParser"/> 接口，组帧/解帧/响应匹配委托给
/// <see cref="FrameComposer"/> 静态共用工具（单一逻辑来源）。子类通过重写
/// <see cref="FrameFormat"/> 等属性定义协议格式，必要时可重写
/// <see cref="BuildFrame"/> / <see cref="TryParseFrame"/> / <see cref="IsResponseMatch"/> 自定义行为。</para>
/// </summary>
public abstract class FrameParserBase : IFrameParser
{
    /// <summary>协议标识，由子类提供。</summary>
    public abstract ProtocolIdentifier ProtocolId { get; }

    /// <summary>帧格式配置，由子类提供。</summary>
    public abstract FrameFormatConfig FrameFormat { get; }

    /// <summary>协议描述文本，由子类提供。</summary>
    public abstract string Description { get; }

    /// <summary>
    /// 尝试从缓冲区解析一帧数据（委托 FrameComposer）
    /// </summary>
    public virtual FrameParseResult TryParseFrame(byte[] buffer, int offset, int length)
        => FrameComposer.TryParseFrame(FrameFormat, buffer, offset, length);

    /// <summary>
    /// 根据帧格式构建一帧数据（委托 FrameComposer）
    /// </summary>
    public virtual byte[] BuildFrame(byte deviceAddress, byte[] command)
        => FrameComposer.BuildFrame(FrameFormat, deviceAddress, command);

    /// <summary>
    /// 判断响应帧是否与请求帧匹配（委托 FrameComposer）
    /// </summary>
    public virtual bool IsResponseMatch(byte[] response, byte[] request)
        => FrameComposer.IsResponseMatch(FrameFormat, response, request);
}
