// =============================================================================
// 文件名：SerialPortWrapper.cs
// 功能描述：串口包装器（内部类）
//           对 System.IO.Ports.SerialPort 进行封装，提供后台读取循环、
//           数据缓冲、超时等待数据等能力，并对外暴露数据接收与异常事件。
//           原始接收字节经 NLog 落盘（logger 名 = 本类全名，nlog.config 按
//           IMDP.Infrastructure.Serial.* 前缀路由到 serial_日期.txt）。
// =============================================================================
using System.Buffers;
using System.IO.Ports;
using NLog;

namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口包装器（内部类）
/// <para>封装 <see cref="SerialPort"/>，在后台线程中循环读取数据并触发事件；
/// 同时提供 <see cref="WaitForDataAsync"/> 方法支持超时等待响应。</para>
/// </summary>
internal class SerialPortWrapper : IDisposable
{
    /// <summary>NLog 日志器（原始接收字节记录；logger 名落在 IMDP.Infrastructure.Serial.* 前缀，单独进串口日志）</summary>
    private static readonly ILogger _logger = LogManager.GetLogger(typeof(SerialPortWrapper).FullName);

    private readonly SerialPort _port;
    private readonly CancellationToken _cancellationToken;
    /// <summary>接收数据缓冲区（线程同步）。</summary>
    private readonly MemoryStream _buffer = new();
    /// <summary>最大缓冲区大小（字节），超过则清空以避免内存膨胀。</summary>
    private const int MaxBufferSize = 65536;
    /// <summary>读取被中止（IOException）后的退避重读间隔（毫秒），避免异常风暴占满 CPU。</summary>
    private const int ReadRetryDelayMs = 100;
    private Task? _readTask;
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
    /// 启动后台读取循环（若已在运行则直接返回）。
    /// </summary>
    public void StartReadLoop()
    {
        // 避免重复启动
        if (_readTask != null && !_readTask.IsCompleted) return;
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        _readTask = Task.Run(ReadLoopAsync, _readCts.Token);
    }

    /// <summary>
    /// 停止后台读取循环。
    /// </summary>
    public void StopReadLoop()
    {
        _readCts?.Cancel();
        _readTask?.Wait(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 后台读取循环：持续从串口基流读取数据并处理。
    /// <para>异常在循环内逐次捕获与恢复，保证接收循环永不因单次 I/O 异常静默死亡：
    /// 在途读被中止（如 USB 串口驱动返回 ERROR_OPERATION_ABORTED）≠ 端口损坏，
    /// 退避后重新发起读取即可；串口已关闭或取消时正常退出。</para>
    /// </summary>
    private async Task ReadLoopAsync()
    {
        // 从共享数组池租用缓冲区，避免频繁分配
        var buffer = ArrayPool<byte>.Shared.Rent(1024);

        try
        {
            while (!_readCts!.Token.IsCancellationRequested && _port.IsOpen)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _port.BaseStream.ReadAsync(buffer, 0, buffer.Length, _readCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 正常停止：关闭串口/停止读循环时取消令牌，退出循环
                    break;
                }
                catch (TimeoutException)
                {
                    // 读超时（当前配置为无限超时，防御性保留）：直接继续下一轮读取
                    continue;
                }
                catch (IOException ex)
                {
                    // 在途读被中止（ERROR_OPERATION_ABORTED 等）：串口仍打开时退避后重读。
                    // 若无此恢复逻辑，一次该异常就会永久终止接收循环，表现为
                    // "发送正常但永远收不到数据"（历史上正是该缺陷导致工装应答全部丢失）。
                    if (!_port.IsOpen)
                    {
                        break;
                    }

                    _logger.Warn(ex, $"[{_port.PortName}] 串口读取被中止，退避 {ReadRetryDelayMs}ms 后重读：{ex.Message}");
                    try
                    {
                        await Task.Delay(ReadRetryDelayMs, _readCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // 退避期间被停止（关闭串口/停止读循环）：安静退出
                        break;
                    }

                    continue;
                }

                if (bytesRead > 0)
                {
                    ProcessReceivedData(buffer, bytesRead);
                }
            }
        }
        catch (Exception ex)
        {
            // 循环整体异常（如关闭过程中的句柄失效）：记录后触发异常事件，交由上层处理（如自动重连）
            _logger.Warn(ex, $"[{_port.PortName}] 串口后台读取循环异常退出：{ex.Message}");
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            // 归还租用的缓冲区
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
    /// 释放资源：停止读取循环并清理缓冲区。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readCts?.Cancel();
        _readTask?.Wait(TimeSpan.FromSeconds(1));
        _buffer.Dispose();
    }
}
