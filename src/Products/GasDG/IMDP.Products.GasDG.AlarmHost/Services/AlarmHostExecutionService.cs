using System.IO;
using System.Collections.Concurrent;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Protocol;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.AlarmHost.Models;
using NLog;

namespace IMDP.Products.GasDG.AlarmHost.Services;

/// <summary>
/// 报警主机单工位方案执行，复用公共串口、接收匹配缓冲及燃气对象协议
/// 同串口整轮串行、不同串口并行；页面连接显式开关，独立执行可使用临时会话
/// </summary>
public sealed class AlarmHostExecutionService(ISerialService serial)
{
    /// <summary>
    /// 本页面明确打开的串口，只关闭这些连接，不接管其他模块的端口
    /// </summary>
    private readonly ConcurrentDictionary<string, OwnedPort> _ownedPorts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 同端口整轮执行互斥，不让两个工位的对象读写交错
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _portGates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 打开和关闭连接串行处理，避免关闭途中又建立新连接
    /// </summary>
    private readonly SemaphoreSlim _connectionGate = new(1, 1);

    /// <summary>
    /// 连接生命周期锁内维护唯一状态订阅，空连接重复关闭不再重复退订
    /// </summary>
    private bool _statusSubscribed;

    /// <summary>
    /// 本页拥有端口的状态事件，沿用公共串口事件类型
    /// </summary>
    public event EventHandler<SerialPortStatusChangedEventArgs>? PortStatusChanged;

    /// <summary>
    /// 本页是否仍有已打开连接，断线后立即反映实际状态
    /// </summary>
    public bool IsConnectionOpen => _ownedPorts.Any(port => !port.Value.Token.IsCancellationRequested && serial.IsOpen(port.Key));

    /// <summary>
    /// 指定端口必须由本页拥有且实际打开，才允许页面扫码执行
    /// </summary>
    public bool IsPortOpen(string portName) => _ownedPorts.TryGetValue(portName, out var connection) &&
        !connection.Token.IsCancellationRequested && serial.IsOpen(portName);

