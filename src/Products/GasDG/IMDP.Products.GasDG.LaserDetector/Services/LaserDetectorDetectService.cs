// =============================================================================
// 文件名：LaserDetectorDetectService.cs
// 功能描述：家用可燃气体激光检测仪检测服务。
//           查询配置后按工位数创建各工位独立通信单元（List 接收缓冲跨轮复用 +
//           每轮重建的 Channel 发送队列），工位之间互不干扰；指令经本工位
//           Channel 串行发送，应答经本工位缓冲按「通信阶段 + 指令位 + 帧内工位号」匹配。
//           工位通信状态机（应答帧按阶段门槛匹配，杜绝上轮迟到 0xAD 串轮）：
//           A 待测（忽略全部应答帧）→ B 开始（AA 仅匹配 0xAB，期间到达的
//           0xAD 直接忽略）→ C 查询（AC 仅匹配 0xAD，进入前清场丢弃迟到帧）
//           → D 正常结束 / E 异常结束（及时取消发送区在途指令、忽略后续帧）→ 回 A。
//           生命周期三级模型：
//           ① 配置级：加载方案时创建 N 套通信单元，重载/关串口时全量销毁；
//           ② 轮级：每轮检测开始清空缓冲并重建 CTS/Channel，结束时同步
//              Cancel（丢弃未发送指令）+ Clear（清空缓冲），不销毁单元；
//           ③ 关闭串口：退订事件 → 取消全部令牌 → 释放资源 → 最后关串口。
//           组合 ISerialService 与 LaserDetectorProtocol，封装单工位完整
//           检测流程：下发开始测试指令 → 轮询查询测试结果 → 解析判定。
//           应答等待结果（匹配成功/超时）经 NLog 落盘，便于现场区分
//           「工装未应答」与「应答被解析/匹配环节丢弃」。
//           应答等待窗一律从「实际写串口完成」起算（StationSendItem.SentCompletion
//           信号）：发送经 Channel 排队 + 串口全局 2s 节流，入队时刻远早于上线
//           时刻，若从入队起算，多工位并发时后入队工位的等待窗早于指令上线，
//           必然超时（三工位时后扫码工位无法启动的根因）。
// =============================================================================
using System.Collections.Concurrent;
using System.Threading.Channels;
using IMDP.Domain.Interfaces;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Logging;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.LaserDetector.Models;
using IMDP.Products.GasDG.LaserDetector.Protocol;
using NLog;

namespace IMDP.Products.GasDG.LaserDetector.Services;

/// <summary>
/// 激光检测仪检测服务
/// <para>封装单工位自动检测的完整串口交互：开始测试（0xAA）→ 轮询查询结果（0xAC）
/// → 解析 0xAD 结果帧并按配置判定合格性。每个工位拥有独立的接收缓冲与发送 Channel，
/// 多工位可并行检测且应答帧互不串扰。</para>
/// <para>工位通信内置 A-E 状态机：A 待测（忽略全部应答帧）→ B 开始（仅匹配 0xAB，
/// 期间到达的迟到 0xAD 直接忽略）→ C 查询（仅匹配 0xAD，进入前清场丢弃迟到帧）
/// → D 正常结束 / E 异常结束（及时取消发送区在途指令、忽略后续应答帧）→ 回 A。
/// 应答帧匹配在「指令位 + 帧内工位号」之上叠加阶段门槛，防止上一轮迟到的
/// 0xAD 结果帧被本轮查询快路径误匹配导致「秒出结果」。</para>
/// </summary>
public class LaserDetectorDetectService
{
    /// <summary>NLog 日志器（应答等待与匹配结果记录）</summary>
    private static readonly ILogger _logger = LogHelper.For<LaserDetectorDetectService>();

    #region 内部类型

    /// <summary>
    /// 单工位检测流程结果类型
    /// <para>区分三种终态，供界面按规则决定 MES 上传行为：
    /// Completed 正常完成；StartFailed 开始指令（0xAA）无应答（不上传 MES）；
    /// QueryTimeout 查询指令（0xAC）总超时（全 0 结果上传 MES）。</para>
    /// </summary>
    public enum LaserDetectorDetectOutcomeKind
    {
        /// <summary>D 正常结束：收到并解析 0xAD 结果帧</summary>
        Completed = 0,

