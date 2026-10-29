// =============================================================================
// 文件名：SerialPortWrapper.cs
// 功能描述：串口包装器（内部类）
//           对 System.IO.Ports.SerialPort 进行封装，提供后台接收线程、
//           数据缓冲、超时等待数据等能力，并对外暴露数据接收与异常事件。
//           原始接收字节经 NLog 落盘（logger 名 = 本类全名，与其它日志
//           统一写入 IMDP日期-1.log）。
//
//           接收实现说明（2026-10-26 现场实测后改写）：
//           原先用 _port.BaseStream.ReadAsync(..., cancellationToken) 异步读，
//           在实际 USB 转串口上从第一次调用起就抛出
//           ERROR_OPERATION_ABORTED(995)（“The I/O operation has been aborted…”），
//           重试退避也永远失败（日志中 6 分钟 2592 次重试、成功接收 0 次）。
//           现改为“同步阻塞读 + 专用后台线程”：不注册取消回调、不触发 CancelIoEx，
//           仅在 Close() 时因句柄失效而抛异常退出，行为可预测。
// =============================================================================
using System.IO.Ports;
using NLog;

namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口包装器（内部类）
/// <para>封装 <see cref="SerialPort"/>，在专用后台线程中阻塞读取数据并触发事件；
/// 同时提供 <see cref="WaitForDataAsync"/> 方法支持超时等待响应。</para>
/// </summary>
internal class SerialPortWrapper : IDisposable
{
    /// <summary>NLog 日志器（原始接收字节记录；logger 名落在 IMDP.Infrastructure.Serial.* 前缀，与其它日志统一写入同一文件）</summary>
    private static readonly ILogger _logger = LogManager.GetLogger(typeof(SerialPortWrapper).FullName);

    private readonly SerialPort _port;
    private readonly CancellationToken _cancellationToken;
    /// <summary>接收数据缓冲区（线程同步）。</summary>
    private readonly MemoryStream _buffer = new();
    /// <summary>最大缓冲区大小（字节），超过则清空以避免内存膨胀。</summary>
    private const int MaxBufferSize = 65536;
    /// <summary>单次读取的字节上限。</summary>
    private const int ReadBufferSize = 4096;
    /// <summary>读取失败后的退避重读间隔（毫秒），避免异常风暴占满 CPU。</summary>
    private const int ReadRetryDelayMs = 100;
    /// <summary>连续失败时每这么多次才记一条日志，避免刷屏。</summary>
    private const int FailureLogEvery = 50;
    private Thread? _readThread;
    private CancellationTokenSource? _readCts;
    private bool _disposed;

    /// <summary>获取被包装的串口实例。</summary>
    public SerialPort Port => _port;
    /// <summary>串口是否处于打开状态。</summary>
    public bool IsOpen => _port?.IsOpen ?? false;

    /// <summary>原始数据接收事件。</summary>
    public event EventHandler<SerialDataReceivedEventArgs>? DataReceived;
    /// <summary>异常事件。</summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// 初始化 <see cref="SerialPortWrapper"/> 类的新实例。
    /// </summary>
    /// <param name="port">被包装的串口实例。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public SerialPortWrapper(SerialPort port, CancellationToken cancellationToken)
    {
        _port = port;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// 启动后台接收线程（若已在运行则直接返回）。
    /// </summary>
    public void StartReadLoop()
    {
        // 避免重复启动
        if (_readThread is { IsAlive: true }) return;
        if (_readCts is null || _readCts.IsCancellationRequested)
        {
            _readCts?.Dispose();
            _readCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        }

        _readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = $"SerialRead-{_port.PortName}"
        };
        _readThread.Start();
        _logger.Info($"[{_port.PortName}] 后台接收线程已启动（同步阻塞读）");
    }

    /// <summary>
    /// 停止后台接收线程。
    /// <para>注意：阻塞中的同步读只能靠 <see cref="SerialPort.Close"/> 唤醒，
    /// 因此调用方应先关闭串口再调本方法（<c>SerialService.CloseAsync</c> 已是该顺序）。</para>
    /// </summary>
    public void StopReadLoop()
    {
        _readCts?.Cancel();
        var thread = _readThread;
        if (thread is { IsAlive: true } && !thread.Join(TimeSpan.FromSeconds(1)))
        {
            _logger.Warn($"[{_port.PortName}] 后台接收线程未能在 1 秒内退出（可能仍阻塞在同步读上）");
        }
    }

