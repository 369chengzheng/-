// =============================================================================
// 文件名：SerialService.cs
// 功能描述：串口服务实现
//           实现 ISerialService 接口，提供多串口并发管理、命令队列与重试、
//           自动重连、USB 端口热插拔监视等完整能力，并通过事件对外通知状态变更。
//           收发字节经 NLog 落盘（logger 名 = 本类全名，nlog.config 按
//           IMDP.Infrastructure.Serial.* 前缀路由到 serial_日期.txt）。
// =============================================================================
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Management;
using System.Runtime.Versioning;
using IMDP.Domain.ValueObjects;
using NLog;

namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口服务实现
/// <para>实现 <see cref="ISerialService"/>，内部维护多个串口包装器、命令队列与重连配置；
/// 通过后台消费者任务统一调度接收数据事件，避免回调线程阻塞。</para>
/// </summary>
public class SerialService : ISerialService
{
    /// <summary>NLog 日志器（串口收发与状态记录；logger 名落在 IMDP.Infrastructure.Serial.* 前缀，单独进串口日志）</summary>
    private static readonly ILogger _logger = LogManager.GetLogger(typeof(SerialService).FullName);

    /// <summary>已打开的串口包装器字典（按串口名索引）。</summary>
    private readonly ConcurrentDictionary<string, SerialPortWrapper> _ports = new();
    /// <summary>每个串口的自动重连配置字典。</summary>
    private readonly ConcurrentDictionary<string, AutoReconnectConfig> _reconnectConfigs = new();
    /// <summary>每个串口的命令请求队列字典（保证同串口命令串行执行）。</summary>
    private readonly ConcurrentDictionary<string, ConcurrentQueue<CommandRequest>> _commandQueues = new();
    /// <summary>每个串口的命令队列处理任务字典。</summary>
    private readonly ConcurrentDictionary<string, Task> _queueProcessTasks = new();
    /// <summary>接收数据生产-消费队列（统一调度 RawDataReceived 事件）。</summary>
    private readonly BlockingCollection<SerialDataReceivedEventArgs> _dataQueue = new();
    /// <summary>释放令牌源（控制后台任务退出）。</summary>
    private readonly CancellationTokenSource _disposeCts = new();
    /// <summary>接收数据消费者任务。</summary>
    private readonly Task _consumerTask;
    /// <summary>USB 设备变更监视器。</summary>
    private ManagementEventWatcher? _portWatcher;
    private bool _disposed;

    /// <summary>串口状态变更事件。</summary>
    public event EventHandler<SerialPortStatusChangedEventArgs>? PortStatusChanged;
    /// <summary>串口列表变更事件（设备插拔时触发）。</summary>
    public event EventHandler<string[]>? PortListChanged;
    /// <summary>原始数据接收事件。</summary>
    public event EventHandler<SerialDataReceivedEventArgs>? RawDataReceived;
    /// <summary>通信日志事件（发送/接收记录）。</summary>
    public event EventHandler<CommunicationLogEventArgs>? CommunicationLog;

    /// <summary>
    /// 初始化 <see cref="SerialService"/> 类的新实例。
    /// <para>启动后台消费者任务处理接收数据。</para>
    /// </summary>
    public SerialService()
    {
        _consumerTask = Task.Run(ConsumeDataAsync);
    }

