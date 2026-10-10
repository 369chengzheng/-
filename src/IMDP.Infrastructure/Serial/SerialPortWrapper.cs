// =============================================================================
// 文件名：SerialPortWrapper.cs
// 功能描述：串口包装器（内部类）
//           对 System.IO.Ports.SerialPort 进行封装，提供后台读取循环、
//           数据缓冲、超时等待数据等能力，并对外暴露数据接收与异常事件。
// =============================================================================
using System.Buffers;
using System.IO.Ports;

namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口包装器（内部类）
/// <para>封装 <see cref="SerialPort"/>，在后台线程中循环读取数据并触发事件；
/// 同时提供 <see cref="WaitForDataAsync"/> 方法支持超时等待响应。</para>
/// </summary>
internal class SerialPortWrapper : IDisposable
{
    private readonly SerialPort _port;
    private readonly CancellationToken _cancellationToken;
    /// <summary>接收数据缓冲区（线程同步）。</summary>
    private readonly MemoryStream _buffer = new();
    /// <summary>最大缓冲区大小（字节），超过则清空以避免内存膨胀。</summary>
    private const int MaxBufferSize = 65536;
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
    /// </summary>
    private async Task ReadLoopAsync()
    {
        // 从共享数组池租用缓冲区，避免频繁分配
        var buffer = ArrayPool<byte>.Shared.Rent(1024);

        try
        {
            while (!_readCts!.Token.IsCancellationRequested && _port.IsOpen)
            {
                int bytesRead = await _port.BaseStream.ReadAsync(buffer, 0, buffer.Length, _readCts.Token);
                if (bytesRead > 0)
                {
                    ProcessReceivedData(buffer, bytesRead);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 触发异常事件，交由上层处理（如自动重连）
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
