// =============================================================================
// 文件名：LoginRepository.cs
// 功能描述：登录记录（Login）仓储实现
//           提供用户登录历史记录的持久化与分页查询，
//           支持按工号关键字检索登录日志，用于审计与追溯。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 登录记录仓储实现
/// <para>继承自泛型仓储 <see cref="Repository{T}"/>，提供按工号分页查询登录记录的方法。</para>
/// </summary>
public class LoginRepository : Repository<Login>, ILoginRepository
{
    /// <summary>
    /// 初始化 <see cref="LoginRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public LoginRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 按工号关键字分页查询登录记录。
    /// </summary>
    /// <param name="workNumberKeyword">工号关键字（可为空，空表示不筛选）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public async Task<PagedResult<Login>> GetPagedAsync(string workNumberKeyword, int pageIndex, int pageSize)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<Login>();

        // 关键字按工号模糊匹配
        if (!string.IsNullOrWhiteSpace(workNumberKeyword))
            query = query.Where(l => l.WorkNumber.Contains(workNumberKeyword));

        // 按登录时间倒序分页
        var items = await query.OrderByDescending(l => l.LoginTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<Login>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<PagedResult<Login>> GetPagedAsync(string workNumber, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
    {
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<Login>();

        // 工号精确匹配（为空则不筛选）
        if (!string.IsNullOrWhiteSpace(workNumber))
            query = query.Where(l => l.WorkNumber == workNumber);

        // 登录时间范围筛选
        // DatePicker 仅含日期，时间部分为 00:00:00；为避免"截止选今天却查不出今天"的问题，
        // 开始时间归一化到当日 00:00:00，截止时间归一化到当日 23:59:59.9999999（即次日 00:00:00 前一刻）。
        if (startTime.HasValue)
            query = query.Where(l => l.LoginTime >= startTime.Value.Date);
        if (endTime.HasValue)
            query = query.Where(l => l.LoginTime <= endTime.Value.Date.AddDays(1).AddTicks(-1));

        // 按登录时间倒序分页
        var items = await query.OrderByDescending(l => l.LoginTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<Login>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<long> InsertReturnIdentityAsync(Login login)
    {
        // ExecuteReturnIdentityAsync 返回数据库自增主键（Login.Id 为 IsIdentity 自增列）
        return await _db.Insertable(login).ExecuteReturnIdentityAsync();
    }
}
