// =============================================================================
// 文件名：WholeConfigAuditLogRepository.cs
// 功能描述：整机配置变更审计日志仓储实现（分表合并版）
//           独立实现（不继承 SplitTableRepository），通过 AS(tableName) 动态路由到
//           {Category}WholeConfigAuditLog 物理表（GasDGWholeConfigAuditLog 等）。
//           AddAsync 通过 entity.Category 路由到物理表；GetPagedAsync 通过 category 参数路由。
//           注意：本仓储不继承 IRepository<T>，仅提供审计日志专用方法。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 整机配置变更审计日志仓储实现（分表合并版）
/// <para>直接持有 <see cref="SqlSugarScope"/>，不继承泛型仓储基类（避免 AddAsync 签名冲突）。</para>
/// <para>通过 AS(tableName) 动态指定物理表名 {Category}WholeConfigAuditLog。</para>
/// </summary>
public class WholeConfigAuditLogRepository : IWholeConfigAuditLogRepository
{
    /// <summary>基表名（不含产品类别前缀）</summary>
    private const string BaseTableName = "WholeConfigAuditLog";

    private readonly SqlSugarScope _db;

    /// <summary>
    /// 初始化 <see cref="WholeConfigAuditLogRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，需提供 <see cref="SqlSugarScope"/> 实例</param>
    /// <exception cref="InvalidOperationException">当上下文客户端不是 <see cref="SqlSugarScope"/> 时抛出</exception>
    public WholeConfigAuditLogRepository(IDbContext dbContext)
    {
        // 将客户端强制转换为 SqlSugarScope，确保支持事务与多表操作
        _db = dbContext.Client as SqlSugarScope
              ?? throw new InvalidOperationException("SqlSugarScope is not available.");
    }

    /// <summary>
    /// 获取物理表名（{Category}WholeConfigAuditLog）
    /// </summary>
    /// <param name="category">产品类别</param>
    /// <returns>物理表名</returns>
    private static string GetTableName(string category)
        => $"{category}{BaseTableName}";

    /// <summary>
    /// 创建已路由到指定物理表的可查询对象。
    /// </summary>
    /// <param name="category">产品类别</param>
    /// <returns>已 AS 到物理表的可查询对象</returns>
    private ISugarQueryable<WholeConfigAuditLog> QueryByCategory(string category)
        => _db.Queryable<WholeConfigAuditLog>().AS(GetTableName(category));

    /// <summary>
    /// 异步新增一条整机配置审计日志记录。
    /// </summary>
    /// <param name="entity">审计日志实体（需设置 Category 字段以路由到物理表）</param>
    public async Task AddAsync(WholeConfigAuditLog entity)
    {
        await _db.Insertable(entity).AS(GetTableName(entity.Category)).ExecuteCommandAsync();
    }

    /// <summary>
    /// 按计划类型、产品编码、客户编码关键字与变更时间范围分页查询整机配置审计日志。
    /// </summary>
    /// <param name="planType">计划类型（可为空，空表示不筛选）</param>
    /// <param name="productCodeKeyword">产品编码关键字（可为空，空表示不筛选）</param>
    /// <param name="customerCodeKeyword">客户编码关键字（可为空，空表示不筛选）</param>
    /// <param name="startTime">变更时间起始值（可为空，空表示不筛选）</param>
    /// <param name="endTime">变更时间结束值（可为空，空表示不筛选）</param>
    /// <param name="pageIndex">页索引（从 1 开始）</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="category">产品类别</param>
    /// <returns>包含分页数据及总记录数的分页结果</returns>
    public async Task<PagedResult<WholeConfigAuditLog>> GetPagedAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = QueryByCategory(category);

        // 计划类型匹配 InsertedData/DeletedData JSON 中的 "PlanType":<value> 字段
        if (planType.HasValue)
        {
            var planTypeKeyword = $"\"PlanType\":{planType.Value}";
            query = query.Where(a => a.InsertedData!.Contains(planTypeKeyword) || a.DeletedData!.Contains(planTypeKeyword));
        }

        // 产品编码关键字同时匹配变更后数据（InsertedData）和变更前数据（DeletedData）
        if (!string.IsNullOrWhiteSpace(productCodeKeyword))
        {
            query = query.Where(a => a.InsertedData!.Contains(productCodeKeyword) || a.DeletedData!.Contains(productCodeKeyword));
        }

        // 客户编码关键字同时匹配变更后数据（InsertedData）和变更前数据（DeletedData）
        if (!string.IsNullOrWhiteSpace(customerCodeKeyword))
        {
            query = query.Where(a => a.InsertedData!.Contains(customerCodeKeyword) || a.DeletedData!.Contains(customerCodeKeyword));
        }

        // 变更时间范围过滤
        if (startTime.HasValue)
            query = query.Where(a => a.CreatedTime >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(a => a.CreatedTime <= endTime.Value);

        // 按创建时间倒序分页
        var items = await query.OrderByDescending(a => a.CreatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<WholeConfigAuditLog>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
