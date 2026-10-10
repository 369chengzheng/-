// =============================================================================
// 文件名：IBoardDetectService.cs
// 功能描述：板卡检测服务接口（领域层契约）
//           封装板卡检测流程：串口开关、可用端口枚举、单工位板卡检测执行。
//           实现类位于 Infrastructure 层（BoardDetectService），负责协议交互与硬件通讯。
//           此接口定义于 Domain 层以避免 Infrastructure 反向依赖 Application 层。
// =============================================================================
namespace IMDP.Domain.Interfaces;

/// <summary>
/// 板卡检测服务接口
/// <para>封装板卡检测流程：串口开关、可用端口枚举、单工位板卡检测执行。</para>
/// <para>实现类位于 Infrastructure 层（BoardDetectService），负责协议交互与硬件通讯。</para>
/// </summary>
public interface IBoardDetectService
{
    /// <summary>在指定工位执行板卡检测</summary>
    /// <param name="stationId">工位 Id</param>
    /// <param name="productSn">待检测产品序列号</param>
    /// <param name="portName">串口名称</param>
    /// <returns>检测结果（true=合格，false=不合格）</returns>
    Task<bool> DetectBoardAsync(int stationId, string productSn, string portName);

    /// <summary>打开指定串口</summary>
    /// <param name="portName">串口名称</param>
    /// <returns>是否成功打开</returns>
    Task<bool> OpenPortAsync(string portName);

    /// <summary>关闭指定串口</summary>
    /// <param name="portName">串口名称</param>
    /// <returns>表示异步操作的任务</returns>
    Task ClosePortAsync(string portName);

    /// <summary>关闭所有已打开的串口</summary>
    /// <returns>表示异步操作的任务</returns>
    Task CloseAllPortsAsync();

    /// <summary>获取系统可用的串口名称列表</summary>
    /// <returns>可用串口名称只读列表</returns>
    IReadOnlyList<string> GetAvailablePorts();

    /// <summary>判断指定串口是否已打开</summary>
    /// <param name="portName">串口名称</param>
    /// <returns>已打开返回 true，否则 false</returns>
    bool IsPortOpen(string portName);
}
