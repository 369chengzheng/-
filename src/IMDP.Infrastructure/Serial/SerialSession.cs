// =============================================================================
// 文件名：SerialSession.cs
// 功能描述：串口会话门面。
//           把「打开串口 → 组帧 → 发送 → 接收 → 解帧 → 关闭」六步缝合成一次调用，
//           上层（产品协议/自测用例）不再各自拼装 SerialService + FrameComposer。
//           对应 AutoFrock 各模块手写的 InitPort/WriteToPort/OnCommRxChar/ClosePort
//           流程与 IMDS 各 SerialPortManager 的样板代码。
// =============================================================================
using IMDP.Domain.Products;
using IMDP.Domain.ValueObjects;
using IMDP.Infrastructure.Protocol;

namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口会话门面
/// <para>一个实例绑定一个串口与一种帧格式；ExecuteAsync 组帧并发送，
/// 内部用 <see cref="FrameStreamBuffer"/> 流式解帧，返回首个完整且校验通过的响应帧。</para>
/// </summary>
public sealed class SerialSession
{
    private readonly ISerialService _serial;
    private readonly string _portName;
    private readonly FrameFormatConfig _format;
    private readonly FrameStreamBuffer _rxBuffer = new();
    /// <summary>当前生效帧格式（默认 _format；ExecuteCommandAsync 按命令临时覆盖）。</summary>
    private FrameFormatConfig _formatOverrideCurrent;

    /// <summary>会话绑定的串口名。</summary>
    public string PortName => _portName;
    /// <summary>会话使用的帧格式。</summary>
    public FrameFormatConfig Format => _format;
    /// <summary>串口当前是否打开。</summary>
    public bool IsOpen => _serial.IsOpen(_portName);

    /// <summary>
    /// 构造串口会话（不打开串口，打开动作在 <see cref="OpenAsync"/> / <see cref="ExecuteAsync"/> 中）。
    /// </summary>
    /// <param name="serial">串口服务（建议为 DI 单例）。</param>
    /// <param name="portName">串口名，如 COM6。</param>
    /// <param name="format">帧格式配置（可直接取 ProtocolFormats 命名常量）。</param>
    public SerialSession(ISerialService serial, string portName, FrameFormatConfig format)
    {
        _serial = serial;
        _portName = portName;
        _format = format;
    }

    /// <summary>
    /// 打开串口（已打开则直接成功）。
    /// </summary>
    /// <param name="baudRate">波特率，默认 9600。</param>
    /// <param name="dataBits">数据位，默认 8。</param>
    /// <param name="stopBits">停止位（None/One/OnePointFive/Two），默认 One。</param>
    /// <param name="parity">校验位（None/Odd/Even/Mark/Space），默认 None。</param>
    public Task<bool> OpenAsync(int baudRate = 9600, int dataBits = 8, string stopBits = "One", string parity = "None")
        => _serial.OpenAsync(_portName, new SerialPortConfig
        {
            BaudRate = baudRate,
            DataBits = dataBits,
            StopBits = stopBits,
            Parity = parity
        });

    /// <summary>关闭串口（未打开则空操作）。</summary>
    public Task CloseAsync() => _serial.CloseAsync(_portName);