    /// <summary>
    /// 按独立端口打开连接；失败或取消时回收本次已成功打开的连接
    /// 串口参数复用平台值对象，不由波特率推断校验位
    /// </summary>
    public async Task OpenPortsAsync(IReadOnlyDictionary<string, SerialPortConfig> ports, CancellationToken cancellationToken = default)
    {
        if (ports.Count == 0) throw new ArgumentException("请先选择工位串口。", nameof(ports));
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (!_ownedPorts.IsEmpty)
            {
                if (IsConnectionOpen) throw new InvalidOperationException("请先关闭本页已有串口。");
                await CloseOwnedPortsAsync();
            }
            if (!_statusSubscribed)
            {
                serial.PortStatusChanged += ConnectionStatusChanged;
                _statusSubscribed = true;
            }
            try
            {
                foreach (var (portName, source) in ports)
                {
                    var gate = _portGates.GetOrAdd(portName, _ => new(1, 1));
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        if (serial.IsOpen(portName)) throw new InvalidOperationException($"{portName} 已被其他连接打开。");
                        var config = new SerialPortConfig
                        {
                            BaudRate = source.BaudRate,
                            DataBits = source.DataBits,
                            Parity = source.Parity,
                            StopBits = source.StopBits
                        };
                        if (!await serial.OpenAsync(portName, config)) throw new IOException($"打开 {portName} 失败。");
                        _ownedPorts[portName] = new OwnedPort(config);
                        PortStatusChanged?.Invoke(this, new() { PortName = portName, Status = SerialPortStatus.Opened, Message = "串口已打开" });
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    finally { gate.Release(); }
                }
            }
            catch
            {
                await CloseOwnedPortsAsync();
                throw;
            }
        }
        finally { _connectionGate.Release(); }
    }

    /// <summary>
    /// 先取消等待和正在执行的工位，再关闭本页面拥有的端口
    /// 不使用公共 CloseAllAsync，避免影响其他产品页面
    /// </summary>
    public async Task ClosePortsAsync()
    {
        await _connectionGate.WaitAsync();
        try { await CloseOwnedPortsAsync(); }
        finally { _connectionGate.Release(); }
    }

    /// <summary>
    /// 连接生命周期锁内回收拥有的端口，等待对应整轮执行退出后再关闭
    /// </summary>
    private async Task CloseOwnedPortsAsync()
    {
        var ports = _ownedPorts.ToArray();
        foreach (var (_, connection) in ports) connection.Closed.Cancel();
        Exception? closeError = null;
        try
        {
            foreach (var (portName, connection) in ports)
            {
                var gate = _portGates.GetOrAdd(portName, _ => new(1, 1));
                await gate.WaitAsync();
                try
                {
                    await serial.CloseAsync(portName);
                    if (serial.IsOpen(portName)) throw new IOException($"{portName} 关闭后仍处于打开状态。");
                }
                catch (Exception error)
                {
                    Logger.Error(error, "报警主机关闭串口失败：{0}", portName);
                    closeError ??= error;
                }
                finally
                {
                    if (!serial.IsOpen(portName))
                    {
                        _ownedPorts.TryRemove(portName, out _);
                        connection.Closed.Dispose();
                    }
                    gate.Release();
                }
            }
        }
        finally
        {
            if (_ownedPorts.IsEmpty && _statusSubscribed)
            {
                serial.PortStatusChanged -= ConnectionStatusChanged;
                _statusSubscribed = false;
            }
        }
        if (closeError != null) throw new IOException("关闭报警主机串口失败。", closeError);
    }

    /// <summary>
    /// 断线同时中止本端口在途和排队工位；其他端口继续运行
    /// </summary>
    private void ConnectionStatusChanged(object? sender, SerialPortStatusChangedEventArgs args)
    {
        if (!_ownedPorts.TryGetValue(args.PortName, out var connection)) return;
        if (args.Status is SerialPortStatus.Disconnected or SerialPortStatus.Closed)
        {
            if (!connection.Closed.IsCancellationRequested)
            {
                connection.Error = new IOException($"{args.PortName} 串口已断开：{args.Message}", args.Exception);
                try { connection.Closed.Cancel(); }
                catch (ObjectDisposedException) { }
            }
        }
        PortStatusChanged?.Invoke(this, args);
    }

    /// <summary>
    /// 本页连接的实际参数及关闭通知；只在连接结束且工位退出后释放取消源
    /// </summary>
    private sealed class OwnedPort
    {
        public OwnedPort(SerialPortConfig config)
        {
            Config = config;
            Token = Closed.Token;
        }

        /// <summary>
        /// 打开时使用的串口配置快照
        /// </summary>
        public SerialPortConfig Config { get; }

        /// <summary>
        /// 连接关闭时统一取消该端口排队和运行操作
        /// </summary>
        public CancellationTokenSource Closed { get; } = new();

        /// <summary>
        /// 提前保留令牌，关闭后迟到的排队调用不再访问已释放的取消源属性
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// 断线原因与主动关闭区别，供工位显示通信中止
        /// </summary>
        public IOException? Error { get; set; }
    }
    /// <summary>
    /// 生产读取的唤醒前导字节数，按用户确认发送一千二百个 FE
    /// </summary>
    private const int PreambleLength = 1200;

    /// <summary>
    /// 独立执行默认无校验；页面显式连接使用平台串口设置
    /// </summary>
    private const string SerialParity = "None";

    /// <summary>
    /// 记录本次端口参数、收发字节和失败原因，沿用项目的 NLog 文件配置
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 既有报警主机使用零地址及内容区加密；不将该设备约定写进公共串口或公共协议
    /// </summary>
    private static readonly GasDataObjectProtocol Protocol = new(new byte[6],
        [0x57, 0x58, 0x5A, 0x4E, 0x43, 0x53, 0x42, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09]);

    /// <summary>
    /// 只枚举端口，不打开连接
    /// </summary>
    public string[] GetAvailablePorts() => serial.GetAvailablePorts();

    /// <summary>
    /// 设置模式写入可写项并回读，检验模式只读取；只读项在两种模式都读取
    /// 工厂态优先切换，出厂态在前序项目通过后最后写入，不以界面排序代替执行规则
    /// </summary>
    public async Task ExecuteAsync(string portName, int baudRate, int planType, string scanCode,
        IReadOnlyList<AlarmHostResultRow> items, Action<AlarmHostItemResult> report, CancellationToken cancellationToken = default,
        IReadOnlyList<ProtocolCommand>? commands = null)
    {
        if (planType is not (0 or 1)) throw new ArgumentException("方案类型只能为设置或检验。", nameof(planType));
        // 先验证所有目标值，避免后半段参数无法编码时，前半段已经写入设备。
        var targets = items.Where(item => item.CanExecute && AlarmHostFunctionCatalog.Get(item.Code).CanWrite)
            .ToDictionary(item => item.Code, item => AlarmHostExecutionCodec.Encode(item, scanCode, DateTime.Now));
        // 公共命令类型沿用平台：1写参数、2读参数、3读版本，不用对象编号替代公共命令码。
        ProtocolCommand Command(int type) => commands?.FirstOrDefault(command => command.CommandType == type)
            ?? new ProtocolCommand { CommandType = type };
        foreach (var type in new[] { 1, 2, 3 })
        {
            var command = Command(type);
            if (command.TimeoutMs <= 0 || command.RetryCount < 0 || command.SendDelayMs < 0)
                throw new ArgumentException("协议命令的超时必须为正数，重发次数和延时不能为负数。", nameof(commands));
        }
        var ordered = items.Where(item => item.CanExecute)
            .OrderBy(item => Command(CommandType(item, planType)).Order).ThenBy(item => item.Order).ToList();
        var production = ordered.FirstOrDefault(item => item.Code == AlarmHostFunctionCatalog.ProductionStatus);
        if (planType == 0 && production != null)
        {
            ordered.Remove(production);
            if (targets[production.Code][0] == 0) ordered.Insert(0, production);
            else ordered.Add(production);
        }
        if (ordered.Count == 0) return;
        await InSessionAsync(portName, baudRate, async (buffer, config, sessionToken) =>
        {
            var passed = true;
            foreach (var item in ordered)
            {
                sessionToken.ThrowIfCancellationRequested();
                if (planType == 0 && item == production && targets[item.Code][0] == 1 && !passed)
                {
                    report(new(item.Code, "未执行", "", false, "前序项目未通过，未切换出厂态"));
                    continue;
                }
                var definition = AlarmHostFunctionCatalog.Get(item.Code);
                var id = definition.ObjectId!.Value;
                try
                {
                    byte? writeResult = null;
                    if (planType == 0 && definition.CanWrite)
                    {
                        // 每次真正发送前取时，排队、前序步骤、发送延时和重发均不使用旧表时。
                        byte[] BuildWrite()
                        {
                            if (item.Code == AlarmHostFunctionCatalog.MeterTime)
                                targets[item.Code] = AlarmHostExecutionCodec.Encode(item, scanCode, DateTime.Now);
                            return Protocol.BuildWriteFrame(id, targets[item.Code]);
                        }
                        var response = await ExchangeAsync(portName, config, buffer, BuildWrite,
                            frame => Protocol.MatchesWriteResponse(frame, id), Command(1), sessionToken);
                        writeResult = Protocol.ReadWriteResponse(response, id);
                        if (writeResult is > 0)
                            Logger.Warn("报警主机{0}项目{1}写结果码0x{2:X2}，继续回读最终值", portName, item.Code, writeResult.Value);
                    }
                    var read = await ExchangeAsync(portName, config, buffer, () => Protocol.BuildReadFrame(id),
                        frame => Protocol.MatchesReadResponse(frame, id), Command(item.Code == AlarmHostFunctionCatalog.Version ? 3 : 2), sessionToken);
                    var value = Protocol.ReadObjectResponse(read, id);
                    targets.TryGetValue(item.Code, out var expected);
                    var result = AlarmHostExecutionCodec.Evaluate(item, value, scanCode, DateTime.Now, expected);
                    if (writeResult is > 0) result = result with { Message = $"写应答结果码：0x{writeResult.Value:X2}, {result.Message}" };
                    report(result);
                    passed &= result.IsPass;
                    if (planType == 0 && item == production && !result.IsPass && targets[item.Code][0] == 0)
                        break;
                }
                catch (OperationCanceledException) { throw; }
                catch (IOException) { throw; }
                catch (Exception error)
                {
                    Logger.Error(error, "报警主机{0}项目{1}执行失败", portName, item.Code);
                    report(new(item.Code, "失败", "", false, error.Message));
                    passed = false;
                    if (planType == 0 && item == production && targets[item.Code][0] == 0) break;
                }
            }
            return passed;
        }, cancellationToken);
    }

    /// <summary>
    /// 项目按当前模式的主要命令类别排序；写后回读始终紧随其写入，不拆成独立工位步骤
    /// </summary>
    private static int CommandType(AlarmHostResultRow item, int planType) =>
        planType == 0 && AlarmHostFunctionCatalog.Get(item.Code).CanWrite ? 1 :
        item.Code == AlarmHostFunctionCatalog.Version ? 3 : 2;

    /// <summary>
    /// 一个工位共用一条会话，先订阅再发送；每次交换复用同一套前导、日志和超时规则
    /// 接收缓存提前建立，发送完成前到达的完整应答仍可匹配
    /// </summary>
    private async Task<T> InSessionAsync<T>(string portName, int baudRate,
        Func<SerialFrameMatchBuffer, SerialPortConfig, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ownedPorts.TryGetValue(portName, out var queuedConnection);
        using var queuedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            queuedConnection?.Token ?? CancellationToken.None);
        var gate = _portGates.GetOrAdd(portName, _ => new(1, 1));
        try
        {
            await gate.WaitAsync(queuedCancellation.Token);
        }
        catch (OperationCanceledException) when (queuedConnection?.Error != null && !cancellationToken.IsCancellationRequested)
        {
            throw queuedConnection.Error;
        }
        try
        {
            // 排队结束时连接可能已关闭，不能转为临时会话重新打开旧工位端口。
            if (queuedConnection?.Error != null && !cancellationToken.IsCancellationRequested)
                throw queuedConnection.Error;
            queuedCancellation.Token.ThrowIfCancellationRequested();
            _ownedPorts.TryGetValue(portName, out var connection);
            var temporary = connection == null;
            var config = connection?.Config ?? new SerialPortConfig { BaudRate = baudRate, DataBits = 8, Parity = SerialParity, StopBits = "One" };
            if (temporary)
            {
                if (serial.IsOpen(portName)) throw new InvalidOperationException($"{portName} 已被打开，请先关闭已有连接。");
                if (!await serial.OpenAsync(portName, config)) throw new IOException($"打开 {portName} 失败。");
            }
            using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                connection?.Token ?? CancellationToken.None);
            IOException? disconnected = null;
            using var buffer = new SerialFrameMatchBuffer(GasDataObjectProtocol.TryExtractFrame);
            void Receive(object? sender, SerialDataReceivedEventArgs args)
            {
                if (!string.Equals(args.PortName, portName, StringComparison.OrdinalIgnoreCase)) return;
                Logger.Debug("报警主机接收 {0}：{1}", portName, BitConverter.ToString(args.Data));
                buffer.Append(args.Data);
            }
            void StatusChanged(object? sender, SerialPortStatusChangedEventArgs args)
            {
                if (!string.Equals(args.PortName, portName, StringComparison.OrdinalIgnoreCase) ||
                    args.Status is not (SerialPortStatus.Disconnected or SerialPortStatus.Closed)) return;
                disconnected = new IOException($"{portName} 串口已断开：{args.Message}", args.Exception);
                // 已进入回调的状态通知可能晚于退订完成，避免取消已经释放的会话源。
                try { sessionCancellation.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            try
            {
                sessionCancellation.Token.ThrowIfCancellationRequested();
                serial.RawDataReceived += Receive;
                if (temporary) serial.PortStatusChanged += StatusChanged;
                Logger.Debug("报警主机会话 {0}，{1}/{2}/{3}/{4}", portName, config.BaudRate, config.DataBits, config.Parity, config.StopBits);
                return await operation(buffer, config, sessionCancellation.Token);
            }
            catch (OperationCanceledException) when ((disconnected ?? connection?.Error) != null && !cancellationToken.IsCancellationRequested)
            {
                var error = disconnected ?? connection!.Error!;
                Logger.Warn(error, "报警主机串口中断 {0}", portName);
                throw error;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Logger.Error(error, "报警主机执行失败，串口{0}，波特率{1}，校验{2}", portName, config.BaudRate, config.Parity);
                throw;
            }
            finally
            {
                serial.RawDataReceived -= Receive;
                if (temporary)
                {
                    serial.PortStatusChanged -= StatusChanged;
                    await serial.CloseAsync(portName);
                }
            }
        }
        finally { gate.Release(); }
    }

    /// <summary>
    /// 单次指令交换：发送前导及帧体后等待应答，三秒响应窗口另加完整报文线上传输预算
    /// 公共SendAsync写入驱动完成不保证物理发送结束，低波特率不能只留三秒总窗口
    /// </summary>
    private async Task<byte[]> ExchangeAsync(string portName, SerialPortConfig config, SerialFrameMatchBuffer buffer, Func<byte[]> buildFrame,
        Func<byte[], bool> matcher, ProtocolCommand command, CancellationToken cancellationToken)
    {
        if (command.TimeoutMs <= 0 || command.RetryCount < 0 || command.SendDelayMs < 0)
            throw new ArgumentException("协议命令的超时必须为正数，重发次数和延时不能为负数。");
        var stopBits = config.StopBits switch { "Two" => 2d, "OnePointFive" => 1.5d, "None" => 0d, _ => 1d };
        var bitsPerByte = 1 + config.DataBits + (config.Parity == "None" ? 0 : 1) + stopBits;
        for (var attempt = 0; attempt <= command.RetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!serial.IsOpen(portName)) throw new IOException($"{portName} 串口已关闭，已终止本工位执行。");
            if (command.SendDelayMs > 0) await Task.Delay(command.SendDelayMs, cancellationToken);
            var frame = buildFrame();
            byte[] request = [.. Enumerable.Repeat((byte)0xFE, PreambleLength), .. frame];
            var transmissionMs = (int)Math.Ceiling(request.Length * bitsPerByte * 1000 / config.BaudRate);
            buffer.Clear();
            Logger.Debug("报警主机发送 {0}，第{1}次，前导FE×{2}，帧体：{3}", portName, attempt + 1, PreambleLength, BitConverter.ToString(frame));
            try { await serial.SendAsync(portName, request).WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { throw new IOException($"{portName} 串口发送失败：{error.Message}", error); }
            if (!serial.IsOpen(portName)) throw new IOException($"{portName} 串口已关闭，已终止本工位执行。");
            var response = await buffer.WaitForFrameAsync(matcher, command.TimeoutMs + transmissionMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (response != null) return response;
            if (attempt < command.RetryCount) Logger.Warn("报警主机{0}命令{1}超时，将重发第{2}次", portName, command.CommandType, attempt + 1);
        }
        throw new TimeoutException("设备操作超时，未收到匹配的完整应答。");
    }
}
