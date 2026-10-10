// =============================================================================
// 文件名：LoginConfigRepository.cs
// 功能描述：登录配置日志（LoginConfig）仓储实现
//           提供用户登录历史记录的持久化与分页查询，
//           支持按工号关键字和时间范围检索登录日志，用于审计与追溯。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 登录配置日志仓储实现
/// <para>继承自泛型仓储 <see cref="Repository{T}"/>，提供按工号和时间分页查询登录日志的方法。</para>
/// </summary>
public class LoginConfigRepository : Repository<LoginConfig>, ILoginConfigRepository
{
    /// <summary>
    /// 初始化 <see cref="LoginConfigRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public LoginConfigRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 按工号关键字分页查询登录配置日志。
    /// </summary>
    /// <param name="workNumberKeyword">工号关键字（可为空，空表示不筛选）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public async Task<PagedResult<LoginConfig>> GetPagedAsync(string workNumberKeyword, int pageIndex, int pageSize)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<LoginConfig>();

        // 关键字按工号模糊匹配
        if (!string.IsNullOrWhiteSpace(workNumberKeyword))
            query = query.Where(l => l.WorkNumber.Contains(workNumberKeyword));

        // 按登录时间倒序分页
        var items = await query.OrderByDescending(l => l.LoginTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<LoginConfig>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<PagedResult<LoginConfig>> GetPagedAsync(string workNumber, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
    {
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<LoginConfig>();

        // 工号精确匹配（为空则不筛选）
        if (!string.IsNullOrWhiteSpace(workNumber))
            query = query.Where(l => l.WorkNumber == workNumber);

        // 登录时间范围筛选
        if (startTime.HasValue)
            query = query.Where(l => l.LoginTime >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(l => l.LoginTime <= endTime.Value);

        // 按登录时间倒序分页
        var items = await query.OrderByDescending(l => l.LoginTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<LoginConfig>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
