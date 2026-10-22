// =============================================================================
// 文件名：SerialFrameMatchBuffer.cs
// 功能描述：串口接收帧公用匹配缓冲（List + 锁 + 滑动窗口提取）。
//           协议方只需注入「从字节流中提取一帧完整数据帧」的方法，即可获得：
//           线程安全缓冲、按自定义条件匹配数据帧（TryMatchFrame 返回 bool + 数据帧）、
//           带超时的异步等待（WaitForFrameAsync）。
//           典型用法：每个工位各建一个实例，串口原始数据事件广播 Append 到各实例，
//           各工位并行等待属于自己的应答帧，互不消费对方的数据帧。
// =============================================================================
namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口接收帧匹配缓冲
/// <para>内部以 List 缓存原始字节并加锁保护，通过注入的帧提取方法反复滑动提取完整帧；
/// 调用方以匹配委托按序扫描已提取帧，命中即移除并返回，未命中的帧保留至超期自动清理，
/// 保证多工位并行等待时互不抢占对方的数据帧。</para>
/// </summary>
public sealed class SerialFrameMatchBuffer : IDisposable
{
    #region 内部类型

    /// <summary>已提取的待匹配完整帧（含接收时间，用于超期清理）</summary>
    private sealed class PendingFrame
    {
        /// <summary>完整数据帧</summary>
        public byte[] Frame { get; init; } = Array.Empty<byte>();

        /// <summary>帧进入缓冲的时间</summary>
        public DateTime ReceivedTime { get; init; }
    }

    /// <summary>注册中的异步等待者（匹配委托 + 完成源）</summary>
    private sealed class FrameWaiter
    {
        /// <summary>帧匹配委托</summary>
        public Func<byte[], bool> Matcher { get; init; } = _ => false;

