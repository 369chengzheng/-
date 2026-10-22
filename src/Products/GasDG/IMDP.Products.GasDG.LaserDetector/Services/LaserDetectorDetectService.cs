// =============================================================================
// 文件名：LaserDetectorDetectService.cs
// 功能描述：家用可燃气体激光检测仪检测服务。
//           查询配置后按工位数创建各工位独立通信单元（List 接收缓冲跨轮复用 +
//           每轮重建的 Channel 发送队列），工位之间互不干扰；指令经本工位
//           Channel 串行发送，应答经本工位缓冲按「指令位 + 帧内工位号」匹配。
//           生命周期三级模型：
//           ① 配置级：加载方案时创建 N 套通信单元，重载/关串口时全量销毁；
//           ② 轮级：每轮检测开始清空缓冲并重建 CTS/Channel，结束时同步
//              Cancel（丢弃未发送指令）+ Clear（清空缓冲），不销毁单元；
//           ③ 关闭串口：退订事件 → 取消全部令牌 → 释放资源 → 最后关串口。
//           组合 ISerialService 与 LaserDetectorProtocol，封装单工位完整
//           检测流程：下发开始测试指令 → 轮询查询测试结果 → 解析判定。
// =============================================================================
using System.Collections.Concurrent;
using System.Threading.Channels;
using IMDP.Domain.Interfaces;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.LaserDetector.Models;
using IMDP.Products.GasDG.LaserDetector.Protocol;

namespace IMDP.Products.GasDG.LaserDetector.Services;

/// <summary>
/// 激光检测仪检测服务
/// <para>封装单工位自动检测的完整串口交互：开始测试（0xAA）→ 轮询查询结果（0xAC）
/// → 解析 0xAD 结果帧并按配置判定合格性。每个工位拥有独立的接收缓冲与发送 Channel，
/// 多工位可并行检测且应答帧互不串扰。</para>
/// </summary>
public class LaserDetectorDetectService
{
    #region 内部类型

    /// <summary>
    /// 单工位待发送项（协议帧 + 发送日志回调）
    /// </summary>
    private sealed class StationSendItem
    {
        /// <summary>待发送的完整协议帧</summary>
        public byte[] Frame { get; init; } = Array.Empty<byte>();

        /// <summary>发送日志回调（可选，记录本帧下发的十六进制内容）</summary>
        public Action<byte[]>? SendCallback { get; init; }
    }

    /// <summary>
    /// 单工位独立通信单元：接收缓冲跨轮复用，发送链路（CTS/Channel/消费任务）每轮重建
    /// </summary>
    private sealed class StationCommState
    {
        /// <summary>工位号（1 起）</summary>
        public int Station { get; init; }

        /// <summary>
        /// 本工位独立接收匹配缓冲（List + 锁 + 滑动窗口匹配）
        /// <para>随通信单元创建，检测轮间 Clear 复用；仅在全量清理（重载配置/关闭串口）时释放。</para>
        /// </summary>
        public SerialFrameMatchBuffer Buffer { get; init; } = null!;

        /// <summary>本轮发送取消源（检测轮开始时重建；清理时 Cancel 同步终止消费循环）</summary>
        public CancellationTokenSource SendCts = new();

        /// <summary>本轮发送队列（检测轮开始时重建；工位内 FIFO 有序，取消后不可复用）</summary>
        public Channel<StationSendItem> SendChannel = null!;

        /// <summary>本轮发送消费任务（排空 Channel 并写串口，随 CTS 取消退出）</summary>
        public Task ConsumerTask = Task.CompletedTask;

        /// <summary>
        /// 重置本轮发送链路：新建 CTS 与 Channel（旧链路须已 Cancel，交由 GC 回收）
        /// </summary>
        public void ResetSendLink()
        {
            SendCts = new CancellationTokenSource();
            SendChannel = Channel.CreateUnbounded<StationSendItem>(
                new UnboundedChannelOptions { SingleReader = true });
        }
    }

