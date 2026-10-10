// =============================================================================
// 文件名：OrderSwitchRepository.cs
// 功能描述：订单切换记录（OrderSwitch）仓储实现
//           提供订单切换记录的分页查询，支持按订单号关键字检索，
//           用于跟踪生产过程中的订单切换操作。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 订单切换记录仓储实现
/// <para>继承自泛型仓储 <see cref="Repository{T}"/>，提供按订单号分页查询切换记录的方法。</para>
/// </summary>
public class OrderSwitchRepository : Repository<OrderSwitch>, IOrderSwitchRepository
{
    /// <summary>
    /// 初始化 <see cref="OrderSwitchRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public OrderSwitchRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 按订单号关键字分页查询切换记录。
    /// </summary>
    /// <param name="orderNumberKeyword">订单号关键字（可为空，空表示不筛选）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public async Task<PagedResult<OrderSwitch>> GetPagedAsync(string orderNumberKeyword, int pageIndex, int pageSize)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<OrderSwitch>();

        // 关键字按订单号模糊匹配
        if (!string.IsNullOrWhiteSpace(orderNumberKeyword))
            query = query.Where(o => o.OrderNumber.Contains(orderNumberKeyword));

        // 按创建时间倒序分页
        var items = await query.OrderByDescending(o => o.CreatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<OrderSwitch>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<PagedResult<OrderSwitch>> GetPagedAsync(string orderNumberKeyword, string operatorKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, bool ascending = false)
    {
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<OrderSwitch>();

        // 制令单号模糊匹配
        if (!string.IsNullOrWhiteSpace(orderNumberKeyword))
            query = query.Where(o => o.OrderNumber.Contains(orderNumberKeyword));

        // 操作员模糊匹配（CreatedBy 字段）
        if (!string.IsNullOrWhiteSpace(operatorKeyword))
            query = query.Where(o => o.CreatedBy.Contains(operatorKeyword));

        // 操作时间范围筛选（基于 CreatedTime）
        if (startTime.HasValue)
            query = query.Where(o => o.CreatedTime >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(o => o.CreatedTime < endTime.Value);

        // 按操作时间排序（正序/倒序）
        var orderedQuery = ascending ? query.OrderBy(o => o.CreatedTime) : query.OrderByDescending(o => o.CreatedTime);
        var items = await orderedQuery.ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<OrderSwitch>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}