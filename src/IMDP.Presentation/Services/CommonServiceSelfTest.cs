// =============================================================================
// 文件名：CommonServiceSelfTest.cs
// 功能描述：串口链路自测（仅 DEBUG）。
//           用例一（默认入口）：0xD0 单命令码收发 demo（同步阻塞版）
//                  前导 549×0x55 + 5×0xFE + 68 06 00 D0 3E 16（共 560 字节）
//                  同步收不到时，说明问题不在异步实现，而在接线/参数/设备状态
//                  自动扫描前导间隔 0/50/100/200ms（gapMs），任一间隔通即停
//           用例二：0x63 读参数（命令字节固定 0x5A，数据区 63 55 00）
//           无目标串口的机器自动跳过放行；结果写 NLog（前缀 [D0Demo] / [SelfTest]）。
// =============================================================================
using System.Diagnostics;
using System.IO.Ports;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Protocol;
using IMDP.Infrastructure.Serial;
using NLog;

namespace IMDP.Presentation.Services;

/// <summary>
/// 串口链路自测
/// <para>仅在 DEBUG 编译时由 App 启动后台调度执行，结果写 NLog。</para>
/// </summary>
public static class CommonServiceSelfTest
{
    private static readonly ILogger _logger = LogManager.GetLogger(typeof(CommonServiceSelfTest).FullName);

    /// <summary>demo 使用的串口名（按现场实际修改）。</summary>
    private const string DemoPort = "COM6";

    /// <summary>demo 使用的波特率。</summary>
    private const int DemoBaudRate = 9600;

    /// <summary>
    /// 启动后执行自测（DEBUG 专用入口）。
    /// </summary>
    /// <param name="delayMs">预留的启动延迟毫秒数（当前不启用延迟）。</param>
    public static async Task RunAfterStartupAsync(int delayMs = 5000)
    {
#if DEBUG
        try
        {
            await RunD0CommandDemoSyncAsync(DemoPort, DemoBaudRate, 3000);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[D0Demo] 自测异常");
        }
#endif
    }