    #endregion

    #region 字段与构造

    /// <summary>串口服务（单例，由容器注入）</summary>
    private readonly ISerialService _serialService;

    /// <summary>全局配置服务（获取串口通讯参数）</summary>
    private readonly IGlobalConfigService _globalConfigService;

    /// <summary>各工位独立通信单元（键 = 工位号；查询配置后按工位数创建）</summary>
    private readonly ConcurrentDictionary<int, StationCommState> _stationStates = new();

    /// <summary>保护串口事件订阅与工位单元整体操作的对象锁（订阅/退订/创建/销毁互斥）</summary>
    private readonly object _lifecycleLock = new();

    /// <summary>已订阅原始数据事件的串口名（空 = 未订阅；同一时刻仅订阅一个串口）</summary>
    private string _subscribedPortName = string.Empty;

    /// <summary>两次查询结果之间的间隔（毫秒）</summary>
    private const int QueryIntervalMs = 500;

    /// <summary>单次等待工装应答的超时（毫秒）</summary>
    private const int SerialTimeoutMs = 1500;

    /// <summary>开始测试指令（0xAA）未收到应答时的最大重发次数（首发 + 3 次重发 = 共 4 次发送）</summary>
    private const int MaxResendCount = 3;

    /// <summary>串口线上任意两条指令之间的最小间隔（毫秒，全局节流）</summary>
    private const int CommandIntervalMs = 2000;

    /// <summary>单工位检测最大超时时间默认值（秒；方案未配置时兜底）</summary>
    private const int DefaultMaxTestTimeoutSeconds = 120;

    /// <summary>
    /// 串口写入门闸：保证同一时刻只有一个工位的消费任务在计算节流与写串口
    /// <para>与 <see cref="_lastSerialWriteTick"/> 配合实现串口级全局 2 秒指令间隔，
    /// 避免多工位并行竞争串口导致指令过密。</para>
    /// </summary>
    private readonly SemaphoreSlim _serialWriteGate = new(1, 1);

    /// <summary>串口最近一次实际写指令的时间戳（TickCount64；Interlocked 原子读写）</summary>
    private long _lastSerialWriteTick;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serialService">串口服务</param>
    /// <param name="globalConfigService">全局配置服务</param>
    public LaserDetectorDetectService(ISerialService serialService, IGlobalConfigService globalConfigService)
    {
        _serialService = serialService;
        _globalConfigService = globalConfigService;
    }

    #endregion

    #region 串口事件订阅

    /// <summary>
    /// 串口原始数据回调：将本串口数据广播到全部工位的独立接收缓冲
    /// <para>各缓冲内部按「帧内工位号」匹配提取，工位之间互不消费对方的数据帧。</para>
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">串口原始数据事件参数</param>
    private void OnSerialDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        if (e.PortName != _subscribedPortName)
        {
            return;
        }

