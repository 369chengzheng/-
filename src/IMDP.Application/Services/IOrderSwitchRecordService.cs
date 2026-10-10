// =============================================================================
// 文件名：IOrderSwitchRecordService.cs
// 功能描述：工单切换记录写入服务接口，负责会话内连续去重并保存菜单快照
// =============================================================================
using IMDP.Application.DTOs;

namespace IMDP.Application.Services;

/// <summary>
/// 工单切换记录写入服务
/// <para>同一登录会话中，连续相同的“制令单号 + 软件物料号”只记录一次；
/// 切换到其他工单后再切回时重新记录</para>
/// </summary>
public interface IOrderSwitchRecordService
{
    /// <summary>
    /// 当工单或物料发生变化时写入一条切换记录
    /// </summary>
    /// <param name="orderNumber">制令单号</param>
    /// <param name="materialNumber">软件物料号</param>
    /// <param name="loadedMenus">当前用户经过角色和工位过滤后实际加载的菜单（含显示标题）</param>
    /// <param name="createdBy">操作员工号</param>
    /// <param name="createdTime">菜单解析完成并生效的时间</param>
    /// <returns>实际写入返回 true；连续重复或入参无效返回 false</returns>
    Task<bool> RecordIfChangedAsync(
        string orderNumber,
        string materialNumber,
        IReadOnlyList<ProductMenuItemDto> loadedMenus,
        string createdBy,
        DateTime createdTime);

    /// <summary>清除当前登录会话的工单去重状态</summary>
    void ResetSession();
}