        /// <summary>等待完成源（结果 = 匹配到的数据帧；未等到时为 null）</summary>
        public TaskCompletionSource<byte[]?> Completion { get; init; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    #endregion

    #region 字段与构造

    /// <summary>帧提取方法：从字节流中提取一帧完整数据帧（滑动窗口），无完整帧时返回 null</summary>
    private readonly Func<byte[], byte[]?> _tryExtractFrame;

    /// <summary>待匹配帧的保留时长（超期自动清理，防止无关帧无限堆积）</summary>
    private readonly TimeSpan _pendingFrameTtl;

    /// <summary>原始字节缓冲（含前导噪声、半包残留与粘包，锁内访问）</summary>
    private readonly List<byte> _receiveBuffer = new();

    /// <summary>已提取的待匹配完整帧列表（锁内访问）</summary>
    private readonly List<PendingFrame> _pendingFrames = new();

    /// <summary>注册中的异步等待者列表（锁内访问）</summary>
    private readonly List<FrameWaiter> _waiters = new();

    /// <summary>保护全部 List 的同步锁</summary>
    private readonly object _lock = new();

    /// <summary>原始字节缓冲上限（超过视为噪声流整体清空，防内存膨胀）</summary>
    private const int MaxReceiveBufferBytes = 64 * 1024;

    /// <summary>默认待匹配帧保留时长（30 秒）</summary>
    private static readonly TimeSpan DefaultPendingFrameTtl = TimeSpan.FromSeconds(30);

    /// <summary>是否已释放</summary>
    private bool _disposed;

    /// <summary>
    /// 构造串口接收帧匹配缓冲
    /// </summary>
    /// <param name="tryExtractFrame">
    /// 帧提取方法：入参为当前全部原始字节（可含前导噪声、半包、粘包），
    /// 返回首个完整且校验正确的数据帧；提取不到时返回 null。
    /// 该方法在锁内调用，必须为无副作用的快速纯计算。</param>
    /// <param name="pendingFrameTtl">待匹配帧保留时长（默认 30 秒，超期自动清理）</param>
    /// <exception cref="ArgumentNullException">帧提取方法为 null 时抛出</exception>
    public SerialFrameMatchBuffer(Func<byte[], byte[]?> tryExtractFrame, TimeSpan? pendingFrameTtl = null)
    {
        ArgumentNullException.ThrowIfNull(tryExtractFrame);
        _tryExtractFrame = tryExtractFrame;
        _pendingFrameTtl = pendingFrameTtl ?? DefaultPendingFrameTtl;
    }

    #endregion

    #region 数据接入

    /// <summary>
    /// 追加一次串口接收数据（线程安全，可从串口接收回调线程直接调用）
    /// <para>锁内滑动提取全部完整帧入待匹配列表，并按注册顺序为等待者分配首个匹配帧；
    /// 提取后的半包残留保留在原始缓冲中等待后续数据补齐。</para>
    /// </summary>
    /// <param name="data">本次接收的原始字节数据</param>
    public void Append(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // 等待者完成列表（锁内收集，锁外完成，避免任务续接代码在锁内执行）
        var completions = new List<(TaskCompletionSource<byte[]?> Completion, byte[]? Frame)>();
        lock (_lock)
        {
            if (_disposed || data.Length == 0)
            {
                return;
            }

            _receiveBuffer.AddRange(data);
            if (_receiveBuffer.Count > MaxReceiveBufferBytes)
            {
                // 溢出保护：正常协议交互远达不到该上限，超过按噪声流整体清空
                _receiveBuffer.Clear();
            }

            // 反复滑动提取：一次接收可能携带多帧粘包
            while (_tryExtractFrame(_receiveBuffer.ToArray()) is { } frame)
            {
                int consumedIndex = IndexOfSequence(_receiveBuffer, frame);
                if (consumedIndex < 0)
                {
                    break; // 理论不可达（帧必来自本缓冲）：防御性退出，避免死循环
                }

                _receiveBuffer.RemoveRange(0, consumedIndex + frame.Length);
                _pendingFrames.Add(new PendingFrame { Frame = frame, ReceivedTime = DateTime.Now });
            }

            RemoveExpiredFramesLocked(DateTime.Now);

            // 按注册顺序为等待者分配首个匹配帧（先到先得，帧不会被无关等待者抢占）
            var matchedWaiters = new List<FrameWaiter>();
            foreach (var waiter in _waiters)
            {
                if (!TryTakeFrameLocked(waiter.Matcher, out byte[]? matched))
                {
                    continue;
                }

                completions.Add((waiter.Completion, matched));
                matchedWaiters.Add(waiter);
            }

            foreach (var matchedWaiter in matchedWaiters)
            {
                _waiters.Remove(matchedWaiter);
            }
        }

        // 锁外唤醒等待者
        foreach (var (completion, frame) in completions)
        {
            completion.TrySetResult(frame);
        }
    }

    #endregion

    #region 帧匹配

    /// <summary>
    /// 公用帧匹配方法：按传入的匹配条件从已提取帧中取出首个命中的数据帧
    /// <para>命中即从待匹配列表移除该帧（同一帧不会被重复返回）；
    /// 未命中的帧保留在缓冲中，供其他匹配条件使用或超期自动清理。</para>
    /// </summary>
    /// <param name="matcher">帧匹配方法（在锁内调用，必须为无副作用的快速判断）</param>
    /// <param name="frame">命中时返回该完整数据帧；未命中时返回空数组</param>
    /// <returns>存在匹配帧时返回 true；否则返回 false</returns>
    /// <exception cref="ArgumentNullException">匹配方法为 null 时抛出</exception>
    public bool TryMatchFrame(Func<byte[], bool> matcher, out byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(matcher);

        lock (_lock)
        {
            if (!_disposed && TryTakeFrameLocked(matcher, out byte[]? matched) && matched != null)
            {
                frame = matched;
                return true;
            }
        }

        frame = Array.Empty<byte>();
        return false;
    }

    /// <summary>
    /// 异步等待匹配帧：先同步匹配一次，未命中则注册等待者，
    /// 直到新到达的帧匹配成功、超时或被取消
    /// </summary>
    /// <param name="matcher">帧匹配方法（在锁内调用，必须为无副作用的快速判断）</param>
    /// <param name="timeoutMs">等待超时（毫秒）</param>
    /// <param name="cancellationToken">取消令牌（可选，取消时抛出 OperationCanceledException）</param>
    /// <returns>等到匹配帧时返回该帧；超时或缓冲已释放时返回 null</returns>
    /// <exception cref="ArgumentNullException">匹配方法为 null 时抛出</exception>
    public async Task<byte[]?> WaitForFrameAsync(
        Func<byte[], bool> matcher,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matcher);

        // 快路径：缓冲中已有匹配帧
        if (TryMatchFrame(matcher, out byte[] matched))
        {
            return matched;
        }

        var waiter = new FrameWaiter { Matcher = matcher };
        lock (_lock)
        {
            // 缓冲已释放（关闭串口/重载配置的全量清理）：语义为「永远不会再有数据」，
            // 等待方按未等到帧处理（返回 null），由调用方走既有的通信失败路径优雅退出。
            if (_disposed)
            {
                return null;
            }

            _waiters.Add(waiter);
        }

        // 注册后二次检查：等待者可能已被并发唤醒，缓冲也可能刚到达匹配帧
        if (waiter.Completion.Task.IsCompleted)
        {
            return await waiter.Completion.Task.ConfigureAwait(false);
        }

        if (TryMatchFrame(matcher, out matched))
        {
            RemoveWaiter(waiter);
            return matched;
        }

        try
        {
            var timeoutTask = Task.Delay(timeoutMs, cancellationToken);
            var completedTask = await Task.WhenAny(waiter.Completion.Task, timeoutTask).ConfigureAwait(false);
            if (completedTask == waiter.Completion.Task)
            {
                return await waiter.Completion.Task.ConfigureAwait(false);
            }

            // 超时任务先完成：区分「取消」与「超时」（取消上抛，超时返回 null）
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        finally
        {
            RemoveWaiter(waiter);
        }
    }

    #endregion

    #region 清理与释放

    /// <summary>
    /// 清空原始字节缓冲与全部待匹配帧
    /// <para>不影响已注册的等待者（它们继续等待新到达的数据或按超时返回）。</para>
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _receiveBuffer.Clear();
            _pendingFrames.Clear();
        }
    }

