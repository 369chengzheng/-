using System.IO;
using System.Collections.Concurrent;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.AlarmHost.Models;
using NLog;
using IMDP.Infrastructure.Protocol.GAS_Protocol.Gas_C5F0;

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
        var valveItems = items.Where(item => AlarmHostFunctionCatalog.Get(item.Code).ObjectId == 0x0200).ToArray();
        var valveTarget = AlarmHostExecutionCodec.EncodeValveConfiguration(valveItems);
        // 公共命令类型沿用平台：1写参数、2读参数、3读版本，不用对象编号替代公共命令码。
        ProtocolCommand Command(int type) => commands?.FirstOrDefault(command => command.CommandType == type)
            ?? new ProtocolCommand { CommandType = type };
        foreach (var type in new[] { 1, 2, 3 })
        {
            var command = Command(type);
            if (command.TimeoutMs <= 0 || command.RetryCount < 0 || command.SendDelayMs < 0)
                throw new ArgumentException("协议命令的超时必须为正数，重发次数和延时不能为负数。", nameof(commands));
        }
        // 同优先级先聚合同类命令，避免目录顺序调整后拆散通信批次
        var ordered = items.Where(item => item.CanExecute)
            .OrderBy(item => Command(CommandType(item, planType)).Order)
            .ThenBy(item => CommandType(item, planType)).ThenBy(item => item.Order).ToList();
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
            void Fail(IEnumerable<AlarmHostResultRow> rows, Exception error, string? phase = null)
            {
                Logger.Error(error, "报警主机{0}项目执行失败", portName);
                var message = phase == null ? error.Message : $"{phase}：{error.Message}";
                foreach (var row in rows) report(new(row.Code, "失败", "", false, message));
                passed = false;
            }
            void ReportValue(IEnumerable<AlarmHostResultRow> rows, byte[] value, byte? writeResult)
            {
                foreach (var row in rows)
                {
                    AlarmHostItemResult result;
                    try
                    {
                        targets.TryGetValue(row.Code, out var expected);
                        result = AlarmHostExecutionCodec.Evaluate(row, value, scanCode, DateTime.Now, expected);
                    }
                    catch (Exception error) when (error is FormatException or InvalidOperationException or ArgumentException or OverflowException)
                    {
                        result = new(row.Code, "失败", "", false, error.Message);
                    }
                    if (writeResult is > 0) result = result with { Message = $"写应答结果码：0x{writeResult.Value:X2}, {result.Message}" };
                    report(result);
                    passed &= result.IsPass != false;
                }
            }

            // 设置的生产态、自检及需要写前读取的对象保留独立步骤；检验普通项统一批读。
            async Task ExecuteSingleAsync(IGrouping<ushort, AlarmHostResultRow> group)
            {
                var groupedItems = group.ToArray();
                var item = groupedItems[0];
                var id = group.Key;
                sessionToken.ThrowIfCancellationRequested();
                if (planType == 0 && item == production && targets[item.Code][0] == 1 && !passed)
                {
                    report(new(item.Code, "未执行", "", false, "前序项目未通过，未切换出厂态"));
                    return;
                }
                var failurePhase = "读取失败";
                try
                {
                    byte? writeResult = null;
                    byte[]? writeValue = null;
                    if (planType == 0 && AlarmHostFunctionCatalog.Get(item.Code).CanWrite)
                    {
                        writeValue = targets[item.Code];
                    }
                    if (writeValue != null) failurePhase = "写前检查失败，尚未发送设置";
                    async Task<byte[]> ReadAsync() => Protocol.ReadObjectResponse(
                        await ExchangeAsync(portName, config, buffer, () => Protocol.BuildReadFrame(id),
                            frame => Protocol.MatchesReadResponse(frame, id), Command(item.Code == AlarmHostFunctionCatalog.Version ? 3 : 2), sessionToken), id);
                    if (writeValue != null && item.Code is AlarmHostFunctionCatalog.MeterSpecification or AlarmHostFunctionCatalog.PulseResolution)
                    {
                        // 规格原表有长度矛盾，001D为旧UGNB设备枚举：先核实设备读布局，再写入。
                        var current = await ReadAsync();
                        if (current.Length != writeValue.Length || (item.Code == AlarmHostFunctionCatalog.PulseResolution && current[0] > 1))
                            throw new FormatException($"{item.Name}的设备格式与已登记UGNB布局不符，未下发设置：{Convert.ToHexString(current)}");
                    }
                    if (writeValue != null && item.Code == AlarmHostFunctionCatalog.SettlementFunctions)
                    {
                        var current = await ReadAsync();
                        if (current.Length != 1) throw new FormatException("结算功能字段应为1字节。");
                        writeValue = [(byte)((current[0] & 0xF8) | writeValue[0])];
                    }
                    if (writeValue != null)
                    {
                        failurePhase = "写入未确认，未执行回读";
                        // 每次真正发送前取时，排队、前序步骤、发送延时和重发均不使用旧表时。
                        byte[] BuildWrite()
                        {
                            if (item.Code == AlarmHostFunctionCatalog.MeterTime)
                                writeValue = targets[item.Code] = AlarmHostExecutionCodec.Encode(item, scanCode, DateTime.Now);
                            return Protocol.BuildWriteFrame(id, writeValue);
                        }
                        var writeCommand = Command(1);
                        // 自检触发只发一次，普通读超时重试不应用到触发写上。
                        if (item.Code == AlarmHostFunctionCatalog.CommunicationSelfCheck)
                            writeCommand = new ProtocolCommand { TimeoutMs = writeCommand.TimeoutMs,
                                RetryCount = 0, SendDelayMs = writeCommand.SendDelayMs };
                        var response = await ExchangeAsync(portName, config, buffer, BuildWrite,
                            frame => Protocol.MatchesWriteResponse(frame, id), writeCommand, sessionToken);
                        writeResult = Protocol.ReadWriteResponse(response, id);
                        failurePhase = "已收到写应答，回读失败";
                        if (writeResult is > 0)
                            Logger.Warn("报警主机{0}项目{1}写结果码0x{2:X2}，继续回读最终值", portName, item.Code, writeResult.Value);
                    }
                    var value = await ReadAsync();
                    if (item.Code == AlarmHostFunctionCatalog.CommunicationSelfCheck)
                    {
                        // 沿旧UGNB节拍：FF等待15秒后再读，最多追加两次，任何工位均可取消。
                        for (var poll = 0; value.Length == 2 && value[1] == 0xFF && poll < 2; poll++)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(15), sessionToken);
                            value = await ReadAsync();
                        }
                    }
                    ReportValue(groupedItems, value, writeResult);
                }
                catch (OperationCanceledException) { throw; }
                catch (IOException error) { throw new IOException($"{failurePhase}：{error.Message}", error); }
                catch (Exception error)
                {
                    Fail(groupedItems, error, failurePhase);
                }
            }

            var batch = new List<IGrouping<ushort, AlarmHostResultRow>>();
            async Task FlushBatchAsync()
            {
                if (batch.Count == 0) return;
                var ids = batch.Select(group => group.Key).ToArray();
                var byId = batch.ToDictionary(group => group.Key);
                var failurePhase = "读取失败";
                try
                {
                    IReadOnlyDictionary<ushort, byte?> writeResults = new Dictionary<ushort, byte?>();
                    var writes = batch.Where(group => planType == 0 && AlarmHostFunctionCatalog.Get(group.First().Code).CanWrite &&
                        (group.Key != 0x0200 || valveTarget[..8].Any(value => value != 0))).ToArray();
                    if (writes.Length > 0)
                    {
                        failurePhase = "写入未确认，未执行回读";
                        var writeIds = writes.Select(group => group.Key).ToArray();
                        Logger.Debug("报警主机{0}批量设置，{1}个对象：{2}", portName, writeIds.Length,
                            string.Join(",", writeIds.Select(id => $"{id:X4}")));
                        byte[] BuildWrite()
                        {
                            foreach (var group in writes)
                                if (group.First().Code == AlarmHostFunctionCatalog.MeterTime)
                                    targets[group.First().Code] = AlarmHostExecutionCodec.Encode(group.First(), scanCode, DateTime.Now);
                            return Protocol.BuildWriteFrame(writes.Select(group =>
                                new KeyValuePair<ushort, byte[]>(group.Key, group.Key == 0x0200 ? valveTarget : targets[group.First().Code])).ToArray());
                        }
                        var response = await ExchangeAsync(portName, config, buffer, BuildWrite,
                            frame => Protocol.MatchesWriteResponse(frame, writeIds), Command(1), sessionToken);
                        writeResults = Protocol.ReadWriteResponses(response, writeIds);
                        failurePhase = "已收到写应答，回读失败";
                    }
                    Logger.Debug("报警主机{0}批量读取，{1}个对象：{2}", portName, ids.Length,
                        string.Join(",", ids.Select(id => $"{id:X4}")));
                    var readResponse = await ExchangeAsync(portName, config, buffer, () => Protocol.BuildReadFrame(ids),
                        frame => Protocol.MatchesReadResponse(frame, ids),
                        Command(batch.All(group => group.First().Code == AlarmHostFunctionCatalog.Version) ? 3 : 2), sessionToken);
                    var values = Protocol.ReadObjectsResponse(readResponse, (id, remaining) =>
                    {
                        if (!byId.TryGetValue(id, out var group))
                            throw new FormatException($"批量应答包含非本次对象0x{id:X4}。");
                        return AlarmHostExecutionCodec.ReadValueLength(group.First().Code, remaining);
                    });
                    foreach (var group in batch)
                    {
                        if (!values.TryGetValue(group.Key, out var value))
                            Fail(group, new FormatException($"设备未返回对象0x{group.Key:X4}。"), failurePhase);
                        else
                        {
                            writeResults.TryGetValue(group.Key, out var writeResult);
                            ReportValue(group, value, writeResult);
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (IOException error) { throw new IOException($"{failurePhase}：{error.Message}", error); }
                catch (Exception error) { Fail(batch.SelectMany(group => group), error, failurePhase); }
                finally { batch.Clear(); }
            }

            // 阀控放在批末尾，兼容已登记的18字节完整读回和旧设备10字节值读回。
            // 版本也是C5F0读对象，不因平台的命令分类3再拆一帧。
            var groups = ordered.GroupBy(item => AlarmHostFunctionCatalog.Get(item.Code).ObjectId!.Value)
                .OrderBy(group => planType == 0 && group.First() == production ? (targets[production.Code][0] == 0 ? -1 : 3) :
                    group.First().Code == AlarmHostFunctionCatalog.CommunicationSelfCheck ? 2 : group.Key == 0x0200 ? 1 : 0);
            // 加密后连同公共头不超过300字节；只在容量不足或确有独立步骤时分批。
            var batchLength = 1;
            foreach (var group in groups)
            {
                sessionToken.ThrowIfCancellationRequested();
                var item = group.First();
                var separate = item.Code == AlarmHostFunctionCatalog.CommunicationSelfCheck ||
                    (planType == 0 && (item == production || item.Code is AlarmHostFunctionCatalog.MeterSpecification or
                        AlarmHostFunctionCatalog.PulseResolution or AlarmHostFunctionCatalog.SettlementFunctions));
                var writeLength = planType == 0 && targets.TryGetValue(item.Code, out var target) ?
                    (group.Key == 0x0200 ? valveTarget.Length : target.Length) : 0;
                var length = 2 + Math.Max(writeLength, AlarmHostExecutionCodec.MaximumReadLength(item.Code));
                if (batch.Count > 0 && (separate || batchLength + length > 255))
                {
                    await FlushBatchAsync();
                    batchLength = 1;
                }
                if (separate)
                {
                    await ExecuteSingleAsync(group);
                    if (planType == 0 && item == production && targets[item.Code][0] == 0 && !passed) break;
                }
                else
                {
                    batch.Add(group);
                    batchLength += length;
                }
            }
            await FlushBatchAsync();
            return passed;
        }, cancellationToken);
    }

    /// <summary>
    /// 项目按公共命令类别排序；同类普通对象合并，特殊对象保留独立步骤
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
            Logger.Debug("报警主机请求 {0}，第{1}次，前导FE×{2}，帧体{3}字节", portName, attempt + 1, PreambleLength, frame.Length);
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
