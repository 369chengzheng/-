// =============================================================================
// 文件名：IOrderSwitchRepository.cs
// 功能描述：订单切换记录仓储接口，定义按订单号关键字分页查询订单切换记录的契约。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 订单切换记录仓储接口
/// <para>继承 <see cref="IRepository{T}"/>，扩展按订单号关键字分页查询订单切换记录。</para>
/// </summary>
public interface IOrderSwitchRepository : IRepository<OrderSwitch>
{
    /// <summary>按订单号关键字分页查询订单切换记录</summary>
    /// <param name="orderNumberKeyword">订单号模糊关键字（为空则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<OrderSwitch>> GetPagedAsync(string orderNumberKeyword, int pageIndex, int pageSize);

    /// <summary>按制令单号 + 操作员 + 操作时间范围分页查询工单切换记录（供 4.17 工单切换日志查询模块使用）</summary>
    /// <param name="orderNumberKeyword">制令单号模糊关键字（为空则不筛选）</param>
    /// <param name="operatorKeyword">操作员模糊关键字（匹配 CreatedBy，为空则不筛选）</param>
    /// <param name="startTime">操作开始时间（可空，匹配 CreatedTime）</param>
    /// <param name="endTime">操作结束时间（可空，匹配 CreatedTime）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="ascending">按操作时间升序排列（true=正序，false=倒序，默认倒序）</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<OrderSwitch>> GetPagedAsync(string orderNumberKeyword, string operatorKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, bool ascending = false);
}