    /// <summary>
    /// 释放缓冲：清空全部数据，并让全部等待者立即以 null 完成（等待方按未等到帧处理）
    /// </summary>
    public void Dispose()
    {
        List<FrameWaiter> pendingWaiters;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _receiveBuffer.Clear();
            _pendingFrames.Clear();
            pendingWaiters = _waiters.ToList();
            _waiters.Clear();
        }

        foreach (var waiter in pendingWaiters)
        {
            waiter.Completion.TrySetResult(null);
        }
    }

    #endregion

    #region 私有辅助

    /// <summary>
    /// 锁内按序扫描待匹配帧，移除并返回首个命中的帧
    /// </summary>
    /// <param name="matcher">帧匹配方法</param>
    /// <param name="frame">命中时返回该帧</param>
    /// <returns>存在匹配帧时返回 true</returns>
    private bool TryTakeFrameLocked(Func<byte[], bool> matcher, out byte[]? frame)
    {
        for (int index = 0; index < _pendingFrames.Count; index++)
        {
            var pending = _pendingFrames[index];
            if (!matcher(pending.Frame))
            {
                continue;
            }

            _pendingFrames.RemoveAt(index);
            frame = pending.Frame;
            return true;
        }

        frame = null;
        return false;
    }

    /// <summary>
    /// 锁内移除超过保留时长的待匹配帧（迟到的无关帧不无限堆积）
    /// </summary>
    /// <param name="now">当前时间</param>
    private void RemoveExpiredFramesLocked(DateTime now)
    {
        for (int index = _pendingFrames.Count - 1; index >= 0; index--)
        {
            if (now - _pendingFrames[index].ReceivedTime > _pendingFrameTtl)
            {
                _pendingFrames.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// 从等待者列表移除指定等待者（等待完成或超时后的注销）
    /// </summary>
    /// <param name="waiter">待移除的等待者</param>
    private void RemoveWaiter(FrameWaiter waiter)
    {
        lock (_lock)
        {
            _waiters.Remove(waiter);
        }
    }

    /// <summary>
    /// 在原始字节缓冲中查找指定帧序列的起始索引（帧必来自该缓冲，用于计算已消费字节数）
    /// </summary>
    /// <param name="source">原始字节缓冲</param>
    /// <param name="target">已提取的完整帧</param>
    /// <returns>帧在缓冲中的起始索引；未找到时返回 -1</returns>
    private static int IndexOfSequence(IReadOnlyList<byte> source, IReadOnlyList<byte> target)
    {
        for (int startIndex = 0; startIndex <= source.Count - target.Count; startIndex++)
        {
            bool isMatched = true;
            for (int offset = 0; offset < target.Count; offset++)
            {
                if (source[startIndex + offset] != target[offset])
                {
                    isMatched = false;
                    break;
                }
            }

            if (isMatched)
            {
                return startIndex;
            }
        }

        return -1;
    }

    #endregion
}
