// =============================================================================
// 文件名：IStationService.cs
// 功能描述：工位管理服务接口
//           维护检测工位的内存状态集合（新增、删除、串口绑定、产品 SN 绑定）及工位计时器。
//           实现类为 StationService，工位数据不持久化，应用重启后清空。
// =============================================================================
using IMDP.Application.DTOs;

namespace IMDP.Application.Services;

/// <summary>
/// 工位管理服务接口
/// <para>维护检测工位的内存状态集合（新增、删除、串口绑定、产品 SN 绑定）及工位计时器。</para>
/// <para>实现类为 <see cref="StationService"/>，工位数据不持久化，应用重启后清空。</para>
/// </summary>
public interface IStationService
{
    /// <summary>获取所有工位的只读列表</summary>
    /// <returns>工位 DTO 只读列表</returns>
    IReadOnlyList<StationDto> GetStations();

    /// <summary>获取指定工位</summary>
    /// <param name="stationId">工位 Id</param>
    /// <returns>工位 DTO</returns>
    /// <exception cref="ArgumentException">指定工位不存在时抛出</exception>
    StationDto GetStation(int stationId);

    /// <summary>新增工位并绑定串口</summary>
    /// <param name="portName">串口名称（如 COM3）</param>
    void AddStation(string portName);

    /// <summary>移除指定工位（同时移除其计时器）</summary>
    /// <param name="stationId">工位 Id</param>
    void RemoveStation(int stationId);

    /// <summary>设置工位绑定的串口</summary>
    /// <param name="stationId">工位 Id</param>
    /// <param name="portName">串口名称</param>
    void SetStationPort(int stationId, string portName);

    /// <summary>设置工位当前检测的产品序列号</summary>
    /// <param name="stationId">工位 Id</param>
    /// <param name="productSn">产品序列号</param>
    void SetStationProductSn(int stationId, string productSn);

    /// <summary>启动工位计时器并将状态置为 Testing</summary>
    /// <param name="stationId">工位 Id</param>
    void StartTimer(int stationId);

    /// <summary>停止工位计时器并将状态置为 Completed</summary>
    /// <param name="stationId">工位 Id</param>
    void StopTimer(int stationId);

    /// <summary>获取工位计时器已用时长</summary>
    /// <param name="stationId">工位 Id</param>
    /// <returns>已用时长；工位不存在时返回 <see cref="TimeSpan.Zero"/></returns>
    TimeSpan GetElapsed(int stationId);

    /// <summary>重置工位（清空产品 SN、参数值，状态置 Idle，计时器归零）</summary>
    /// <param name="stationId">工位 Id</param>
    void ResetStation(int stationId);

    /// <summary>清空所有工位与计时器，Id 计数器归 1</summary>
    void ClearAll();

    /// <summary>当前处于 Testing 状态的工位数量</summary>
    int ActiveCount { get; }
}
