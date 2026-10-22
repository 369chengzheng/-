// =============================================================================
// 文件名：ISerialService.cs
// 功能描述：串口服务接口及相关辅助类型定义
//           定义串口状态枚举、通信方向枚举、各类事件参数，以及 ISerialService
//           接口（打开/关闭/收发/自动重连/端口监视等能力）。
// =============================================================================
using IMDP.Domain.ValueObjects;

namespace IMDP.Infrastructure.Serial;

/// <summary>
/// 串口状态枚举
/// </summary>
public enum SerialPortStatus
{
    /// <summary>已打开</summary>
    Opened,
    /// <summary>已关闭</summary>
    Closed,
    /// <summary>已断开（异常）</summary>
    Disconnected,
    /// <summary>正在重连</summary>
    Reconnecting,
    /// <summary>重连成功</summary>
    Reconnected,
    /// <summary>重连失败</summary>
    ReconnectFailed
}

/// <summary>
/// 通信方向枚举
/// </summary>
public enum CommunicationDirection
{
    /// <summary>发送方向</summary>
    Send,
    /// <summary>接收方向</summary>
    Receive
}

/// <summary>
/// 串口状态变更事件参数
/// </summary>
public class SerialPortStatusChangedEventArgs : EventArgs
{
    /// <summary>串口名称。</summary>
    public string PortName { get; set; } = string.Empty;
    /// <summary>新状态。</summary>
    public SerialPortStatus Status { get; set; }
    /// <summary>状态描述信息。</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>关联异常（如有）。</summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// 串口原始数据接收事件参数
/// </summary>
public class SerialDataReceivedEventArgs : EventArgs
{
    /// <summary>串口名称。</summary>
    public string PortName { get; set; } = string.Empty;
    /// <summary>接收到的原始字节数据。</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    /// <summary>接收时间戳。</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 通信日志事件参数
/// </summary>
public class CommunicationLogEventArgs : EventArgs
{
    /// <summary>串口名称。</summary>
    public string PortName { get; set; } = string.Empty;
    /// <summary>工站 Id。</summary>
    public int StationId { get; set; }
    /// <summary>通信方向（发送/接收）。</summary>
    public CommunicationDirection Direction { get; set; }
    /// <summary>通信数据。</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    /// <summary>通信时间戳。</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>描述信息。</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>重试次数。</summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// 串口服务接口
/// <para>提供串口打开/关闭、发送/接收、自动重连、端口监视等能力，并通过事件对外通知状态与数据。</para>
/// </summary>
public interface ISerialService : IDisposable
{
    /// <summary>
    /// 异步打开指定串口。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="config">串口配置参数。</param>
    /// <returns>打开成功返回 true；否则返回 false。</returns>
    Task<bool> OpenAsync(string portName, SerialPortConfig config);

    /// <summary>
    /// 异步关闭指定串口。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    Task CloseAsync(string portName);

    /// <summary>
    /// 发送帧并等待响应。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="frame">待发送的帧数据。</param>
    /// <param name="timeoutMs">单次等待响应超时（毫秒）。</param>
    /// <param name="retryCount">重试次数，默认 1。</param>
    /// <param name="sendDelayMs">发送前延迟（毫秒），默认 0。</param>
    /// <returns>接收到的响应数据；超时或失败返回 null。</returns>
    Task<byte[]?> SendAndWaitAsync(string portName, byte[] frame, int timeoutMs, int retryCount = 1, int sendDelayMs = 0);

    /// <summary>
    /// 发送帧并等待响应（带帧格式：PreambleGapMs&gt;0 时前导与帧体分段发送）。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="frame">待发送的帧数据。</param>
    /// <param name="format">帧格式配置（可空，空时不分段）。</param>
    /// <param name="timeoutMs">单次等待响应超时（毫秒）。</param>
    /// <param name="retryCount">重试次数，默认 1。</param>
    /// <param name="sendDelayMs">发送前延迟（毫秒），默认 0。</param>
    Task<byte[]?> SendAndWaitAsync(string portName, byte[] frame, IMDP.Domain.Products.FrameFormatConfig? format, int timeoutMs, int retryCount = 1, int sendDelayMs = 0);

    /// <summary>
    /// 异步发送数据（不等待响应）。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="data">待发送的数据。</param>
    Task SendAsync(string portName, byte[] data);

    /// <summary>
    /// 判断指定串口是否已打开。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <returns>已打开返回 true；否则返回 false。</returns>
    bool IsOpen(string portName);

    /// <summary>
    /// 获取本机可用的全部串口名称。
    /// </summary>
    /// <returns>串口名称数组。</returns>
    string[] GetAvailablePorts();

    /// <summary>启动端口监视（监听 USB 设备变更事件）。</summary>
    void StartPortWatcher();

    /// <summary>停止端口监视。</summary>
    void StopPortWatcher();

    /// <summary>
    /// 为指定串口启用自动重连。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <param name="maxRetryCount">最大重试次数，默认 5。</param>
    /// <param name="retryIntervalMs">重试间隔（毫秒），默认 2000。</param>
    void EnableAutoReconnect(string portName, int maxRetryCount = 5, int retryIntervalMs = 2000);

    /// <summary>
    /// 为指定串口禁用自动重连。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    void DisableAutoReconnect(string portName);

    /// <summary>
    /// 手动触发指定串口的重连。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <returns>重连成功返回 true；否则返回 false。</returns>
    Task<bool> ReconnectAsync(string portName);

    /// <summary>异步关闭全部已打开的串口。</summary>
    Task CloseAllAsync();

    /// <summary>串口状态变更事件。</summary>
    event EventHandler<SerialPortStatusChangedEventArgs>? PortStatusChanged;
    /// <summary>串口列表变更事件（设备插拔时触发）。</summary>
    event EventHandler<string[]>? PortListChanged;
    /// <summary>原始数据接收事件。</summary>
    event EventHandler<SerialDataReceivedEventArgs>? RawDataReceived;
    /// <summary>通信日志事件（发送/接收记录）。</summary>
    event EventHandler<CommunicationLogEventArgs>? CommunicationLog;
}