        /// <summary>E 异常结束（开始阶段）：0xAA 重发均无 0xAB 应答，通信失败</summary>
        StartFailed = 1,

        /// <summary>E 异常结束（查询阶段）：0xAC 轮询总超时未收到 0xAD 结果帧</summary>
        QueryTimeout = 2
    }

    /// <summary>
    /// 单工位检测流程结果
    /// <para>Kind 表达终态类型；Result 仅在 Completed 时非空（协议解析后的完整测试结果）。</para>
    /// </summary>
    public sealed class LaserDetectorDetectOutcome
    {
        /// <summary>检测终态类型</summary>
        public LaserDetectorDetectOutcomeKind Kind { get; init; }

        /// <summary>自动测试结果（仅 Completed 非空；StartFailed/QueryTimeout 为 null）</summary>
        public LaserDetectorTestResult? Result { get; init; }

        /// <summary>以 Completed 结果构造</summary>
        private LaserDetectorDetectOutcome(LaserDetectorDetectOutcomeKind kind, LaserDetectorTestResult? result)
        {
            Kind = kind;
            Result = result;
        }

        /// <summary>构造正常完成结果</summary>
        public static LaserDetectorDetectOutcome Completed(LaserDetectorTestResult result) => new(LaserDetectorDetectOutcomeKind.Completed, result);

        /// <summary>构造开始指令无应答结果（0xAA 重发均无 0xAB）</summary>
        public static LaserDetectorDetectOutcome StartFailed() => new(LaserDetectorDetectOutcomeKind.StartFailed, null);

        /// <summary>构造查询总超时结果（0xAC 轮询耗尽总超时）</summary>
        public static LaserDetectorDetectOutcome QueryTimeout() => new(LaserDetectorDetectOutcomeKind.QueryTimeout, null);
    }

    /// <summary>
    /// 工位通信状态机阶段（A 待测 / B 开始 / C 查询 / D 正常结束 / E 异常结束）
    /// <para>状态迁移仅发生在检测流程线程；帧匹配委托在缓冲锁内跨线程执行，
    /// 经 Volatile 读写保证阶段可见性。各阶段应答帧处理规则：
    /// A 忽略全部；B 仅匹配 0xAB（迟到 0xAD 忽略）；C 仅匹配 0xAD（进入前清场）；
    /// D/E 忽略后续全部帧。</para>
    /// </summary>
    internal enum StationCommPhase
    {
        /// <summary>A 待测：空闲态，尚未下发开始测试指令，忽略全部应答帧</summary>
        AwaitingTest = 0,

        /// <summary>B 开始：AA 已下发等待 0xAB 应答，仅匹配 0xAB；
        /// 期间到达的 0xAD（上一轮迟到结果）不匹配、由 B→C 迁移时统一清场丢弃</summary>
        StartSent = 1,

        /// <summary>C 查询：AC 轮询中，仅匹配 0xAD 结果帧</summary>
        Querying = 2,

        /// <summary>D 正常结束：已取得并解析结果，及时清理发送区并忽略后续帧</summary>
        Completed = 3,

        /// <summary>E 异常结束：开始应答失败或总超时，及时清理发送区并忽略后续帧</summary>
        Faulted = 4
    }

    /// <summary>
    /// 单工位待发送项（协议帧 + 发送日志回调 + 实际发送完成信号）
    /// </summary>
    private sealed class StationSendItem
    {
        /// <summary>待发送的完整协议帧</summary>
        public byte[] Frame { get; init; } = Array.Empty<byte>();

        /// <summary>发送日志回调（可选，记录本帧下发的十六进制内容）</summary>
        public Action<byte[]>? SendCallback { get; init; }