    /// <summary>
    /// 0xD0 单命令码收发 demo：前导码 + 68 06 00 D0 3E 16。
    /// <para>流程：组帧 → 校验字节 → 开串口 → 发送 → 打印收到的原始字节 → 解帧。
    /// 先打印原始字节再看解析结果，现场排障时可以一眼分清「没收到」和「收到了但解错」。</para>
    /// </summary>
    /// <param name="portName">串口名，默认 COM6。</param>
    /// <param name="baudRate">波特率，默认 9600。</param>
    /// <param name="timeoutMs">等待响应超时（毫秒），默认 3000。</param>
    public static async Task RunD0CommandDemoAsync(
        string portName = DemoPort,
        int baudRate = DemoBaudRate,
        int timeoutMs = 3000)
    {
#if DEBUG
        // 1. 帧格式：带唤醒前导的 G2-SAGA 布局。
        //    前导 = 549×0x55 + 5×0xFE，按约定不参与长度字段与校验码计算。
        var format = ProtocolFormats.G2SagaWakeup549.Clone();
        // 单命令码帧：命令字段取 payload[0]（0xD0），且命令码不再进数据区。
        // 若沿用 0x5A 固定前缀模式，组出来会是 68 07 00 5A D0 xx 16，与本例不符。
        format.CommandIsPayload = true;

        // 2. 组帧：期望前导 554 字节 + 帧体 68 06 00 D0 3E 16（总长 560）
        var frame = FrameComposer.BuildFrame(format, 0x00, new byte[] { 0xD0 });
        var body = frame.Skip(frame.Length - 6).ToArray();
        _logger.Info($"[D0Demo] 组帧完成：总长={frame.Length} 字节，前导={PreambleSummary(frame)}，帧体={BitConverter.ToString(body)}");

        bool builtOk = frame.Length == 560
            && frame.Take(549).All(b => b == 0x55)
            && frame.Skip(549).Take(5).All(b => b == 0xFE)
            && body.SequenceEqual(new byte[] { 0x68, 0x06, 0x00, 0xD0, 0x3E, 0x16 });
        if (!builtOk)
        {
            _logger.Warn("[D0Demo] 组帧字节与预期（68 06 00 D0 3E 16）不符，用例终止");
            return;
        }

        // 3. 打开串口
        using var serial = new SerialService();
        var ports = serial.GetAvailablePorts();
        _logger.Info($"[D0Demo] 本机串口：{(ports.Length == 0 ? "(无)" : string.Join(",", ports))}");
        if (!ports.Contains(portName))
        {
            _logger.Info($"[D0Demo] 本机无 {portName}，用例跳过放行");
            return;
        }

        var opened = await serial.OpenAsync(portName, new SerialPortConfig
        {
            BaudRate = baudRate,
            DataBits = 8,
            StopBits = "One",
            Parity = "None"
        });
        if (!opened)
        {
            _logger.Warn($"[D0Demo] {portName} 打开失败（可能被占用），用例终止");
            return;
        }

        try
        {
            // 4. 发送（retryCount = 0：demo 只发一次，便于看清「一次请求一次响应」）
            _logger.Info($"[D0Demo] 发送 {frame.Length} 字节：{HexPreview(frame)}");
            var raw = await serial.SendAndWaitAsync(portName, frame, timeoutMs, retryCount: 0);

            // 5. 接收：先落原始字节
            if (raw == null || raw.Length == 0)
            {
                _logger.Warn($"[D0Demo] {timeoutMs}ms 内无响应（检查接线、波特率、工位地址、前导码是否需要）");
                return;
            }
            _logger.Info($"[D0Demo] 收到 {raw.Length} 字节：{BitConverter.ToString(raw)}");

            // 6. 解帧：FrameStreamBuffer 会自动跳过帧头前的噪声/前导，支持半帧等待
            var rx = new FrameStreamBuffer();
            rx.Append(raw);
            if (!rx.TryParseFrame(format, out var parsed))
            {
                _logger.Warn($"[D0Demo] 解帧未完成：{parsed.ErrorMessage ?? "缓冲不足一帧（可能响应分包）"}，缓冲残留={rx.BufferedByteCount} 字节");
                return;
            }

            var data = parsed.Data ?? Array.Empty<byte>();
            _logger.Info($"[D0Demo] 解析成功：工位=0x{parsed.DeviceAddress:X2} 命令=0x{parsed.CommandCode:X2} 数据区={BitConverter.ToString(data)} 帧长={parsed.ConsumedLength}");
        }
        finally
        {
            // 7. 无论成败都关串口
            await serial.CloseAsync(portName);
            _logger.Info($"[D0Demo] {portName} 已关闭");
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// 0xD0 同步收发版：直接用 SerialPort 阻塞读写，绕开异步队列/事件/TCS 全链路。
    /// <para>用途：判断「设备到底回没回」。同步阻塞读也收不到，就与异步实现无关，
    /// 问题在接线、波特率、校验位、工位地址、前导码或设备状态上。</para>
    /// </summary>
    /// <param name="portName">串口名，默认 COM6。</param>
    /// <param name="baudRate">波特率，默认 9600。</param>
    /// <param name="timeoutMs">等待首字节的超时（毫秒），默认 3000。</param>
    public static async Task RunD0CommandDemoSyncAsync(
        string portName = DemoPort,
        int baudRate = DemoBaudRate,
        int timeoutMs = 3000)
    {
#if DEBUG
        // 1. 组帧：与异步版完全一致（单命令码帧 + 549×0x55 + 5×0xFE 前导）
        var format = ProtocolFormats.G2SagaWakeup549.Clone();
        format.CommandIsPayload = true;
        var frame = FrameComposer.BuildFrame(format, 0x00, new byte[] { 0xD0 });
        var body = frame.Skip(frame.Length - 6).ToArray();
        _logger.Info($"[D0Demo-Sync] 组帧完成：总长={frame.Length} 字节，前导={PreambleSummary(frame)}，帧体={BitConverter.ToString(body)}");
        if (frame.Length != 560 || !body.SequenceEqual(new byte[] { 0x68, 0x06, 0x00, 0xD0, 0x3E, 0x16 }))
        {
            _logger.Warn("[D0Demo-Sync] 组帧字节与预期不符，用例终止");
            return;
        }

        // 2. 依次尝试多个前导间隔（gapMs=0 即旧版连续发送）
        int[] gaps = { 0, 50, 100, 200 };
        foreach (var gapMs in gaps)
        {
            _logger.Info($"[D0Demo-Sync] ===== 开始尝试前导间隔 gapMs={gapMs} =====");
            var ok = await Task.Run(() => TrySendReceiveOnce(portName, baudRate, timeoutMs, frame, format, gapMs));
            if (ok)
            {
                _logger.Info($"[D0Demo-Sync] gapMs={gapMs} 通信成功，扫描结束");
                return;
            }
            // 两轮之间稍作停顿，让设备退出上一轮的唤醒状态
            await Task.Delay(500);
        }
        _logger.Warn($"[D0Demo-Sync] 全部间隔（{string.Join("/", gaps)}ms）均未收到响应");
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// 同步收发单轮：开串口 → 前导/帧体（按 gapMs 分段或连续）→ 阻塞收 → 解帧。
    /// </summary>
    /// <returns>通信成功（收到并解出完整帧）返回 true。</returns>
    private static bool TrySendReceiveOnce(string portName, int baudRate, int timeoutMs, byte[] frame, IMDP.Domain.Products.FrameFormatConfig format, int gapMs)
    {
        using var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = timeoutMs,
            WriteTimeout = timeoutMs,
            DtrEnable = false,   // 与 SerialService 打开参数保持一致，否则对比不成立
            RtsEnable = false
        };

        try
        {
            port.Open();
        }
        catch (Exception ex)
        {
            _logger.Warn($"[D0Demo-Sync] {portName} 打开失败（可能被占用）：{ex.Message}");
            return false;
        }

        try
        {
            // 丢弃打开前的残留字节，保证下面收到的就是本次响应
            port.DiscardInBuffer();
            port.DiscardOutBuffer();

            const int preambleLen = 554;   // 549×0x55 + 5×0xFE
            var sw = Stopwatch.StartNew();
            if (gapMs > 0)
            {
                port.Write(frame, 0, preambleLen);
                Thread.Sleep(gapMs);           // 前导与帧体之间的处理间隔
                port.Write(frame, preambleLen, frame.Length - preambleLen);
            }
            else
            {
                port.Write(frame, 0, frame.Length);
            }
            _logger.Info($"[D0Demo-Sync] 已写入 {frame.Length} 字节（gapMs={gapMs}，写完耗时 {sw.ElapsedMilliseconds}ms），开始等待响应…");

            // 阻塞等首字节：收不到就是真没回包
            var received = new List<byte>(64);
            try
            {
                int first = port.ReadByte();
                received.Add((byte)first);
                _logger.Info($"[D0Demo-Sync] 首字节 0x{first:X2}，距写完 {sw.ElapsedMilliseconds}ms，当前可读 {port.BytesToRead} 字节");
            }
            catch (TimeoutException)
            {
                _logger.Warn($"[D0Demo-Sync] gapMs={gapMs}：{timeoutMs}ms 内一个字节都没收到");
                return false;
            }

            // 首字节到达后按静默间隔收齐：60ms 无新字节即认为本帧结束
            var idleSw = Stopwatch.StartNew();
            while (idleSw.ElapsedMilliseconds < 60)
            {
                if (port.BytesToRead > 0)
                {
                    var chunk = new byte[port.BytesToRead];
                    int n = port.Read(chunk, 0, chunk.Length);
                    if (n > 0)
                    {
                        received.AddRange(chunk.Take(n));
                        idleSw.Restart();
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }

            var raw = received.ToArray();
            _logger.Info($"[D0Demo-Sync] 收到 {raw.Length} 字节：{BitConverter.ToString(raw)}");

            // 解帧
            var rx = new FrameStreamBuffer();
            rx.Append(raw);
            if (!rx.TryParseFrame(format, out var parsed))
            {
                _logger.Warn($"[D0Demo-Sync] 解帧未完成：{parsed.ErrorMessage ?? "缓冲不足一帧"}，残留={rx.BufferedByteCount} 字节");
                return false;
            }

            var data = parsed.Data ?? Array.Empty<byte>();
            _logger.Info($"[D0Demo-Sync] 解析成功：工位=0x{parsed.DeviceAddress:X2} 命令=0x{parsed.CommandCode:X2} 数据区={BitConverter.ToString(data)} 帧长={parsed.ConsumedLength}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[D0Demo-Sync] 同步收发异常：{ex.Message}");
            return false;
        }
        finally
        {
            try { port.Close(); } catch { }
            _logger.Info($"[D0Demo-Sync] {portName} 已关闭");
        }
    }

    /// <summary>
    /// 前导码摘要：549 个 0x55 + 5 个 0xFE 逐字节打印会刷屏，这里只报数量。
    /// </summary>
    private static string PreambleSummary(byte[] frame)
    {
        int repeat = 0;
        while (repeat < frame.Length && frame[repeat] == 0x55)
            repeat++;
        int suffix = 0;
        while (repeat + suffix < frame.Length && frame[repeat + suffix] == 0xFE)
            suffix++;
        return $"{repeat}×0x55 + {suffix}×0xFE";
    }

    /// <summary>
    /// 十六进制预览：短帧全打印；长帧（含前导）只打首尾，避免日志被 549 字节唤醒码淹没。
    /// </summary>
    private static string HexPreview(byte[] data, int head = 16, int tailBytes = 8)
    {
        if (data.Length <= head + tailBytes)
            return BitConverter.ToString(data);
        return $"{BitConverter.ToString(data, 0, head)} ...(省略 {data.Length - head - tailBytes} 字节)... {BitConverter.ToString(data, data.Length - tailBytes, tailBytes)}";
    }

    /// <summary>
    /// 串口链路用例：开 → 组帧 → 发送 → 解析 → 关（基于 SerialSession 门面）。
    /// <para>保留作为 0x63 读参数（命令字节固定 0x5A）的参考用例，必须 await 调用。</para>
    /// </summary>
    private static async Task TestSerialLinkAsync()
    {
        const string port = "COM6";
        // 带唤醒前导的完整指令格式：549×0x55 + 5×0xFE + 帧体（命令字节固定 5A，数据区 63 55 00）
        var format = ProtocolFormats.G2SagaWakeup549;

        // 1. 组帧：期望 549×0x55 + 5×0xFE + 68 09 00 5A 63 55 00 83 16（总长 563）
        var frame = FrameComposer.BuildFrame(format, 0x00, new byte[] { 0x63, 0x55, 0x00 });
        _logger.Info($"[SelfTest] 组帧：总长{frame.Length}，帧尾9字节={BitConverter.ToString(frame, frame.Length - 9)}");
        bool ok = frame.Length == 563
            && frame.Take(549).All(b => b == 0x55)
            && frame.Skip(549).Take(5).All(b => b == 0xFE)
            && frame.Skip(frame.Length - 9).SequenceEqual(new byte[] { 0x68, 0x09, 0x00, 0x5A, 0x63, 0x55, 0x00, 0x83, 0x16 });
        if (!ok)
        {
            _logger.Warn("[SelfTest] 组帧字节不符，用例终止");
            return;
        }

        // 2. 开串口
        using var serial = new SerialService();
        if (!serial.GetAvailablePorts().Contains(port))
        {
            _logger.Info($"[SelfTest] 本机无 {port}，用例跳过放行");
            return;
        }

        var session = new SerialSession(serial, port, format)
        {
            // 命令注册表：按码登记组负载与解响应（此处示范 0x63 读参数）
            Catalog = new CommandCatalog()
        };
        session.Catalog.Register(new CommandDefinition
        {
            Code = 0x63,
            Name = "读参数",
            BuildPayload = () => new byte[] { 0x63, 0x55, 0x00 }
        });
        if (!await session.OpenAsync())
        {
            _logger.Warn($"[SelfTest] {port} 打开失败（可能被占用），用例终止");
            return;
        }

        try
        {
            // 3. 按命令码发送并解析（查注册表组帧，3 秒超时 1 次重试）
            var result = await session.ExecuteCommandAsync(0x63, 3000, 1);

            // 4. 解析结果
            if (result == null)
            {
                _logger.Warn($"[SelfTest] 无有效响应：{session.LastError}");
                return;
            }
            var parsed = result.Value.Frame;
            var data = parsed.Data ?? Array.Empty<byte>();
            _logger.Info($"[SelfTest] 解析成功：工位=0x{parsed.DeviceAddress:X2} 命令=0x{parsed.CommandCode:X2} 数据区={BitConverter.ToString(data)} 帧长={parsed.ConsumedLength}");
        }
        finally
        {
            // 5. 关串口（无论成败都关）
            await session.CloseAsync();
            _logger.Info($"[SelfTest] {port} 已关闭");
        }
    }
}
