using CommunityToolkit.Mvvm.ComponentModel;

namespace IMDP.Products.GasDG.AlarmHost.Models;

/// <summary>
/// 一个工位的扫码输入状态，与设备读取的参数结果分开保存
/// </summary>
public sealed class AlarmHostStation(int number) : ObservableObject
{
    /// <summary>
    /// 扫码框当前输入，回车确认前可继续编辑
    /// </summary>
    private string _scanCode = "";

    /// <summary>
    /// 当前输入是否已经回车确认
    /// </summary>
    private bool _isScanned;

    /// <summary>
    /// 当前工位选择的串口，不预设或伪造端口
    /// </summary>
    private string _portName = "";

    /// <summary>
    /// 默认使用9600，需要时可在页面切换到4800
    /// </summary>
    private int _baudRate = 9600;

    /// <summary>
    /// 当前设备执行反馈，与扫码确认状态分开
    /// </summary>
    private string _executionStatus = "";

    /// <summary>
    /// 本工位是否正在执行，不阻塞其他工位扫码
    /// </summary>
    private bool _isRunning;

    /// <summary>
    /// 本次设备项目的整体判定，尚未执行时为空，不代表MES上传成功
    /// </summary>
    private bool? _isPass;

    /// <summary>
    /// 本次工位执行的本地开始时间，结束后清空并保留最终耗时
    /// </summary>
    private DateTime? _startedAt;

    /// <summary>
    /// 最近一次刷新或结束时的耗时
    /// </summary>
    private TimeSpan _elapsed;

    /// <summary>
    /// 页面显式连接期间锁定串口参数，关闭后允许重新选择
    /// </summary>
    private bool _isConnected;

    /// <summary>
    /// 连接期间保留当前端口和参数，串口断开后可重新配置
    /// </summary>
    public bool IsConnected
    {
        get => _isConnected;
        internal set
        {
            if (!SetProperty(ref _isConnected, value)) return;
            OnPropertyChanged(nameof(CanEditPort));
            OnPropertyChanged(nameof(CanScan));
        }
    }

    /// <summary>
    /// 扫码与端口选择使用不同锁定范围
    /// </summary>
    public bool CanEditPort => CanEdit && !IsConnected;

    /// <summary>
    /// 串口已打开且本工位空闲时才接受扫码
    /// </summary>
    public bool CanScan => CanEdit && IsConnected;

    /// <summary>
    /// 本工位设备操作实际耗时，界面运行中每秒刷新，结束后保持
    /// </summary>
    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set
        {
            if (!SetProperty(ref _elapsed, value)) return;
            OnPropertyChanged(nameof(ElapsedText));
            OnPropertyChanged(nameof(DisplayStatus));
        }
    }

    /// <summary>
    /// 总分钟和秒，不在一小时后回绕
    /// </summary>
    public string ElapsedText => $"{(int)Elapsed.TotalMinutes:D2}:{Elapsed.Seconds:D2}";

    /// <summary>
    /// 工位状态行共用显示，包含耗时及当前操作状态
    /// </summary>
    public string DisplayStatus => $"{ElapsedText} {StatusText}";

    /// <summary>
    /// 当前工位执行期间锁定本工位输入
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        internal set
        {
            if (!SetProperty(ref _isRunning, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanEditPort));
            OnPropertyChanged(nameof(CanScan));
        }
    }

    /// <summary>
    /// 仅本工位执行中禁止修改串口、波特率及条码
    /// </summary>
    public bool CanEdit => !IsRunning;

    /// <summary>
    /// 设备项目整体是否通过，与扫码状态分开
    /// </summary>
    public bool? IsPass
    {
        get => _isPass;
        internal set => SetProperty(ref _isPass, value);
    }

    /// <summary>
    /// 方案内连续工位号，从一开始
    /// </summary>
    public int Number { get; } = number;

    /// <summary>
    /// 当前扫码编号；内容改变后恢复待扫码状态，不写入设备读取结果
    /// </summary>
    public string ScanCode
    {
        get => _scanCode;
        set
        {
            if (!SetProperty(ref _scanCode, value)) return;
            IsScanned = false;
            IsPass = null;
            _startedAt = null;
            Elapsed = TimeSpan.Zero;
            SetExecutionStatus("");
        }
    }

    /// <summary>
    /// 是否已确认扫码，仅表示输入完成，不表示设备检测成功
    /// </summary>
    public bool IsScanned
    {
        get => _isScanned;
        private set
        {
            if (!SetProperty(ref _isScanned, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(DisplayStatus));
        }
    }

    /// <summary>
    /// 优先显示执行反馈，没有执行时显示扫码状态
    /// </summary>
    public string StatusText => _executionStatus.Length > 0 ? _executionStatus : IsScanned ? "已扫码" : "待扫码";

    /// <summary>
    /// 该工位独立选择的串口
    /// </summary>
    public string PortName
    {
        get => _portName;
        set => SetProperty(ref _portName, value);
    }

    /// <summary>
    /// 该工位本次连接的波特率，不写入方案数据库
    /// </summary>
    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    /// <summary>
    /// 由执行页更新当前工位的操作反馈
    /// </summary>
    internal void SetExecutionStatus(string value)
    {
        if (!SetProperty(ref _executionStatus, value)) return;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DisplayStatus));
    }

    /// <summary>
    /// 开始一轮工位计时，重试也从零开始
    /// </summary>
    internal void StartTiming()
    {
        _startedAt = DateTime.Now;
        Elapsed = TimeSpan.Zero;
    }

    /// <summary>
    /// 刷新运行中的耗时，系统时钟回调时最低显示零
    /// </summary>
    internal void RefreshElapsed()
    {
        if (_startedAt is not { } started) return;
        var elapsed = DateTime.Now - started;
        Elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    /// <summary>
    /// 成功、失败、中止都停止计时并保留最终值
    /// </summary>
    internal void StopTiming()
    {
        RefreshElapsed();
        _startedAt = null;
    }

    /// <summary>
    /// 回车确认非空编号，去除首尾空白；具体条码格式随业务接入再确定
    /// </summary>
    internal bool ConfirmScan()
    {
        if (string.IsNullOrWhiteSpace(ScanCode)) return false;
        ScanCode = ScanCode.Trim();
        IsPass = null;
        _startedAt = null;
        Elapsed = TimeSpan.Zero;
        SetExecutionStatus("");
        IsScanned = true;
        return true;
    }
}
