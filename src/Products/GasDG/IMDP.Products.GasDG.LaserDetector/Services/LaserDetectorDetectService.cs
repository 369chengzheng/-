// =============================================================================
// 文件名：LaserDetectorDetectService.cs
// 功能描述：家用可燃气体激光检测仪检测服务。
//           组合 ISerialService 与 LaserDetectorProtocol，封装单工位完整
//           检测流程：下发开始测试指令 → 轮询查询测试结果 → 解析判定。
// =============================================================================
using IMDP.Domain.Interfaces;
using IMDP.Infrastructure.Serial;
using IMDP.Products.GasDG.LaserDetector.Models;
using IMDP.Products.GasDG.LaserDetector.Protocol;

namespace IMDP.Products.GasDG.LaserDetector.Services;

/// <summary>
/// 激光检测仪检测服务
/// <para>封装单工位自动检测的完整串口交互：开始测试（0xAA）→ 轮询查询结果（0xAC）
/// → 解析 0xAD 结果帧并按配置判定合格性。</para>
/// </summary>
public class LaserDetectorDetectService
{
    /// <summary>串口服务（单例，由容器注入）</summary>
    private readonly ISerialService _serialService;

    /// <summary>全局配置服务（获取串口通讯参数）</summary>
    private readonly IGlobalConfigService _globalConfigService;

    /// <summary>单工位查询结果的最大轮询次数</summary>
    private const int MaxQueryAttempts = 20;

    /// <summary>两次查询结果之间的间隔（毫秒）</summary>
    private const int QueryIntervalMs = 500;

    /// <summary>串口单次收发超时（毫秒）</summary>
    private const int SerialTimeoutMs = 1500;

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

    /// <summary>
    /// 打开指定串口（串口参数取自全局配置）
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <returns>打开成功返回 true</returns>
    public async Task<bool> OpenPortAsync(string portName)
    {
        var config = await _globalConfigService.GetSerialPortConfigAsync();
        return await _serialService.OpenAsync(portName, config);
    }

    /// <summary>
    /// 关闭指定串口
    /// </summary>
    /// <param name="portName">串口名称</param>
    public async Task ClosePortAsync(string portName)
    {
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

    /// <summary>
    /// 执行单工位完整自动检测流程
    /// <para>流程：下发开始测试指令并确认工装应答 → 轮询查询结果直到收到
    /// 0xAD 结果帧 → 按当前配置判定各项是否合格。</para>
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <param name="station">界面工位号 1～3</param>
    /// <param name="config">当前产品的激光检测配置</param>
    /// <returns>检测成功且解析通过时返回结果对象；超时或失败时返回 null</returns>
    public async Task<LaserDetectorTestResult?> DetectAsync(string portName, int station, LaserDetectorConfig config)
    {
        // 第一步：下发开始测试指令（0xAA），等待工装 0xAB 应答确认已受理
        var startCommand = LaserDetectorProtocol.CreateStartTestCommand(station, config);
        var startResponse = await _serialService.SendAndWaitAsync(portName, startCommand, SerialTimeoutMs, 1);
        if (startResponse == null ||
            !LaserDetectorProtocol.TryParseGetFullResponse(startResponse, out byte[] startAck) ||
            startAck[1] != LaserDetectorProtocolConstants.StartTestResponseFlag)
        {
            return null;
        }

        // 第二步：轮询查询测试结果（0xAC），直到收到 0xAD 结果帧或超出最大轮询次数
        var queryCommand = LaserDetectorProtocol.CreateQueryTestResultCommand(station);
        for (int attempt = 0; attempt < MaxQueryAttempts; attempt++)
        {
            await Task.Delay(QueryIntervalMs);
            var queryResponse = await _serialService.SendAndWaitAsync(portName, queryCommand, SerialTimeoutMs, 1);
            if (queryResponse == null)
            {
                continue;
            }

            if (LaserDetectorProtocol.TryParseTestResultResponse(queryResponse, config, out var result))
            {
                return result;
            }
        }

        return null;
    }
}