    /// <summary>
    /// 异步打开指定串口。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="config">串口配置参数。</param>
    /// <returns>打开成功返回 true；若已打开则直接返回当前状态；失败返回 false。</returns>
    public async Task<bool> OpenAsync(string portName, SerialPortConfig config)
    {
        // 已存在则直接返回当前打开状态
        if (_ports.ContainsKey(portName))
            return _ports[portName].IsOpen;

        // 字符串停止位转换为枚举
        var stopBits = config.StopBits switch
        {
            "None" => System.IO.Ports.StopBits.None,
            "One" => System.IO.Ports.StopBits.One,
            "OnePointFive" => System.IO.Ports.StopBits.OnePointFive,
            "Two" => System.IO.Ports.StopBits.Two,
            _ => System.IO.Ports.StopBits.One
        };

        // 字符串校验位转换为枚举
        var parity = config.Parity switch
        {
            "None" => Parity.None,
            "Odd" => Parity.Odd,
            "Even" => Parity.Even,
            "Mark" => Parity.Mark,
            "Space" => Parity.Space,
            _ => Parity.None
        };

        var port = new SerialPort(portName, config.BaudRate, parity, config.DataBits, stopBits)
        {
            // 读超时必须为无限：本串口的唯一读方是包装器的后台异步读循环（ReadAsync
            // 内部会把超时临时改为无限再恢复，该 SetCommTimeouts 往返在部分 USB 串口
            // 驱动上会中止在途读并抛 ERROR_OPERATION_ABORTED，杀死接收循环）；
            // 应用层等待一律走 DataReceived 事件，不受此超时影响。写超时保留有限值。
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = 500,
            DtrEnable = false,
            RtsEnable = false
        };

        try
        {
            await Task.Run(() => port.Open());

            // 创建包装器并订阅事件
            var wrapper = new SerialPortWrapper(port, _disposeCts.Token);
            wrapper.DataReceived += OnPortDataReceived;
            wrapper.ErrorOccurred += OnPortErrorOccurred;

            _ports[portName] = wrapper;
            _commandQueues[portName] = new ConcurrentQueue<CommandRequest>();
            wrapper.StartReadLoop();

            _logger.Info($"[{portName}] 串口已打开：波特率={config.BaudRate}，数据位={config.DataBits}，停止位={stopBits}，校验位={parity}");
            OnPortStatusChanged(portName, SerialPortStatus.Opened, "串口已打开");
            return true;
        }
        catch (Exception ex)
        {
            port.Dispose();
            OnPortStatusChanged(portName, SerialPortStatus.Closed, $"打开失败: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// 异步关闭指定串口。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    public async Task CloseAsync(string portName)
    {
        if (!_ports.TryGetValue(portName, out var wrapper))
            return;

        try
        {
            var port = wrapper.Port;
            if (port.IsOpen)
            {
                // 关闭前复位 DTR/RTS 信号
                port.DtrEnable = false;
                port.RtsEnable = false;
                await Task.Run(() => port.Close());
            }

            wrapper.StopReadLoop();
            wrapper.DataReceived -= OnPortDataReceived;
            wrapper.ErrorOccurred -= OnPortErrorOccurred;
            wrapper.Dispose();

            // 清理各字典中关联项
            _ports.TryRemove(portName, out _);
            _reconnectConfigs.TryRemove(portName, out _);
            _commandQueues.TryRemove(portName, out _);

            _logger.Info($"[{portName}] 串口已关闭");
            OnPortStatusChanged(portName, SerialPortStatus.Closed, "串口已关闭");
        }
        catch (Exception ex)
        {
            OnPortStatusChanged(portName, SerialPortStatus.Closed, $"关闭异常: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 发送帧并等待响应（支持重试）。
    /// <para>命令入队后由该串口的队列处理器串行执行；调用方等待完成或整体超时。</para>
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="frame">待发送的帧数据。</param>
    /// <param name="timeoutMs">单次等待响应超时（毫秒）。</param>
    /// <param name="retryCount">重试次数，默认 1。</param>
    /// <param name="sendDelayMs">发送前延迟（毫秒），默认 0。</param>
    /// <returns>接收到的响应数据；超时或失败返回 null。</returns>
    public Task<byte[]?> SendAndWaitAsync(string portName, byte[] frame, int timeoutMs, int retryCount = 1, int sendDelayMs = 0)
        => SendAndWaitAsync(portName, frame, null, timeoutMs, retryCount, sendDelayMs);

    /// <summary>
    /// 发送帧并等待响应（带帧格式：PreambleGapMs>0 时前导与帧体分段发送）。
    /// </summary>
    public async Task<byte[]?> SendAndWaitAsync(string portName, byte[] frame, IMDP.Domain.Products.FrameFormatConfig? format, int timeoutMs, int retryCount = 1, int sendDelayMs = 0)
    {
        if (!_ports.TryGetValue(portName, out var wrapper) || !wrapper.IsOpen)
            return null;

        // 构造命令请求并入队
        var request = new CommandRequest
        {
            Frame = frame,
            TimeoutMs = timeoutMs,
            RetryCount = retryCount,
            SendDelayMs = sendDelayMs,
            Format = format,
            CompletionSource = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        _commandQueues[portName].Enqueue(request);
        StartQueueProcessor(portName);

        // 整体超时 = 单次超时 × (重试次数+1) + 1 秒冗余
        using var timeoutCts = new CancellationTokenSource(timeoutMs * (retryCount + 1) + 1000);
        // 使用 await 替代 Task.Result，避免同步阻塞（即便任务已完成也走异步路径，规避潜在 SyncContext 死锁）
        try
        {
            return await request.CompletionSource.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 命令队列处理循环：串行消费某串口的命令队列。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    private async Task ProcessCommandQueueAsync(string portName)
    {
        if (!_ports.TryGetValue(portName, out var wrapper)) return;

        while (!_disposeCts.Token.IsCancellationRequested && _commandQueues.TryGetValue(portName, out var queue))
        {
            // 队列为空时短暂等待，避免空转
            if (!queue.TryDequeue(out var request))
            {
                await Task.Delay(10);
                continue;
            }

            // 执行命令（含重试），并通过 TCS 返回结果
            var response = await ExecuteCommandWithRetryAsync(portName, wrapper, request);
            request.CompletionSource.TrySetResult(response);
        }
    }

    /// <summary>
    /// 计算帧格式的前导总长（Preamble×Repeat + PreambleSuffix）；无前导返回 0。
    /// </summary>
    private static int GetPreambleLength(IMDP.Domain.Products.FrameFormatConfig? format)
    {
        if (format?.Preamble == null || format.PreambleRepeat <= 0) return 0;
        return format.Preamble.Length * format.PreambleRepeat + (format.PreambleSuffix?.Length ?? 0);
    }

    /// <summary>
    /// 按帧格式写出数据：前导与帧体之间有 PreambleGapMs 间隔时分两段写（唤醒类设备需要）。
    /// </summary>
    private static async Task WriteFrameWithPreambleGapAsync(
        System.IO.Ports.SerialPort port, byte[] frame,
        IMDP.Domain.Products.FrameFormatConfig? format,
        CancellationToken token)
    {
        int preambleLen = GetPreambleLength(format);
        if (preambleLen <= 0 || (format?.PreambleGapMs ?? 0) <= 0 || preambleLen >= frame.Length)
        {
            await port.BaseStream.WriteAsync(frame, 0, frame.Length, token).ConfigureAwait(false);
            return;
        }

        // 前导段 → 间隔 → 帧体段
        await port.BaseStream.WriteAsync(frame, 0, preambleLen, token).ConfigureAwait(false);
        await port.BaseStream.FlushAsync(token).ConfigureAwait(false);
        await Task.Delay(format!.PreambleGapMs, token).ConfigureAwait(false);
        await port.BaseStream.WriteAsync(frame, preambleLen, frame.Length - preambleLen, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行单条命令（含重试逻辑）。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="wrapper">串口包装器。</param>
    /// <param name="request">命令请求。</param>
    /// <returns>接收到的响应数据；全部重试失败返回 null。</returns>
    private async Task<byte[]?> ExecuteCommandWithRetryAsync(string portName, SerialPortWrapper wrapper, CommandRequest request)
    {
        // attempt 从 0 开始，循环 RetryCount+1 次
        for (int attempt = 0; attempt <= request.RetryCount; attempt++)
        {
            try
            {
                // 发送前延迟
                if (request.SendDelayMs > 0)
                    await Task.Delay(request.SendDelayMs);

                LogCommunication(portName, CommunicationDirection.Send, request.Frame, $"发送(第{attempt + 1}次)");
                await WriteFrameWithPreambleGapAsync(wrapper.Port, request.Frame, request.Format, _disposeCts.Token);

                // 等待响应数据
                var response = await wrapper.WaitForDataAsync(request.TimeoutMs, _disposeCts.Token);
                if (response != null)
                {
                    LogCommunication(portName, CommunicationDirection.Receive, response, "接收原始数据");
                    return response;
                }
            }
            catch (OperationCanceledException)
            {
                // 取消时直接终止重试
                break;
            }
            catch (Exception ex)
            {
                OnPortStatusChanged(portName, SerialPortStatus.Disconnected, $"发送异常: {ex.Message}", ex);
                // 若启用自动重连则触发后台重连
                if (_reconnectConfigs.TryGetValue(portName, out var reconnectConfig) && reconnectConfig.Enabled)
                {
                    _ = TriggerAutoReconnectAsync(portName);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 启动指定串口的命令队列处理器（若已运行则不重复启动）。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    private void StartQueueProcessor(string portName)
    {
        if (_queueProcessTasks.TryGetValue(portName, out var task) && !task.IsCompleted)
            return;
        _queueProcessTasks[portName] = Task.Run(() => ProcessCommandQueueAsync(portName));
    }

    /// <summary>
    /// 异步发送数据（不等待响应）。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="data">待发送的数据。</param>
    public async Task SendAsync(string portName, byte[] data)
    {
        if (!_ports.TryGetValue(portName, out var wrapper) || !wrapper.IsOpen)
            return;
        LogCommunication(portName, CommunicationDirection.Send, data, "发送(无响应)");
        await wrapper.Port.BaseStream.WriteAsync(data, 0, data.Length, _disposeCts.Token);
    }

    /// <summary>
    /// 触发通信日志事件，并将收发字节同步写入 NLog 串口日志文件
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="direction">通信方向。</param>
    /// <param name="data">通信数据。</param>
    /// <param name="description">描述信息。</param>
    private void LogCommunication(string portName, CommunicationDirection direction, byte[] data, string description)
    {
        // 串口日志落盘：方向 + 描述 + 字节数 + 十六进制内容（与 CommunicationLog 事件同一口径）
        string directionText = direction == CommunicationDirection.Send ? "发送" : "接收";
        _logger.Info(
            $"[{portName}] {directionText}({description}) {data.Length}字节：{BitConverter.ToString(data).Replace('-', ' ')}");

        CommunicationLog?.Invoke(this, new CommunicationLogEventArgs
        {
            PortName = portName,
            Direction = direction,
            Data = data,
            Timestamp = DateTime.Now,
            Description = description
        });
    }

    /// <summary>
    /// 串口数据接收回调：将事件入队交由消费者处理。
    /// </summary>
    private void OnPortDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        if (!_disposeCts.IsCancellationRequested)
            _dataQueue.Add(e);
    }

    /// <summary>
    /// 接收数据消费者任务：从队列取出事件并触发 RawDataReceived。
    /// </summary>
    private async Task ConsumeDataAsync()
    {
        foreach (var e in _dataQueue.GetConsumingEnumerable(_disposeCts.Token))
        {
            RawDataReceived?.Invoke(this, e);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// 串口异常回调：触发断开事件并在启用时启动自动重连。
    /// </summary>
    private void OnPortErrorOccurred(object? sender, Exception ex)
    {
        var wrapper = sender as SerialPortWrapper;
        if (wrapper == null) return;

        OnPortStatusChanged(wrapper.Port.PortName, SerialPortStatus.Disconnected, $"异常断开: {ex.Message}", ex);
        // 启用自动重连时后台触发
        if (_reconnectConfigs.TryGetValue(wrapper.Port.PortName, out var config) && config.Enabled)
        {
            _ = TriggerAutoReconnectAsync(wrapper.Port.PortName);
        }
    }

    /// <summary>
    /// 触发自动重连流程：按配置的次数与间隔尝试重新打开串口。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    private async Task TriggerAutoReconnectAsync(string portName)
    {
        if (!_ports.TryGetValue(portName, out var wrapper))
            return;

        var config = _reconnectConfigs.GetOrAdd(portName, _ => new AutoReconnectConfig());
        if (!config.Enabled) return;

        OnPortStatusChanged(portName, SerialPortStatus.Reconnecting, "开始重连...");

        // 按最大重试次数循环尝试
        for (int i = 1; i <= config.MaxRetryCount; i++)
        {
            try
            {
                await Task.Delay(config.RetryIntervalMs, _disposeCts.Token);
                if (!wrapper.Port.IsOpen)
                {
                    await Task.Run(() => wrapper.Port.Open());
                    wrapper.StartReadLoop();
                    OnPortStatusChanged(portName, SerialPortStatus.Reconnected, "重连成功");
                    return;
                }
            }
            catch (Exception ex)
            {
                OnPortStatusChanged(portName, SerialPortStatus.ReconnectFailed, $"重连失败({i}/{config.MaxRetryCount}): {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 启动端口监视（监听 Windows 设备变更事件，触发端口列表更新）。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void StartPortWatcher()
    {
        if (_portWatcher != null) return;
        // 监听 Win32 设备变更事件
        var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
        _portWatcher = new ManagementEventWatcher(query);
        _portWatcher.EventArrived += (s, e) => PortListChanged?.Invoke(this, GetAvailablePorts());
        _portWatcher.Start();
    }

    /// <summary>
    /// 停止端口监视。
    /// </summary>
    public void StopPortWatcher()
    {
        if (_portWatcher != null)
        {
            _portWatcher.Stop();
            _portWatcher.Dispose();
            _portWatcher = null;
        }
    }

    /// <summary>
    /// 判断指定串口是否已打开。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <returns>已打开返回 true；否则返回 false。</returns>
    public bool IsOpen(string portName) => _ports.TryGetValue(portName, out var w) && w.IsOpen;

    /// <summary>
    /// 获取本机可用的全部串口名称。
    /// </summary>
    /// <returns>串口名称数组。</returns>
    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    /// <summary>
    /// 为指定串口启用自动重连。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="maxRetryCount">最大重试次数，默认 5。</param>
    /// <param name="retryIntervalMs">重试间隔（毫秒），默认 2000。</param>
    public void EnableAutoReconnect(string portName, int maxRetryCount = 5, int retryIntervalMs = 2000)
    {
        var config = _reconnectConfigs.GetOrAdd(portName, _ => new AutoReconnectConfig());
        config.Enabled = true;
        config.MaxRetryCount = maxRetryCount;
        config.RetryIntervalMs = retryIntervalMs;
    }

    /// <summary>
    /// 为指定串口禁用自动重连。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    public void DisableAutoReconnect(string portName)
    {
        if (_reconnectConfigs.TryGetValue(portName, out var config))
            config.Enabled = false;
    }

    /// <summary>
    /// 手动触发指定串口的重连。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <returns>重连成功返回 true；否则返回 false。</returns>
    public async Task<bool> ReconnectAsync(string portName)
    {
        if (!_ports.TryGetValue(portName, out var wrapper))
            return false;

        try
        {
            if (!wrapper.Port.IsOpen)
            {
                await Task.Run(() => wrapper.Port.Open());
                wrapper.StartReadLoop();
                OnPortStatusChanged(portName, SerialPortStatus.Reconnected, "手动重连成功");
                return true;
            }
        }
        catch (Exception ex)
        {
            OnPortStatusChanged(portName, SerialPortStatus.ReconnectFailed, $"手动重连失败: {ex.Message}", ex);
        }
        return false;
    }

    /// <summary>
    /// 异步关闭全部已打开的串口。
    /// </summary>
    public async Task CloseAllAsync()
    {
        // 复制 Keys 避免遍历过程中集合变更
        foreach (var portName in _ports.Keys.ToList())
            await CloseAsync(portName);
    }

    /// <summary>
    /// 触发串口状态变更事件。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="status">新状态。</param>
    /// <param name="message">描述信息。</param>
    /// <param name="ex">关联异常（可选）。</param>
    private void OnPortStatusChanged(string portName, SerialPortStatus status, string message, Exception? ex = null)
    {
        PortStatusChanged?.Invoke(this, new SerialPortStatusChangedEventArgs
        {
            PortName = portName,
            Status = status,
            Message = message,
            Exception = ex
        });
    }

    /// <summary>
    /// 释放资源：取消后台任务、停止监视、关闭全部串口。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _disposeCts.Cancel();
        _dataQueue.CompleteAdding();
        StopPortWatcher();

        // 关闭并释放所有串口包装器
        foreach (var wrapper in _ports.Values)
            wrapper.Dispose();
        _ports.Clear();

        // 等待消费者任务退出
        _consumerTask?.Wait(TimeSpan.FromSeconds(2));
        _disposeCts.Dispose();
        _dataQueue.Dispose();
    }

    /// <summary>
    /// 命令请求内部类（用于队列化发送并等待响应）。
    /// </summary>
    private class CommandRequest
    {
        /// <summary>待发送的帧数据。</summary>
        public byte[] Frame { get; set; } = Array.Empty<byte>();
        /// <summary>单次等待响应超时（毫秒）。</summary>
        public int TimeoutMs { get; set; }
        /// <summary>重试次数。</summary>
        public int RetryCount { get; set; }
        /// <summary>发送前延迟（毫秒）。</summary>
        public int SendDelayMs { get; set; }
        /// <summary>用于异步返回响应的 TaskCompletionSource。</summary>
        public TaskCompletionSource<byte[]?> CompletionSource { get; set; } = null!;
        /// <summary>可选帧格式：提供时按 PreambleGapMs 分段发送。</summary>
        public IMDP.Domain.Products.FrameFormatConfig? Format { get; set; }
    }

    /// <summary>
    /// 自动重连配置内部类。
    /// </summary>
    private class AutoReconnectConfig
    {
        /// <summary>是否启用自动重连。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>最大重试次数。</summary>
        public int MaxRetryCount { get; set; } = 5;
        /// <summary>重试间隔（毫秒）。</summary>
        public int RetryIntervalMs { get; set; } = 2000;
    }
}
