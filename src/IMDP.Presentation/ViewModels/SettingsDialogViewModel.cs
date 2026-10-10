// =============================================================================
// 文件名：SettingsDialogViewModel.cs
// 功能描述：设置对话框视图模型。
//           负责串口参数（波特率/数据位/停止位/校验位）与协议命令列表的加载、编辑和保存，
//           同时定义协议命令显示模型 ProtocolCommandDisplay。
// =============================================================================
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IMDP.Application.Services;
using IMDP.Domain.Interfaces;
using IMDP.Domain.ValueObjects;

namespace IMDP.Presentation.ViewModels;

/// <summary>
/// 设置对话框视图模型
/// <para>加载并编辑全局串口参数与协议命令列表；保存时通过 IGlobalConfigService
/// 持久化到配置文件。协议命令支持新增、上移、下移、删除操作，并维护执行顺序（Order）。</para>
/// </summary>
public partial class SettingsDialogViewModel : ObservableObject
{
    private readonly IGlobalConfigService _configService;

    // 串口参数可选项
    public int[] BaudRateOptions { get; } = { 9600, 19200, 38400, 57600, 115200 };
    public int[] DataBitsOptions { get; } = { 5, 6, 7, 8 };
    public string[] StopBitsOptions { get; } = { "One", "OnePointFive", "Two" };
    public string[] ParityOptions { get; } = { "None", "Odd", "Even", "Mark", "Space" };

    // 命令类型可选项（命令码-名称）
    public string[] CommandTypeOptions { get; } =
    {
        "01-写入参数",
        "02-读取参数",
        "03-读取版本",
        "04-固件升级",
        "05-复位",
        "06-读取状态"
    };

    /// <summary>串口波特率</summary>
    [ObservableProperty]
    private int _baudRate = 9600;

    /// <summary>串口数据位</summary>
    [ObservableProperty]
    private int _dataBits = 8;

    /// <summary>串口停止位（One/OnePointFive/Two）</summary>
    [ObservableProperty]
    private string _stopBits = "One";

    /// <summary>串口校验位（None/Odd/Even/Mark/Space）</summary>
    [ObservableProperty]
    private string _parity = "None";

    /// <summary>协议命令列表（按 Order 排序）</summary>
    [ObservableProperty]
    private ObservableCollection<ProtocolCommandDisplay> _commands = new();

    /// <summary>当前选中的协议命令</summary>
    [ObservableProperty]
    private ProtocolCommandDisplay? _selectedCommand;

    /// <summary>新增协议命令命令</summary>
    public IRelayCommand AddCommandCommand { get; }
    /// <summary>上移协议命令命令（同时交换 Order）</summary>
    public IRelayCommand<ProtocolCommandDisplay> MoveUpCommand { get; }
    /// <summary>下移协议命令命令（同时交换 Order）</summary>
    public IRelayCommand<ProtocolCommandDisplay> MoveDownCommand { get; }
    /// <summary>删除协议命令命令（删除后重新编号）</summary>
    public IRelayCommand<ProtocolCommandDisplay> DeleteCommandCommand { get; }
    /// <summary>保存命令（持久化串口参数与协议命令配置）</summary>
    public IRelayCommand SaveCommand { get; }
    /// <summary>取消命令（关闭对话框不保存）</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configService">全局配置服务（提供串口与协议配置的读写）</param>
    public SettingsDialogViewModel(IGlobalConfigService configService)
    {
        _configService = configService;

        AddCommandCommand = new RelayCommand(OnAddCommand);
        MoveUpCommand = new RelayCommand<ProtocolCommandDisplay>(OnMoveUp);
        MoveDownCommand = new RelayCommand<ProtocolCommandDisplay>(OnMoveDown);
        DeleteCommandCommand = new RelayCommand<ProtocolCommandDisplay>(OnDeleteCommand);
        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(OnCancel);

        // 构造时即触发异步加载配置
        _ = LoadAsync();
    }

    /// <summary>
    /// 异步加载串口参数与协议命令配置
    /// </summary>
    private async Task LoadAsync()
    {
        var serialConfig = await _configService.GetSerialPortConfigAsync();
        BaudRate = serialConfig.BaudRate;
        DataBits = serialConfig.DataBits;
        StopBits = serialConfig.StopBits;
        Parity = serialConfig.Parity;

        var protocolConfig = await _configService.GetProtocolConfigAsync();
        Commands = new ObservableCollection<ProtocolCommandDisplay>(
            protocolConfig.Commands.Select(c => new ProtocolCommandDisplay
            {
                CommandType = c.CommandType,
                TimeoutMs = c.TimeoutMs,
                RetryCount = c.RetryCount,
                SendDelayMs = c.SendDelayMs,
                Order = c.Order
            }).OrderBy(c => c.Order));
    }

    /// <summary>
    /// 新增协议命令处理
    /// <para>在列表末尾追加一条默认命令（Order 取当前最大值 + 1）。</para>
    /// </summary>
    private void OnAddCommand()
    {
        var nextOrder = Commands.Count > 0 ? Commands.Max(c => c.Order) + 1 : 1;
        Commands.Add(new ProtocolCommandDisplay
        {
            CommandType = 1,
            TimeoutMs = 3000,
            RetryCount = 1,
            SendDelayMs = 0,
            Order = nextOrder
        });
    }