        /// <summary>
        /// 实际发送完成信号（true=写串口成功；false=发送失败或被取消，指令未上线）
        /// <para>发送经由本工位 Channel 异步排队并受串口全局 2s 节流，
        /// 「入队时刻」远早于「实际写串口时刻」；应答等待窗必须从实际发送
        /// 时刻起算，否则多工位排队时等待窗早于指令上线，必然超时
        /// （三工位并发时后扫码工位 StartFailed 的根因）。</para>
        /// </summary>
        public TaskCompletionSource<bool> SentCompletion { get; init; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 单工位独立通信单元：接收缓冲跨轮复用，发送链路（CTS/Channel/消费任务）每轮重建
    /// </summary>
    private sealed class StationCommState
    {
        /// <summary>工位号（1 起）</summary>
        public int Station { get; init; }

        /// <summary>
        /// 当前通信阶段（串口回调帧匹配线程与检测流程线程跨线程读写，
        /// 以 int 经 Volatile 读写保证可见性；初始 A 待测）
        /// </summary>
        public StationCommPhase Phase
        {
            get => (StationCommPhase)Volatile.Read(ref _phase);
            set => Volatile.Write(ref _phase, (int)value);
        }

        private int _phase = (int)StationCommPhase.AwaitingTest;

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

    /// <summary>等待「实际写串口完成」的防护上限（毫秒）
    /// <para>发送排队/节流的极端情况下（多工位深度排队），等待方不能无限等；
    /// 超过本上限仍未写串口则视为发送失败处理，由上层重发或超时兜底。</para>
    /// </summary>
    private const int MaxSendWaitMs = 30_000;

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
        // ④ 阶段复位为 A 待测（防御性：单元销毁后不再复用，保持语义完整）
        state.Phase = StationCommPhase.AwaitingTest;
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
    /// 节流等待与写串口前均响应取消令牌：D/E 结束态取消时，
    /// 正在节流等待中的在途指令立即中断（不再上串口线），队列中未发送的指令全部丢弃
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
                    //（门闸内计算等待时长，门闸外 Delay，避免长时间占闸阻塞其他工位）；
                    // Delay 传入令牌：取消时立即中断节流等待，在途指令不再上串口线
                    await ThrottleToIntervalAsync(state.SendCts.Token).ConfigureAwait(false);
                    // 节流等待结束后、写串口前最后检查：等待期间被取消则丢弃本条指令
                    state.SendCts.Token.ThrowIfCancellationRequested();
                    item.SendCallback?.Invoke(item.Frame);
                    await _serialService.SendAsync(_subscribedPortName, item.Frame).ConfigureAwait(false);
                    // 写串口成功：置 true 唤醒等待「实际发送完成」的应答等待方
                    item.SentCompletion.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    // 取消：置 false，等待方立即转入重发/超时判定，不空等应答窗
                    item.SentCompletion.TrySetResult(false);
                    throw;
                }
                catch
                {
                    // 单帧发送异常：置 false 并吞掉（检测流程由应答超时兜底判定通信失败）
                    item.SentCompletion.TrySetResult(false);
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
    /// 从根本上解决单串口多工位并行时指令竞争过密的问题。
    /// Delay 传入取消令牌：工位检测轮结束（D/E 态取消）时，正在节流等待中的
    /// 在途指令立即中断，不再上串口线。</para>
    /// </summary>
    /// <param name="cancellationToken">取消令牌（检测轮结束/单元销毁时取消）</param>
    private async Task ThrottleToIntervalAsync(CancellationToken cancellationToken)
    {
        int waitMs;
        await _serialWriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region 检测流程

    /// <summary>
    /// 执行单工位完整自动检测流程（内置 A-E 通信状态机）
    /// <para>流程：A 待测 → B 开始：经本工位 Channel 下发开始测试指令（未收到 0xAB
    /// 应答时自动重发，首发后最多重发 <see cref="MaxResendCount"/> 次，期间到达的
    /// 迟到 0xAD 直接忽略）→ C 查询：进入前清场丢弃开始态期间到达的迟到 0xAD，
    /// 经本工位 Channel 持续轮询查询结果直到收到 0xAD 结果帧或超出工位总超时
    /// → D 正常结束 / E 异常结束：及时取消发送区（含节流等待中的在途指令）、
    /// 忽略后续应答帧 → finally 清理后回到 A 待测。
    /// 应答帧从本工位独立接收缓冲中按「通信阶段 + 指令位 + 帧内工位号」匹配，
    /// 多工位并行互不串扰；每轮开始清空缓冲（防上轮迟到帧串轮）并重建发送链路，
    /// 本轮结束（成功/失败/超时）同步取消令牌并清空缓冲。</para>
    /// <para>返回结果对象区分三种终态（Completed / StartFailed / QueryTimeout），
    /// 供界面按 MES 上传规则分派：StartFailed 不上传，QueryTimeout 全 0 上传。</para>
    /// </summary>
    /// <param name="station">界面工位号（正整数，来源数据库方案配置）</param>
    /// <param name="config">当前产品的激光检测配置</param>
    /// <param name="maxTestTimeoutSeconds">单工位检测最大超时时间（秒；≤0 时取默认 120）</param>
    /// <param name="commandSentCallback">指令发送回调（可选，用于日志记录每条实际下发的指令帧）</param>
    /// <returns>检测终态结果对象（区分正常完成 / 开始无应答 / 查询总超时）</returns>
    public async Task<LaserDetectorDetectOutcome> DetectAsync(
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
                // A → B 开始态：AA 下发后仅匹配 0xAB，期间到达的迟到 0xAD 不匹配
                commState.Phase = StationCommPhase.StartSent;
            }

            // 第一步：下发开始测试指令（0xAA）并等待 0xAB 应答；未收到应答时自动重发
            //（首发 + 最多 MaxResendCount 次重发；重发帧经全局节流自然落在 2s 间隔节奏上）
            var startCommand = LaserDetectorProtocol.CreateStartTestCommand(station, config);
            var startAck = await SendWithRetryAsync(
                station,
                commState,
                startCommand,
                frame => commState.Phase == StationCommPhase.StartSent &&
                         IsStationResponseFrame(frame, LaserDetectorProtocolConstants.StartTestResponseFlag, protocolStation),
                commandSentCallback);
            if (startAck == null)
            {
                // B → E 异常结束（开始阶段）：重发均无应答，判定通信失败
                commState.Phase = StationCommPhase.Faulted;
                return LaserDetectorDetectOutcome.StartFailed();
            }

            // B → C 查询态迁移：清场丢弃开始态期间到达的迟到 0AD
            //（上一轮在途 0xAC 的应答若恰在本轮开始后到达，将在此被统一取走丢弃，
            //  防止查询阶段快路径秒匹配上一轮结果导致「秒出结果」）
            commState.Phase = StationCommPhase.Querying;
            int purgedCount = PurgeStationResponseFrames(
                commState, LaserDetectorProtocolConstants.TestResultResponseFlag, protocolStation);
            if (purgedCount > 0)
            {
                _logger.Warn($"工位{station} 进入查询态前丢弃上一轮迟到的 0xAD 结果帧 {purgedCount} 帧");
            }

            // 第二步：持续轮询查询测试结果（0xAC）直到收到 0xAD 结果帧；
            // 轮询节奏由串口全局 2s 节流统一约束（多工位并行时按预约时刻排队），
            // 整体受工位总超时控制，超时仍未收到结果帧则判通信失败
            var queryCommand = LaserDetectorProtocol.CreateQueryTestResultCommand(station);
            long deadlineTick = Environment.TickCount64 + timeoutSeconds * 1000L;
            int queryCount = 0;
            while (Environment.TickCount64 < deadlineTick)
            {
                await Task.Delay(QueryIntervalMs);
                var sentTask = await SendViaStationChannelAsync(commState, queryCommand, commandSentCallback);
                var sent = await WaitActualSendAsync(station, sentTask);
                if (!sent)
                {
                    // 本条 0xAC 未实际上线（取消/失败/排队超限）：不空耗应答窗，
                    // 记录后直接进入下一轮循环（总超时仍在控制整体节奏）
                    _logger.Warn($"工位{station} 第{++queryCount}次查询指令未实际上线，继续轮询");
                    continue;
                }

                queryCount++;
                var queryResponse = await commState.Buffer.WaitForFrameAsync(
                    frame => commState.Phase == StationCommPhase.Querying &&
                             IsStationResponseFrame(frame, LaserDetectorProtocolConstants.TestResultResponseFlag, protocolStation),
                    SerialTimeoutMs);
                if (queryResponse == null)
                {
                    _logger.Info(
                        $"工位{station} 第{queryCount}次查询暂无 0xAD 结果帧（测试仍在进行，继续轮询）");
                    continue;
                }

                if (LaserDetectorProtocol.TryParseTestResultResponse(queryResponse, config, out var result))
                {
                    if (result.Station == station)
                    {
                        // C → D 正常结束：已取得并解析结果，后续帧全部忽略
                        commState.Phase = StationCommPhase.Completed;
                        _logger.Info(
                            $"工位{station} 收到测试结果帧(0xAD)({queryResponse.Length}字节)：{BitConverter.ToString(queryResponse).Replace('-', ' ')}");
                        return LaserDetectorDetectOutcome.Completed(result);
                    }

                    // 帧内工位号与界面工位号不符：数据异常，记录后继续轮询
                    _logger.Warn(
                        $"工位{station} 收到 0xAD 帧但帧内工位号为 {result.Station}，与本工位不符，丢弃并继续轮询");
                }
                else
                {
                    // 帧通过了 WaitForFrameAsync 匹配但未通过 TryParseTestResultResponse 校验
                    //（长度/数据域不符等），属于协议层异常，记录帧内容供分析
                    _logger.Warn(
                        $"工位{station} 收到 0xAD 帧但解析校验失败，丢弃并继续轮询：{BitConverter.ToString(queryResponse).Replace('-', ' ')}");
                }
            }

            // C → E 异常结束（查询阶段）：总超时仍未收到有效结果帧，判定通信失败
            commState.Phase = StationCommPhase.Faulted;
            _logger.Error($"工位{station} 检测总超时({timeoutSeconds}s)，轮询期间未收到有效 0xAD 结果帧，判定通信失败");
            return LaserDetectorDetectOutcome.QueryTimeout();
        }
        finally
        {
            // 轮级清理：同步取消令牌（含节流等待中的在途指令，见 ThrottleToIntervalAsync）
            // + 清空缓冲（List 跨轮复用，仅清数据）；随后回到 A 待测态，后续帧全部忽略；
            // 全量清理后单元已不存在（finally 对已取消 CTS 再 Cancel 无感，
            // 对已释放 Buffer 的 Clear 安全）
            sendCts.Cancel();
            commState.Buffer.Clear();
            commState.Phase = StationCommPhase.AwaitingTest;
        }
    }

    /// <summary>
    /// 发送指令并等待匹配应答帧，未收到应答时自动重发（首发 + 最多 3 次重发）
    /// <para>重发帧经由本工位 Channel 入队，消费任务按串口全局 2s 节流节奏实际发出；
    /// 每次尝试均先等待「实际写串口完成」（多工位排队时入队与上线间隔可达数秒，
    /// 应答等待窗必须从实际发送时刻起算，否则窗早于指令上线必然超时），
    /// 再开 <see cref="SerialTimeoutMs"/> 应答窗。每次等待结果（匹配成功/超时）
    /// 均写入 NLog，便于现场区分「工装未应答」与「应答帧不匹配被丢弃」。</para>
    /// </summary>
    /// <param name="station">工位号（1 起，用于日志定位）</param>
    /// <param name="commState">工位通信单元</param>
    /// <param name="command">完整协议指令帧</param>
    /// <param name="responseMatcher">应答帧匹配条件（调用方已含 B 开始态通信阶段门槛）</param>
    /// <param name="sendCallback">发送日志回调（可选）</param>
    /// <returns>等到匹配应答帧时返回该帧；全部发送均无应答时返回 null</returns>
    private async Task<byte[]?> SendWithRetryAsync(
        int station,
        StationCommState commState,
        byte[] command,
        Func<byte[], bool> responseMatcher,
        Action<byte[]>? sendCallback)
    {
        for (int attempt = 0; attempt <= MaxResendCount; attempt++)
        {
            // 先等指令实际上线（防护上限 MaxSendWaitMs），应答窗自实际发送时刻起算
            var sentTask = await SendViaStationChannelAsync(commState, command, sendCallback).ConfigureAwait(false);
            var sent = await WaitActualSendAsync(station, sentTask).ConfigureAwait(false);
            if (!sent)
            {
                _logger.Warn(
                    $"工位{station} 第{attempt + 1}/{MaxResendCount + 1}次发送未实际上线（取消/失败/排队超限），重试");
                continue;
            }

            var response = await commState.Buffer.WaitForFrameAsync(responseMatcher, SerialTimeoutMs).ConfigureAwait(false);
            if (response != null)
            {
                _logger.Info(
                    $"工位{station} 收到开始测试应答(0xAB) 第{attempt + 1}次发送即匹配({response.Length}字节)：{BitConverter.ToString(response).Replace('-', ' ')}");
                return response;
            }

            _logger.Warn(
                $"工位{station} 等待开始测试应答(0xAB)超时({SerialTimeoutMs}ms)，第{attempt + 1}/{MaxResendCount + 1}次发送无应答");
        }

        _logger.Error($"工位{station} 开始测试指令重发 {MaxResendCount} 次均无 0xAB 应答，判定通信失败");
        return null;
    }

    /// <summary>
    /// 等待指令实际写入串口（带防护上限，防消费任务异常导致挂死）
    /// </summary>
    /// <param name="station">工位号（日志定位）</param>
    /// <param name="sentTask">实际发送完成任务（true=成功上线）</param>
    /// <returns>true 表示已实际上线；false 表示取消/失败/等待超限</returns>
    private async Task<bool> WaitActualSendAsync(int station, Task<bool> sentTask)
    {
        try
        {
            var winner = await Task.WhenAny(sentTask, Task.Delay(MaxSendWaitMs)).ConfigureAwait(false);
            if (winner == sentTask)
            {
                return await sentTask.ConfigureAwait(false);
            }

            _logger.Warn($"工位{station} 等待实际发送超过 {MaxSendWaitMs}ms（深度排队或消费任务异常），放弃本条");
            return false;
        }
        catch (OperationCanceledException)
        {
            // 消费任务被取消（检测轮结束/单元销毁）：指令未上线
            return false;
        }
    }

    /// <summary>
    /// 取走并丢弃缓冲区内指定工位的全部指定指令位应答帧
    /// <para>B→C 查询态迁移时调用：开始态期间到达的 0xAD（上一轮在途 0xAC 的迟到
    /// 应答）在此统一取走丢弃，防止查询阶段快路径秒匹配上一轮结果帧导致
    /// 「新样品秒出旧结果」；上一轮 WaitForFrameAsync 已消费的合法应答不受影响。</para>
    /// </summary>
    /// <param name="commState">工位通信单元</param>
    /// <param name="responseFlag">应答指令位（当前仅 0xAD 结果帧）</param>
    /// <param name="protocolStation">协议工位值（界面工位号 - 1）</param>
    /// <returns>丢弃的帧数（大于 0 时调用方记录 Warn 日志供现场分析）</returns>
    private static int PurgeStationResponseFrames(StationCommState commState, byte responseFlag, byte protocolStation)
    {
        int count = 0;
        while (commState.Buffer.TryMatchFrame(
            frame => IsStationResponseFrame(frame, responseFlag, protocolStation), out _))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// 经指定工位的独立 Channel 异步发送一帧指令（由该工位消费任务按 FIFO 写入串口）
    /// </summary>
    /// <param name="commState">工位通信单元</param>
    /// <param name="frame">完整协议指令帧</param>
    /// <param name="sendCallback">发送日志回调（可选）</param>
    /// <returns>实际发送完成任务（true=写串口成功；false=发送失败或被取消），
    /// 应答等待窗应自本任务完成后起算</returns>
    private static async Task<Task<bool>> SendViaStationChannelAsync(
        StationCommState commState,
        byte[] frame,
        Action<byte[]>? sendCallback)
    {
        var item = new StationSendItem { Frame = frame, SendCallback = sendCallback };
        await commState.SendChannel.Writer.WriteAsync(item).ConfigureAwait(false);
        return item.SentCompletion.Task;
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