    /// <summary>
    /// 后台接收循环：以同步阻塞方式读取串口数据。
    /// <para>为什么不使用 <c>BaseStream.ReadAsync</c>：带 CancellationToken 的串口异步读
    /// 在部分 USB 转串口驱动上会立刻返回 ERROR_OPERATION_ABORTED(995)，
    /// 表现为“端口已打开、发送正常，但一个字节都收不到”，且重试无效。</para>
    /// </summary>
    private void ReadLoop()
    {
        var buffer = new byte[ReadBufferSize];
        int consecutiveFailures = 0;

        while (!_readCts!.IsCancellationRequested && _port.IsOpen)
        {
            int bytesRead;
            try
            {
                // ReadTimeout 为 InfiniteTimeout：阻塞到至少读到 1 字节；关闭端口时抛异常退出
                bytesRead = _port.Read(buffer, 0, buffer.Length);
                if (consecutiveFailures > 0)
                {
                    _logger.Info($"[{_port.PortName}] 串口读取已恢复（此前连续失败 {consecutiveFailures} 次）");
                    consecutiveFailures = 0;
                }
            }
            catch (TimeoutException)
            {
                // ReadTimeout 为无限时的防御分支：直接继续下一轮
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                // 端口已关闭/已取消：正常退出（关闭串口时同步读会以异常形式返回）
                if (!_port.IsOpen || _readCts!.IsCancellationRequested)
                {
                    break;
                }

                consecutiveFailures++;
                if (consecutiveFailures == 1 || consecutiveFailures % FailureLogEvery == 0)
                {
                    _logger.Warn($"[{_port.PortName}] 串口读取失败（连续 {consecutiveFailures} 次），" +
                                 $"退避 {ReadRetryDelayMs}ms 后重读：{ex.Message}");
                }

                if (_readCts!.IsCancellationRequested) break;
                try
                {
                    Thread.Sleep(ReadRetryDelayMs);
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }

                continue;
            }

            if (bytesRead > 0)
            {
                try
                {
                    ProcessReceivedData(buffer, bytesRead);
                }
                catch (Exception ex)
                {
                    // 下游订阅者异常绝不能杀死接收线程，否则又会回到“永远收不到数据”
                    _logger.Warn(ex, $"[{_port.PortName}] 处理接收数据时下游抛出异常：{ex.Message}");
                }
            }
        }

        _logger.Info($"[{_port.PortName}] 后台接收线程已退出");
    }

    /// <summary>
    /// 处理接收到的数据：写入缓冲区并触发数据接收事件。
    /// </summary>
    /// <param name="buffer">接收缓冲区。</param>
    /// <param name="bytesRead">本次读取的字节数。</param>
    private void ProcessReceivedData(byte[] buffer, int bytesRead)
    {
        // 加锁保护内存缓冲区，避免并发写入冲突
        lock (_buffer)
        {
            // 缓冲区超限时清空，防止内存膨胀
            if (_buffer.Length + bytesRead > MaxBufferSize)
                _buffer.SetLength(0);
            _buffer.Write(buffer, 0, bytesRead);
            _buffer.Flush();
        }

        // 触发数据接收事件（拷贝本次读取的字节）
        DataReceived?.Invoke(this, new SerialDataReceivedEventArgs
        {
            PortName = _port.PortName,
            Data = buffer.Take(bytesRead).ToArray(),
            Timestamp = DateTime.Now
        });

        // 原始接收字节落盘（串口日志）：每次回调的实际字节数与十六进制内容
        _logger.Info(
            $"[{_port.PortName}] 接收原始数据({bytesRead}字节)：{BitConverter.ToString(buffer, 0, bytesRead).Replace('-', ' ')}");
    }

    /// <summary>
    /// 等待数据到达，直到收到数据、超时或被取消。
    /// </summary>
    /// <param name="timeoutMs">超时时间（毫秒）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>接收到的数据；超时或取消时返回 null。</returns>
    public async Task<byte[]?> WaitForDataAsync(int timeoutMs, CancellationToken token)
    {
        // 创建超时与外部取消的组合令牌
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

        var tcs = new TaskCompletionSource<byte[]?>();
        EventHandler<SerialDataReceivedEventArgs>? handler = null;

        // 订阅数据接收事件，到达即完成 Task
        handler = (s, e) =>
        {
            tcs.TrySetResult(e.Data);
        };

        DataReceived += handler;

        try
        {
            // 等待数据到达或超时取消
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, linkedCts.Token)).ConfigureAwait(false);
            if (completedTask == tcs.Task)
                return await tcs.Task;
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            // 始终取消订阅，避免内存泄漏
            DataReceived -= handler;
        }
    }

    /// <summary>
    /// 释放资源：停止接收线程并清理缓冲区。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopReadLoop();
        _readCts?.Dispose();
        _readCts = null;
        _buffer.Dispose();
    }
}