    /// <summary>
    /// 上移协议命令处理
    /// <para>与前一项交换 Order 并在集合中上移一位。</para>
    /// </summary>
    /// <param name="cmd">待上移的命令</param>
    private void OnMoveUp(ProtocolCommandDisplay? cmd)
    {
        if (cmd == null) return;
        var idx = Commands.IndexOf(cmd);
        if (idx <= 0) return;
        SwapOrder(cmd, Commands[idx - 1]);
        Commands.Move(idx, idx - 1);
    }

    /// <summary>
    /// 下移协议命令处理
    /// <para>与后一项交换 Order 并在集合中下移一位。</para>
    /// </summary>
    /// <param name="cmd">待下移的命令</param>
    private void OnMoveDown(ProtocolCommandDisplay? cmd)
    {
        if (cmd == null) return;
        var idx = Commands.IndexOf(cmd);
        if (idx < 0 || idx >= Commands.Count - 1) return;
        SwapOrder(cmd, Commands[idx + 1]);
        Commands.Move(idx, idx + 1);
    }

    /// <summary>
    /// 交换两条命令的 Order 值
    /// </summary>
    /// <param name="a">命令 A</param>
    /// <param name="b">命令 B</param>
    private void SwapOrder(ProtocolCommandDisplay a, ProtocolCommandDisplay b)
    {
        (a.Order, b.Order) = (b.Order, a.Order);
    }

    /// <summary>
    /// 删除协议命令处理
    /// <para>删除指定命令后，对剩余命令按位置重新编号 Order。</para>
    /// </summary>
    /// <param name="cmd">待删除的命令</param>
    private void OnDeleteCommand(ProtocolCommandDisplay? cmd)
    {
        if (cmd == null) return;
        Commands.Remove(cmd);
        // 重新编号
        for (int i = 0; i < Commands.Count; i++)
        {
            Commands[i].Order = i + 1;
        }
    }

    /// <summary>
    /// 保存处理
    /// <para>将界面编辑结果构造为 SerialPortConfig 与 ProtocolConfig，
    /// 调用配置服务持久化后关闭对话框。</para>
    /// </summary>
    private async void OnSave()
    {
        var serialConfig = new SerialPortConfig
        {
            BaudRate = BaudRate,
            DataBits = DataBits,
            StopBits = StopBits,
            Parity = Parity
        };
        await _configService.SaveSerialPortConfigAsync(serialConfig);

        var protocolConfig = new ProtocolConfig
        {
            Commands = Commands.Select(c => new ProtocolCommand
            {
                CommandType = c.CommandType,
                TimeoutMs = c.TimeoutMs,
                RetryCount = c.RetryCount,
                SendDelayMs = c.SendDelayMs,
                Order = c.Order
            }).ToList()
        };
        await _configService.SaveProtocolConfigAsync(protocolConfig);

        CloseDialog();
    }

    /// <summary>
    /// 取消处理：直接关闭对话框
    /// </summary>
    private void OnCancel()
    {
        CloseDialog();
    }

    /// <summary>
    /// 关闭设置对话框（在当前应用窗口集合中查找 SettingsDialogView 并关闭）
    /// </summary>
    private void CloseDialog()
    {
        foreach (Window w in System.Windows.Application.Current.Windows)
        {
            if (w is Views.SettingsDialogView)
            {
                w.Close();
                break;
            }
        }
    }
}

/// <summary>
/// 协议命令显示模型，扩展了命令类型的显示字符串
/// <para>在 ProtocolCommand 基础上增加 CommandTypeDisplay 属性，
/// 以 "01-写入参数" 格式呈现命令类型，便于下拉框选择。</para>
/// </summary>
public partial class ProtocolCommandDisplay : ObservableObject
{
    /// <summary>命令类型编码（1=写入参数, 2=读取参数, 3=读取版本, 4=固件升级, 5=复位, 6=读取状态）</summary>
    [ObservableProperty]
    private int _commandType;

    /// <summary>超时时间（毫秒）</summary>
    [ObservableProperty]
    private int _timeoutMs = 3000;

    /// <summary>重试次数</summary>
    [ObservableProperty]
    private int _retryCount = 1;

    /// <summary>发送延迟（毫秒）</summary>
    [ObservableProperty]
    private int _sendDelayMs;

    /// <summary>执行顺序（从 1 开始）</summary>
    [ObservableProperty]
    private int _order;

    /// <summary>
    /// 命令类型显示字符串（"01-写入参数" 格式）
    /// <para>getter 由 CommandType 格式化得到；setter 解析 "01-写入参数" 格式
    /// 并回写 CommandType。</para>
    /// </summary>
    public string CommandTypeDisplay
    {
        get => $"{CommandType:D2}-{GetCommandName(CommandType)}";
        set
        {
            // 解析 "01-写入参数" 格式
            if (!string.IsNullOrEmpty(value) && value.Length >= 2 && int.TryParse(value.Substring(0, 2), out var code))
            {
                CommandType = code;
                OnPropertyChanged(nameof(CommandTypeDisplay));
            }
        }
    }

    /// <summary>
    /// 根据命令类型编码获取中文名称
    /// </summary>
    /// <param name="code">命令类型编码</param>
    /// <returns>中文名称；未知编码返回"未知"</returns>
    private static string GetCommandName(int code) => code switch
    {
        1 => "写入参数",
        2 => "读取参数",
        3 => "读取版本",
        4 => "固件升级",
        5 => "复位",
        6 => "读取状态",
        _ => "未知"
    };
}