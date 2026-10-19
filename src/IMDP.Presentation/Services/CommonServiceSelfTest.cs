// =============================================================================
// 文件名：CommonServiceSelfTest.cs
// 功能描述：串口链路自测（仅 DEBUG）。一条用例走完：
//           开串口 → 组帧(68 09 00 5A 63 55 00 83 16) → 发送 → 解析 → 关串口。
//           无 COM6 的机器自动跳过放行。
// =============================================================================
using IMDP.Infrastructure.Protocol;
using IMDP.Infrastructure.Serial;
using NLog;

namespace IMDP.Presentation.Services;

/// <summary>
/// 串口链路自测
/// <para>仅在 DEBUG 编译时由 App 启动后台调度执行，结果写 NLog（前缀 [SelfTest]）。</para>
/// </summary>
public static class CommonServiceSelfTest
{
    private static readonly ILogger _logger = LogManager.GetLogger(typeof(CommonServiceSelfTest).FullName);

    /// <summary>
    /// 启动后延迟执行自测（DEBUG 专用入口）。
    /// </summary>
    /// <param name="delayMs">延迟毫秒数，默认 5000，避开启动高峰。</param>
    public static async Task RunAfterStartupAsync(int delayMs = 5000)
    {
#if DEBUG
        //await Task.Delay(delayMs);
        try
        {
            await TestSerialLinkAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[SelfTest] 自测异常");
        }
#endif
    }

    /// <summary>
    /// 串口链路用例：开 → 组帧 → 发送 → 解析 → 关（基于 SerialSession 门面）。
    /// <para>必须 await 调用，禁止同步阻塞（UI 线程死锁）。</para>
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
