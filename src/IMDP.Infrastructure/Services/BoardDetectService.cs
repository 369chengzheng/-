// =============================================================================
// 文件名：BoardDetectService.cs
// 功能描述：板卡检测服务实现
//           封装串口与全局配置服务，提供板卡检测时的端口打开/关闭、
//           可用端口查询等能力，作为应用层与基础设施层串口能力的桥梁。
// =============================================================================
using IMDP.Domain.Interfaces;
using IMDP.Infrastructure.Serial;
using IMDP.Domain.ValueObjects;

namespace IMDP.Infrastructure.Services;

/// <summary>
/// 板卡检测服务实现
/// <para>实现 <see cref="IBoardDetectService"/>，组合 <see cref="ISerialService"/> 与
/// <see cref="IGlobalConfigService"/>，提供板卡检测流程所需的串口管理能力。</para>
/// </summary>
public class BoardDetectService : IBoardDetectService
{
    private readonly ISerialService _serialService;
    private readonly IGlobalConfigService _configService;

    /// <summary>
    /// 初始化 <see cref="BoardDetectService"/> 类的新实例。
    /// </summary>
    /// <param name="serialService">串口服务。</param>
    /// <param name="configService">全局配置服务，用于获取串口配置。</param>
    public BoardDetectService(ISerialService serialService, IGlobalConfigService configService)
    {
        _serialService = serialService;
        _configService = configService;
    }

    /// <summary>
    /// 检测指定工站与产品序列号对应的板卡。
    /// <para>若串口未打开则先打开串口，打开成功后等待 100ms 稳定后返回。</para>
    /// </summary>
    /// <param name="stationId">工站 Id。</param>
    /// <param name="productSn">产品序列号。</param>
    /// <param name="portName">串口名称。</param>
    /// <returns>检测就绪返回 true；串口打开失败返回 false。</returns>
    public async Task<bool> DetectBoardAsync(int stationId, string productSn, string portName)
    {
        // 串口未打开时尝试打开
        if (!IsPortOpen(portName))
        {
            if (!await OpenPortAsync(portName))
                return false;
        }

        // 等待 100ms 让串口稳定
        await Task.Delay(100);
        return true;
    }

    /// <summary>
    /// 异步打开指定串口（从全局配置获取串口参数）。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <returns>打开成功返回 true；否则返回 false。</returns>
    public async Task<bool> OpenPortAsync(string portName)
    {
        var config = await _configService.GetSerialPortConfigAsync();
        return await _serialService.OpenAsync(portName, config);
    }

    /// <summary>
    /// 异步关闭指定串口。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    public async Task ClosePortAsync(string portName)
    {
        await _serialService.CloseAsync(portName);
    }

    /// <summary>
    /// 异步关闭全部已打开的串口。
    /// </summary>
    public async Task CloseAllPortsAsync()
    {
        await _serialService.CloseAllAsync();
    }

    /// <summary>
    /// 获取本机可用的全部串口名称。
    /// </summary>
    /// <returns>串口名称只读列表。</returns>
    public IReadOnlyList<string> GetAvailablePorts()
    {
        return _serialService.GetAvailablePorts().ToList().AsReadOnly();
    }

    /// <summary>
    /// 判断指定串口是否已打开。
    /// </summary>
    /// <param name="portName">串口名称。</param>
    /// <returns>已打开返回 true；否则返回 false。</returns>
    public bool IsPortOpen(string portName)
    {
        return _serialService.IsOpen(portName);
    }
}
