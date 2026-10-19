// =============================================================================
// 文件名：FrameStreamBuffer.cs
// 功能描述：通用流式收帧缓冲。
//           职责单一：不断 Append 到达的字节，反复 TryParseFrame 抽出完整帧，
//           内部处理「前导噪声定位、粘包、半帧、校验失败跳帧重同步」。
//           取代 IMDS 各 SerialPortManager 里手写的 _buffer.IndexOf(0x68) 循环
//           与 AutoFrock 各模块的 m_effectiveData 去头去尾逻辑。
// =============================================================================
using IMDP.Domain.Products;

namespace IMDP.Infrastructure.Protocol;

/// <summary>
/// 通用流式收帧缓冲
/// <para>典型用法：串口/TCP 每次到达数据调用 <see cref="Append"/>，
/// 随后循环调用 <see cref="TryParseFrame"/> 直到返回 false（缓冲不足一帧）。</para>
/// </summary>
public sealed class FrameStreamBuffer
{
    private readonly List<byte> _buffer = new();
    private readonly object _lock = new();
    /// <summary>缓冲区上限（默认 256KB），超过则整体清空防内存膨胀。</summary>
    private readonly int _maxCapacity;

    /// <summary>构造收帧缓冲。</summary>
    /// <param name="maxCapacity">缓冲上限字节数，默认 262144。</param>
    public FrameStreamBuffer(int maxCapacity = 256 * 1024)
    {
        _maxCapacity = maxCapacity;
    }

    /// <summary>当前缓冲字节数（尚未解析出的残留数据）。</summary>
    public int BufferedByteCount { get { lock (_lock) return _buffer.Count; } }

    /// <summary>清空缓冲。</summary>
    public void Clear() { lock (_lock) _buffer.Clear(); }

    /// <summary>快照当前缓冲内容（用于超时诊断日志）。</summary>
    public byte[] SnapshotBufferedBytes() { lock (_lock) return _buffer.ToArray(); }

    /// <summary>
    /// 追加一批到达的字节（线程安全，可从接收回调线程直接调用）。
    /// </summary>
    /// <param name="data">本次到达的字节。</param>
    public void Append(byte[] data)
    {
        if (data == null || data.Length == 0) return;
        lock (_lock)
        {
            if (_buffer.Count + data.Length > _maxCapacity)
                _buffer.Clear();
            _buffer.AddRange(data);
        }
    }

    /// <summary>
    /// 尝试从缓冲中解析出一帧完整数据。
    /// <para>自动跳过帧头之前的噪声；校验失败的帧同样消费并返回（ErrorMessage 非空），
    /// 调用方可据此记录坏帧后继续抽下一帧；缓冲不足一帧时返回 false。</para>
    /// </summary>
    /// <param name="format">帧格式配置。</param>
    /// <param name="result">解析结果（含帧数据/工位/命令/数据区）。</param>
    /// <returns>本次抽出一帧（无论校验是否通过）返回 true；缓冲不足返回 false。</returns>
    public bool TryParseFrame(FrameFormatConfig format, out FrameParseResult result)
    {
        result = new FrameParseResult { IsComplete = false };
        lock (_lock)
        {
            if (_buffer.Count == 0) return false;

            byte[] head = format.FrameHeader;

            // 1. 扫描帧头：丢弃帧头前的垃圾字节（残帧/噪声）。快照一次，避免逐字节拷贝。
            byte[] snapshot = _buffer.ToArray();
            int count = snapshot.Length;
            int start = 0;
            while (start <= count - head.Length && !FrameComposer.MatchHeader(format, snapshot, start))
                start++;

            if (start > 0)
                _buffer.RemoveRange(0, start);

            // 2. 交给 FrameComposer 解析（含长度字段判定与校验）；噪声移除后重取快照
            snapshot = _buffer.ToArray();
            count = snapshot.Length;
            if (count == 0) return false;
            var parsed = FrameComposer.TryParseFrame(format, snapshot, 0, count);

            // 3a. 数据不足（半帧/缺长度字段）或帧头不匹配：保留缓冲等待后续数据
            if (!parsed.IsComplete && parsed.ErrorMessage != null
                && (parsed.ErrorMessage.Contains("不足") || parsed.ErrorMessage.Contains("帧头不匹配")))
                return false;

            // 3b. 完整帧（校验通过）或校验错误帧：均消费对应字节并返回
            int consumed = parsed.ConsumedLength > 0 ? parsed.ConsumedLength : count;
            _buffer.RemoveRange(0, Math.Min(consumed, count));
            result = parsed;
            return true;
        }
    }

}
