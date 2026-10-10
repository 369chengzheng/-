// =============================================================================
// 文件名：ProtocolManager.cs
// 功能描述：协议管理器实现
//           维护多个产品协议解析器（IFrameParser）的注册表，支持协议切换、
//           按协议标识获取解析器，以及尝试用任一已注册协议解析缓冲区数据。
// =============================================================================
using IMDP.Domain.Interfaces;
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 协议管理器实现
/// <para>实现 <see cref="IProtocolManager"/>，统一管理多产品协议解析器的注册、切换与查询；
/// 当切换协议时通过 <see cref="ProtocolChanged"/> 事件通知订阅者。</para>
/// </summary>
public class ProtocolManager : IProtocolManager
{
    /// <summary>已注册协议解析器字典（按协议标识索引）。</summary>
    private readonly Dictionary<ProtocolIdentifier, IFrameParser> _parsers = new();
    /// <summary>当前激活的协议标识。</summary>
    private ProtocolIdentifier _activeProtocol;

    /// <summary>当前激活的协议标识。</summary>
    public ProtocolIdentifier ActiveProtocol => _activeProtocol;
    /// <summary>协议切换事件，当激活协议变更时触发。</summary>
    public event EventHandler<ProtocolChangedEventArgs>? ProtocolChanged;

    /// <summary>
    /// 注册协议解析器。
    /// <para>若解析器为首个注册项，则自动设为激活协议。</para>
    /// </summary>
    /// <param name="parser">协议解析器实例。</param>
    public void RegisterParser(IFrameParser parser)
    {
        // 仅在未注册时添加，避免重复注册覆盖
        if (!_parsers.ContainsKey(parser.ProtocolId))
            _parsers[parser.ProtocolId] = parser;

        // 首个注册项自动设为激活协议
        if (_parsers.Count == 1)
            _activeProtocol = parser.ProtocolId;
    }

    /// <summary>
    /// 注销指定协议解析器。
    /// <para>若注销的是当前激活协议，则自动切换到首个剩余协议。</para>
    /// </summary>
    /// <param name="protocolId">待注销的协议标识。</param>
    public void UnregisterParser(ProtocolIdentifier protocolId)
    {
        _parsers.Remove(protocolId);
        // 若注销的是当前激活协议，则切换到首个剩余协议
        if (_activeProtocol.Equals(protocolId) && _parsers.Count > 0)
            _activeProtocol = _parsers.Keys.First();
    }

    /// <summary>
    /// 按协议标识获取解析器。
    /// </summary>
    /// <param name="protocolId">协议标识。</param>
    /// <returns>解析器实例；未找到时返回 null。</returns>
    public IFrameParser? GetParser(ProtocolIdentifier protocolId)
    {
        return _parsers.TryGetValue(protocolId, out var parser) ? parser : null;
    }

    /// <summary>获取全部已注册协议标识。</summary>
    /// <returns>协议标识集合。</returns>
    public IEnumerable<ProtocolIdentifier> GetRegisteredProtocols() => _parsers.Keys;

    /// <summary>
    /// 切换激活协议。
    /// </summary>
    /// <param name="protocolId">目标协议标识。</param>
    /// <exception cref="ArgumentException">当目标协议未注册时抛出。</exception>
    public void SwitchProtocol(ProtocolIdentifier protocolId)
    {
        if (!_parsers.ContainsKey(protocolId))
            throw new ArgumentException($"未找到协议: {protocolId.Name}");

        var previous = _activeProtocol;
        _activeProtocol = protocolId;

        // 触发协议切换事件
        ProtocolChanged?.Invoke(this, new ProtocolChangedEventArgs
        {
            PreviousProtocol = previous,
            NewProtocol = protocolId
        });
    }

    /// <summary>
    /// 尝试用任一已注册协议解析缓冲区数据。
    /// </summary>
    /// <param name="buffer">数据缓冲区。</param>
    /// <param name="offset">起始偏移量。</param>
    /// <param name="length">可用数据长度。</param>
    /// <returns>匹配的解析器与解析结果；均不匹配时返回 (null, null)。</returns>
    public (IFrameParser? Parser, FrameParseResult? Result) TryParseWithAnyProtocol(byte[] buffer, int offset, int length)
    {
        // 遍历所有已注册解析器，返回首个成功解析的结果
        foreach (var parser in _parsers.Values)
        {
            var result = parser.TryParseFrame(buffer, offset, length);
            if (result.IsComplete)
                return (parser, result);
        }
        // 所有解析器均不匹配
        return (null, null);
    }
}