    /// <summary>
    /// 执行一次「组帧 → 发送 → 等待 → 解帧」事务（不含开/关串口，适合连续多命令复用连接）。
    /// </summary>
    /// <param name="deviceAddress">工位/设备地址字节。</param>
    /// <param name="payload">命令负载（CommandFieldValue 固定前缀模式下为数据区内容）。</param>
    /// <param name="timeoutMs">单次等待响应超时（毫秒）。</param>
    /// <param name="retryCount">重试次数，默认 0。</param>
    /// <returns>成功解析的响应帧；无响应/校验失败返回 null（失败详情在 ErrorMessage）。</returns>
    public async Task<FrameParseResult?> ExecuteAsync(byte deviceAddress, byte[] payload, int timeoutMs = 3000, int retryCount = 0)
    {
        LastError = null;
        _rxBuffer.Clear();   // 清掉上一次的残留，避免污染本次响应解析
        var effective = _formatOverrideCurrent ?? _format;
        var frame = FrameComposer.BuildFrame(effective, deviceAddress, payload);
        var raw = await _serial.SendAndWaitAsync(_portName, frame, effective, timeoutMs, retryCount, 0);
        if (raw == null || raw.Length == 0)
        {
            LastError = "无响应（超时或串口未开）";
            return null;
        }

        // 响应可能带残留噪声，交给流式缓冲定位帧头
        _rxBuffer.Append(raw);
        while (_rxBuffer.TryParseFrame(effective, out var parsed))
        {
            if (parsed.IsComplete)
                return parsed;               // 首个完整且校验通过的帧
            // 校验错误帧：记录后继续抽下一帧（重同步）
            LastError = parsed.ErrorMessage ?? "校验失败";
        }
        if (string.IsNullOrEmpty(LastError))
            LastError = "缓冲不足一帧（半帧）";
        return null;
    }

    /// <summary>命令注册表（可空；通过 ExecuteCommandAsync 按码分发）。</summary>
    public CommandCatalog? Catalog { get; set; }

    /// <summary>
    /// 按命令码执行指令：查注册表 → 组负载 → 发送 → 解帧 → 调命令的 Parse 回调。
    /// </summary>
    /// <param name="code">命令码（如 0x63 读参数、0xD0 进检定）。</param>
    /// <param name="timeoutMs">单次等待响应超时（毫秒），默认 3000。</param>
    /// <param name="retryCount">重试次数，默认 0。</param>
    /// <returns>成功时为解析结果+业务值；命令未注册/无响应/解析失败返回 null。</returns>
    public async Task<(FrameParseResult Frame, object? Value)?> ExecuteCommandAsync(byte code, int timeoutMs = 3000, int retryCount = 0)
    {
        var cmd = Catalog?.Find(code);
        if (cmd == null)
        {
            LastError = $"命令 0x{code:X2} 未注册";
            return null;
        }

        // 命令专属格式变体优先（如 0xD0 单命令码帧），否则用会话默认格式
        var format = cmd.FormatOverride ?? _format;
        var prevFormat = _formatOverrideCurrent;
        _formatOverrideCurrent = format;
        try
        {
            var frame = await ExecuteAsync(0x00, cmd.Build(), timeoutMs, retryCount);
            if (frame == null) return null;
            return (frame, cmd.Parse?.Invoke(frame));
        }
        finally
        {
            _formatOverrideCurrent = prevFormat;
        }
    }

    /// <summary>最近一次失败原因（诊断用）。</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// 一次性事务：打开 → 执行 → 关闭（适合单命令探测场景，如自测用例）。
    /// </summary>
    /// <param name="deviceAddress">工位/设备地址字节。</param>
    /// <param name="payload">命令负载。</param>
    /// <param name="baudRate">波特率，默认 9600。</param>
    /// <param name="timeoutMs">单次等待响应超时（毫秒），默认 3000。</param>
    /// <param name="retryCount">重试次数，默认 0。</param>
    /// <returns>成功解析的响应帧；打开失败/无响应/解析失败返回 null。</returns>
    public async Task<FrameParseResult?> ExecuteOnceAsync(byte deviceAddress, byte[] payload, int baudRate = 9600, int timeoutMs = 3000, int retryCount = 0)
    {
        LastError = null;
        if (!await OpenAsync(baudRate))
        {
            LastError = $"打开 {_portName} 失败（可能被占用）";
            return null;
        }
        try
        {
            return await ExecuteAsync(deviceAddress, payload, timeoutMs, retryCount);
        }
        finally
        {
            await CloseAsync();
        }
    }
}
