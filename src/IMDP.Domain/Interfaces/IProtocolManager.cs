// =============================================================================
// 文件名：IProtocolManager.cs
// 功能描述：帧解析器与协议管理器接口，定义多协议注册、切换、解析与匹配契约。
//           由产品插件实现 IFrameParser，由 Infrastructure 层实现 IProtocolManager。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 帧解析器接口（由产品插件实现）
/// </summary>
public interface IFrameParser
{
    /// <summary>本解析器对应的协议标识</summary>
    ProtocolIdentifier ProtocolId { get; }

    /// <summary>本解析器对应的帧格式配置（帧头、帧尾、长度字段等定义）</summary>
    FrameFormatConfig FrameFormat { get; }

    /// <summary>尝试从缓冲区解析帧（支持粘包/拆包）</summary>
    FrameParseResult TryParseFrame(byte[] buffer, int offset, int length);

    /// <summary>构建发送帧</summary>
    byte[] BuildFrame(byte deviceAddress, byte[] command);

    /// <summary>匹配响应帧（判断是否是当前协议的响应）</summary>
    bool IsResponseMatch(byte[] response, byte[] request);

    /// <summary>协议描述文本（用于 UI 展示与日志）</summary>
    string Description { get; }
}

/// <summary>
/// 协议管理器（管理多协议注册、切换、解析）
/// </summary>
public interface IProtocolManager
{
    /// <summary>注册一个帧解析器</summary>
    /// <param name="parser">帧解析器实例</param>
    void RegisterParser(IFrameParser parser);

    /// <summary>按协议标识注销帧解析器</summary>
    /// <param name="protocolId">协议标识</param>
    void UnregisterParser(ProtocolIdentifier protocolId);

    /// <summary>获取所有已注册的协议标识</summary>
    /// <returns>已注册协议标识集合</returns>
    IEnumerable<ProtocolIdentifier> GetRegisteredProtocols();

    /// <summary>按协议标识获取帧解析器</summary>
    /// <param name="protocolId">协议标识</param>
    /// <returns>匹配的解析器；不存在时返回 null</returns>
    IFrameParser? GetParser(ProtocolIdentifier protocolId);

    /// <summary>切换当前激活协议</summary>
    /// <param name="protocolId">目标协议标识</param>
    void SwitchProtocol(ProtocolIdentifier protocolId);

    /// <summary>当前激活的协议标识</summary>
    ProtocolIdentifier ActiveProtocol { get; }

    /// <summary>用所有已注册协议依次尝试解析缓冲区中的帧</summary>
    /// <param name="buffer">数据缓冲区</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="length">数据长度</param>
    /// <returns>匹配的解析器与解析结果；均不匹配时解析器与结果均为 null</returns>
    (IFrameParser? Parser, FrameParseResult? Result) TryParseWithAnyProtocol(byte[] buffer, int offset, int length);

    /// <summary>协议切换事件（切换成功后触发）</summary>
    event EventHandler<ProtocolChangedEventArgs>? ProtocolChanged;
}

/// <summary>
/// 协议切换事件参数
/// <para>携带切换前后的协议标识，供订阅者感知协议变更。</para>
/// </summary>
public class ProtocolChangedEventArgs : EventArgs
{
    /// <summary>切换前的协议标识</summary>
    public ProtocolIdentifier PreviousProtocol { get; set; }

    /// <summary>切换后的协议标识</summary>
    public ProtocolIdentifier NewProtocol { get; set; }
}
