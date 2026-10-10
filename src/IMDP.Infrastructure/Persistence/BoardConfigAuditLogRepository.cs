// =============================================================================
// 文件名：BoardConfigAuditLogRepository.cs
// 功能描述：板卡配置变更审计日志仓储实现（分表合并版）
//           独立实现（不继承 SplitTableRepository），通过 AS(tableName) 动态路由到
//           {Category}BoardConfigAuditLog 物理表（GasDGBoardConfigAuditLog 等）。
//           AddAsync 通过 entity.Category 路由到物理表；GetPagedAsync 通过 category 参数路由。
//           注意：本仓储不继承 IRepository<T>，仅提供审计日志专用方法。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 板卡配置变更审计日志仓储实现（分表合并版）
/// <para>直接持有 <see cref="SqlSugarScope"/>，不继承泛型仓储基类（避免 AddAsync 签名冲突）。</para>
/// <para>通过 AS(tableName) 动态指定物理表名 {Category}BoardConfigAuditLog。</para>
/// </summary>
public class BoardConfigAuditLogRepository : IBoardConfigAuditLogRepository
{
    /// <summary>基表名（不含产品类别前缀）</summary>
    private const string BaseTableName = "BoardConfigAuditLog";

    private readonly SqlSugarScope _db;

    /// <summary>
    /// 初始化 <see cref="BoardConfigAuditLogRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，需提供 <see cref="SqlSugarScope"/> 实例</param>
    /// <exception cref="InvalidOperationException">当上下文客户端不是 <see cref="SqlSugarScope"/> 时抛出</exception>
    public BoardConfigAuditLogRepository(IDbContext dbContext)
    {
        // 将客户端强制转换为 SqlSugarScope，确保支持事务与多表操作
        _db = dbContext.Client as SqlSugarScope
              ?? throw new InvalidOperationException("SqlSugarScope is not available.");
    }

    /// <summary>
    /// 获取物理表名（{Category}BoardConfigAuditLog）
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
    private ISugarQueryable<BoardConfigAuditLog> QueryByCategory(string category)
        => _db.Queryable<BoardConfigAuditLog>().AS(GetTableName(category));

    /// <summary>
    /// 异步新增一条审计日志记录。
    /// </summary>
    /// <param name="entity">审计日志实体（需设置 Category 字段以路由到物理表）</param>
    public async Task AddAsync(BoardConfigAuditLog entity)
    {
        await _db.Insertable(entity).AS(GetTableName(entity.Category)).ExecuteCommandAsync();
    }

    /// <summary>
    /// 按产品编码关键字与变更时间范围分页查询审计日志。
    /// </summary>
    /// <param name="productCodeKeyword">产品编码关键字（可为空，空表示不筛选）。关键字同时匹配变更前/后的数据。</param>
    /// <param name="startTime">变更时间起始值（可为空，空表示不筛选）</param>
    /// <param name="endTime">变更时间结束值（可为空，空表示不筛选）</param>
    /// <param name="pageIndex">页索引（从 1 开始）</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="category">产品类别</param>
    /// <returns>包含分页数据及总记录数的分页结果</returns>
    public async Task<PagedResult<BoardConfigAuditLog>> GetPagedAsync(string productCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = QueryByCategory(category);

        // 关键字同时匹配变更后数据（InsertedData）和变更前数据（DeletedData）
        if (!string.IsNullOrWhiteSpace(productCodeKeyword))
        {
            query = query.Where(a => a.InsertedData!.Contains(productCodeKeyword) || a.DeletedData!.Contains(productCodeKeyword));
        }

        // 变更时间范围过滤
        if (startTime.HasValue)
            query = query.Where(a => a.CreatedTime >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(a => a.CreatedTime <= endTime.Value);

        // 按创建时间倒序分页
        var items = await query.OrderByDescending(a => a.CreatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<BoardConfigAuditLog>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