        foreach (var state in _stationStates.Values)
        {
            state.Buffer.Append(e.Data);
        }
    }

    /// <summary>
    /// 订阅串口原始数据事件（锁内调用；重复打开同一串口时仅保持一份订阅）
    /// </summary>
    /// <param name="portName">串口名称</param>
    private void SubscribeSerialDataLocked(string portName)
    {
        if (_subscribedPortName == portName)
        {
            return;
        }

        _serialService.RawDataReceived += OnSerialDataReceived;
        _subscribedPortName = portName;
    }

    /// <summary>
    /// 退订串口原始数据事件（锁内调用；未订阅时无操作）
    /// </summary>
    private void UnsubscribeSerialDataLocked()
    {
        if (string.IsNullOrEmpty(_subscribedPortName))
        {
            return;
        }

        _serialService.RawDataReceived -= OnSerialDataReceived;
        _subscribedPortName = string.Empty;
    }

    #endregion

    #region 串口打开与关闭

    /// <summary>
    /// 打开指定串口（串口参数取自全局配置），成功后订阅原始数据事件
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <returns>打开成功返回串口配置（含波特率等参数）；失败返回 null</returns>
    public async Task<SerialPortConfig?> OpenPortAsync(string portName)
    {
        var config = await _globalConfigService.GetSerialPortConfigAsync();
        var opened = await _serialService.OpenAsync(portName, config);
        if (!opened)
        {
            return null;
        }

        lock (_lifecycleLock)
        {
            SubscribeSerialDataLocked(portName);
        }

        return config;
    }

    /// <summary>
    /// 关闭指定串口：退订事件 → 同步取消全部工位令牌并释放资源 → 最后关闭串口
    /// <para>取消令牌同步执行（Cancel 返回时消费循环已终止，未发送指令全部丢弃，
    /// 不会向即将关闭的串口写入）；在途检测轮经「缓冲释放 → 等待返回 null」
    /// 走既有通信失败路径优雅退出。</para>
    /// </summary>
    /// <param name="portName">串口名称</param>
    public async Task ClosePortAsync(string portName)
    {
        // ①②③ 全量清理（退订 → 取消 → 释放 → 清字典），必须先于物理关串口完成
        lock (_lifecycleLock)
        {
            UnsubscribeSerialDataLocked();
            TeardownAllStationsLocked();
        }

        // ④ 此时已无任何读写方，安全关闭串口
        await _serialService.CloseAsync(portName);
    }

    /// <summary>
    /// 获取本机可用串口列表
    /// </summary>
    /// <returns>串口名称数组</returns>
    public string[] GetAvailablePorts()
    {
        return _serialService.GetAvailablePorts();
    }

    /// <summary>
    /// 判断指定串口是否已打开
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <returns>已打开返回 true</returns>
    public bool IsPortOpen(string portName)
    {
        return _serialService.IsOpen(portName);
    }

    #endregion

    #region 工位通信单元管理（配置级创建 / 轮级复用 / 全量销毁）

    /// <summary>
    /// 查询配置后按工位数创建各工位独立通信单元（List 接收缓冲 + Channel 发送队列）
    /// <para>先全量销毁上一轮方案的全部工位单元，再为工位 1..stationCount 逐一创建：
    /// 每工位一个独立接收匹配缓冲与一个独立发送链路（CTS + Channel + 消费任务），
    /// 工位之间零共享，多工位并行检测互不干扰。</para>
    /// </summary>
    /// <param name="stationCount">工位数量（≥1，来源于数据库方案配置）</param>
    public void ConfigureStations(int stationCount)
    {
        if (stationCount < 1)
        {
            return;
        }

        lock (_lifecycleLock)
        {
            // 全量销毁旧单元（重载配置 = 配置级销毁点之一）
            TeardownAllStationsLocked();

            for (int station = 1; station <= stationCount; station++)
            {
                CreateStationStateLocked(station);
            }
        }
    }

    /// <summary>
    /// 锁内创建单个工位的独立通信单元（Buffer + 首轮发送链路 + 消费任务）
    /// </summary>
    /// <param name="station">工位号（1 起）</param>
    /// <returns>新创建的工位通信单元</returns>
    private StationCommState CreateStationStateLocked(int station)
    {
        var state = new StationCommState
        {
            Station = station,
            // 帧提取器：复用协议的滑动窗口提取（处理噪声、半包与粘包）
            Buffer = new SerialFrameMatchBuffer(data =>
                LaserDetectorProtocol.TryParseGetFullResponse(data, out var frame) ? frame : null)
        };

        state.ResetSendLink();
        state.ConsumerTask = Task.Run(() => ConsumeSendChannelAsync(state));
        _stationStates[station] = state;
        return state;
    }

    /// <summary>
    /// 锁内全量销毁全部工位通信单元（重载配置 / 关闭串口时的配置级销毁点）
    /// <para>每个工位：同步 Cancel（消费循环立即终止，未发送指令丢弃）→
    /// CTS 释放 → Buffer 释放（清 List + 立即唤醒全部等待者返回 null）→ 移除。</para>
    /// </summary>
    private void TeardownAllStationsLocked()
    {
        foreach (var station in _stationStates.Keys)
        {
            if (!_stationStates.TryRemove(station, out var state))
            {
                continue;
            }

            TeardownStation(state);
        }
    }

    /// <summary>
    /// 销毁单个工位通信单元（同步取消令牌并及时释放资源）
    /// </summary>
    /// <param name="state">待销毁的工位通信单元</param>
    private static void TeardownStation(StationCommState state)
    {
        // ① 同步取消：Cancel() 返回时消费循环已终止，队列残留指令直接丢弃
        state.SendCts.Cancel();
        // ② 释放取消源（Cancel 先于 Dispose，消费方仅消费令牌不持有句柄，安全）
        state.SendCts.Dispose();
        // ③ 释放接收缓冲：清空 List 并立即唤醒全部等待者返回 null
        state.Buffer.Dispose();
    }

    /// <summary>
    /// 获取指定工位的通信单元；不存在时惰性重建
    /// <para>正常由 <see cref="ConfigureStations"/> 创建；关串口全量清理后或
    /// 检测轮次超出方案工位数时兜底重建，保证流程不中断。</para>
    /// </summary>
    /// <param name="station">工位号（1 起）</param>
    /// <returns>该工位的独立通信单元</returns>
    private StationCommState GetOrCreateStationState(int station)
    {
        // 快路径：已存在直接返回（无需加锁，字典本身线程安全）
        if (_stationStates.TryGetValue(station, out var existing))
        {
            return existing;
        }

        lock (_lifecycleLock)
        {
            // 双重检查：并发惰性重建时只创建一次
            if (_stationStates.TryGetValue(station, out existing))
            {
                return existing;
            }

            return CreateStationStateLocked(station);
        }
    }

    /// <summary>
    /// 单工位发送 Channel 消费循环：排空队列并逐帧写入串口
    /// <para>工位内严格按入队顺序发送（FIFO）；写串口前经串口级全局节流，
    /// 保证整条串口线上任意两条指令间隔不小于 <see cref="CommandIntervalMs"/>；
    /// CTS 取消时立即终止枚举，队列中未发送的指令全部丢弃
    /// （清理时刻之后不再有任何指令上串口）。</para>
    /// </summary>
    /// <param name="state">工位通信单元</param>
    private async Task ConsumeSendChannelAsync(StationCommState state)
    {
        try
        {
            await foreach (var item in state.SendChannel.Reader
                .ReadAllAsync(state.SendCts.Token).ConfigureAwait(false))
            {
                try
                {
                    // 串口级全局节流：与最近一次实际写串口间隔不足 2s 时补足等待
                    //（门闸内计算等待时长，门闸外 Delay，避免长时间占闸阻塞其他工位）
                    await ThrottleToIntervalAsync().ConfigureAwait(false);
                    item.SendCallback?.Invoke(item.Frame);
                    await _serialService.SendAsync(_subscribedPortName, item.Frame).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // 单帧发送异常吞掉：检测流程由应答超时兜底判定通信失败
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出路径：检测轮结束/全量清理时取消令牌，残留未发送指令已丢弃
        }
    }

    /// <summary>
    /// 串口级全局指令间隔节流：等待至距最近一次实际写串口满 2 秒
    /// <para>门闸串行化「读取时间戳 → 计算等待 → 预约发送时刻」的检查序列；
    /// 等待在门闸外进行（预约时刻 = 上次发送 + 2s，各工位按预约时刻排队互不插队），
    /// 从根本上解决单串口多工位并行时指令竞争过密的问题。</para>
    /// </summary>
    private async Task ThrottleToIntervalAsync()
    {
        int waitMs;
        await _serialWriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
            long last = Interlocked.Read(ref _lastSerialWriteTick);
            long now = Environment.TickCount64;
            long elapsed = now - last;
            // 预约本条指令的最早发送时刻（上次发送 + 间隔）；
            // 预约同时前移时间戳，后续工位将以本预约时刻为基准继续排队
            long reservedTick = Math.Max(now, last + CommandIntervalMs);
            waitMs = (int)Math.Min(int.MaxValue, reservedTick - now);
            Interlocked.Exchange(ref _lastSerialWriteTick, reservedTick);
        }
        finally
        {
            _serialWriteGate.Release();
        }

        if (waitMs > 0)
        {
            await Task.Delay(waitMs).ConfigureAwait(false);
        }
    }

    #endregion

    #region 检测流程

    /// <summary>
    /// 执行单工位完整自动检测流程
    /// <para>流程：经本工位 Channel 下发开始测试指令（未收到 0xAB 应答时自动重发，
    /// 首发后最多重发 <see cref="MaxResendCount"/> 次）→ 经本工位 Channel 持续轮询
    /// 查询结果直到收到 0xAD 结果帧或超出工位总超时 → 按当前配置判定各项是否合格。
    /// 应答帧从本工位独立接收缓冲中按「指令位 + 帧内工位号」匹配，多工位并行互不串扰；
    /// 每轮开始清空缓冲（防上轮迟到帧串轮）并重建发送链路，
    /// 本轮结束（成功/失败/超时）同步 Cancel 令牌并清空缓冲。</para>
    /// </summary>
    /// <param name="station">界面工位号（正整数，来源数据库方案配置）</param>
    /// <param name="config">当前产品的激光检测配置</param>
    /// <param name="maxTestTimeoutSeconds">单工位检测最大超时时间（秒；≤0 时取默认 120）</param>
    /// <param name="commandSentCallback">指令发送回调（可选，用于日志记录每条实际下发的指令帧）</param>
    /// <returns>检测成功且解析通过时返回结果对象；超时或失败时返回 null</returns>
    public async Task<LaserDetectorTestResult?> DetectAsync(
        int station,
        LaserDetectorConfig config,
        int maxTestTimeoutSeconds,
        Action<byte[]>? commandSentCallback = null)
    {
        var commState = GetOrCreateStationState(station);
        byte protocolStation = (byte)(station - 1);
        CancellationTokenSource sendCts = commState.SendCts;
        int timeoutSeconds = maxTestTimeoutSeconds > 0 ? maxTestTimeoutSeconds : DefaultMaxTestTimeoutSeconds;
        try
        {
            lock (_lifecycleLock)
            {
                // 轮级重置：清空上轮残留（迟到帧/半包字节），重建本轮发送链路
                //（旧 CTS 已在上轮 finally Cancel，旧对象交 GC；Buffer 跨轮复用不销毁）
                commState.Buffer.Clear();
                commState.ResetSendLink();
                sendCts = commState.SendCts;
                commState.ConsumerTask = Task.Run(() => ConsumeSendChannelAsync(commState));
            }

            // 第一步：下发开始测试指令（0xAA）并等待 0xAB 应答；未收到应答时自动重发
            //（首发 + 最多 MaxResendCount 次重发；重发帧经全局节流自然落在 2s 间隔节奏上）
            var startCommand = LaserDetectorProtocol.CreateStartTestCommand(station, config);
            var startAck = await SendWithRetryAsync(
                commState,
                startCommand,
                frame => IsStationResponseFrame(frame, LaserDetectorProtocolConstants.StartTestResponseFlag, protocolStation),
                commandSentCallback);
            if (startAck == null)
            {
                return null;
            }

            // 第二步：持续轮询查询测试结果（0xAC）直到收到 0xAD 结果帧；
            // 轮询节奏由串口全局 2s 节流统一约束（多工位并行时按预约时刻排队），
            // 整体受工位总超时控制，超时仍未收到结果帧则判通信失败
            var queryCommand = LaserDetectorProtocol.CreateQueryTestResultCommand(station);
            long deadlineTick = Environment.TickCount64 + timeoutSeconds * 1000L;
            while (Environment.TickCount64 < deadlineTick)
            {
                await Task.Delay(QueryIntervalMs);
                await SendViaStationChannelAsync(commState, queryCommand, commandSentCallback);
                var queryResponse = await commState.Buffer.WaitForFrameAsync(
                    frame => IsStationResponseFrame(frame, LaserDetectorProtocolConstants.TestResultResponseFlag, protocolStation),
                    SerialTimeoutMs);
                if (queryResponse == null)
                {
                    continue;
                }

                if (LaserDetectorProtocol.TryParseTestResultResponse(queryResponse, config, out var result) &&
                    result.Station == station)
                {
                    return result;
                }
            }

            return null;
        }
        finally
        {
            // 轮级清理：同步取消令牌（丢弃未发送指令）+ 清空缓冲（List 跨轮复用，仅清数据）；
            // 全量清理后单元已不存在（finally 对已取消 CTS 再 Cancel 无感，对已释放 Buffer 的 Clear 安全）
            sendCts.Cancel();
            commState.Buffer.Clear();
        }
    }

    /// <summary>
    /// 发送指令并等待匹配应答帧，未收到应答时自动重发（首发 + 最多 3 次重发）
    /// <para>重发帧经由本工位 Channel 入队，消费任务按串口全局 2s 节流节奏实际发出；
    /// 等待窗口 <see cref="SerialTimeoutMs"/>，重发间隔由节流自动对齐。</para>
    /// </summary>
    /// <param name="commState">工位通信单元</param>
    /// <param name="command">完整协议指令帧</param>
    /// <param name="responseMatcher">应答帧匹配条件</param>
    /// <param name="sendCallback">发送日志回调（可选）</param>
    /// <returns>等到匹配应答帧时返回该帧；全部发送均无应答时返回 null</returns>
    private async Task<byte[]?> SendWithRetryAsync(
        StationCommState commState,
        byte[] command,
        Func<byte[], bool> responseMatcher,
        Action<byte[]>? sendCallback)
    {
        for (int attempt = 0; attempt <= MaxResendCount; attempt++)
        {
            await SendViaStationChannelAsync(commState, command, sendCallback).ConfigureAwait(false);
            var response = await commState.Buffer.WaitForFrameAsync(responseMatcher, SerialTimeoutMs).ConfigureAwait(false);
            if (response != null)
            {
                return response;
            }
        }

        return null;
    }

    /// <summary>
    /// 经指定工位的独立 Channel 异步发送一帧指令（由该工位消费任务按 FIFO 写入串口）
    /// </summary>
    /// <param name="commState">工位通信单元</param>
    /// <param name="frame">完整协议指令帧</param>
    /// <param name="sendCallback">发送日志回调（可选）</param>
    private static async Task SendViaStationChannelAsync(
        StationCommState commState,
        byte[] frame,
        Action<byte[]>? sendCallback)
    {
        await commState.SendChannel.Writer.WriteAsync(
            new StationSendItem { Frame = frame, SendCallback = sendCallback }).ConfigureAwait(false);
    }

    /// <summary>
    /// 判断数据帧是否为指定工位的指定指令位应答帧（帧头 + 指令位 + 帧内工位号匹配）
    /// <para>用于从本工位接收缓冲中匹配属于自己的应答帧，多工位并行时互不串扰。</para>
    /// </summary>
    /// <param name="frame">待判断的完整数据帧</param>
    /// <param name="responseFlag">期望的应答指令位（0xAB 或 0xAD）</param>
    /// <param name="protocolStation">协议工位值（界面工位号 - 1）</param>
    /// <returns>帧内工位号与指令位均匹配时返回 true</returns>
    private static bool IsStationResponseFrame(byte[] frame, byte responseFlag, byte protocolStation)
    {
        return frame.Length >= LaserDetectorProtocolConstants.ShortFrameLength &&
               frame[0] == LaserDetectorProtocolConstants.FrameHeader &&
               frame[1] == responseFlag &&
               frame[3] == protocolStation;
    }

    #endregion
}
