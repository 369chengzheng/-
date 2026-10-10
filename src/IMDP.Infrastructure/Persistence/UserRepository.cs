// =============================================================================
// 文件名：UserRepository.cs
// 功能描述：用户（User）仓储实现
//           提供按工号查询用户、分页查询用户（支持工号/姓名关键字）、
//           工号唯一性校验等功能，用于用户管理与登录认证。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 用户仓储实现
/// <para>继承自泛型仓储 <see cref="Repository{T}"/>，提供按工号查询与用户分页查询。</para>
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    /// <summary>
    /// 初始化 <see cref="UserRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public UserRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 按工号查询未删除的用户。
    /// </summary>
    /// <param name="workNumber">工号。</param>
    /// <returns>用户实体；未找到时返回 null。</returns>
    public async Task<User?> GetByWorkNumberAsync(string workNumber)
    {
        return await _db.Queryable<User>()
            .Where(u => u.WorkNumber == workNumber && !u.IsDeleted)
            .FirstAsync();
    }

    /// <summary>
    /// 按关键字分页查询未删除的用户（关键字同时匹配工号与姓名）。
    /// </summary>
    /// <param name="keyword">查询关键字（可为空，空表示不筛选）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public async Task<PagedResult<User>> GetPagedAsync(string? workNumberKeyword, string? nameKeyword, string? role, int pageIndex, int pageSize)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        // 默认过滤已软删除的用户
        var query = _db.Queryable<User>().Where(u => !u.IsDeleted);

        // 关键字同时匹配工号与姓名
        if (!string.IsNullOrWhiteSpace(workNumberKeyword))
            query = query.Where(u => u.WorkNumber.Contains(workNumberKeyword));

        if (!string.IsNullOrWhiteSpace(nameKeyword))
            query = query.Where(u => u.Name.Contains(nameKeyword));

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        // 按创建时间倒序分页
        var items = await query.OrderByDescending(u => u.CreatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<User>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 检查工号是否已存在（用于唯一性校验）。
    /// </summary>
    /// <param name="workNumber">待校验的工号。</param>
    /// <param name="excludeId">需要排除的实体 Id（更新时用于排除自身）。</param>
    /// <returns>若已存在返回 true；否则返回 false。</returns>
    public async Task<bool> WorkNumberExistsAsync(string workNumber, long? excludeId = null)
    {
        var query = _db.Queryable<User>()
            .Where(u => u.WorkNumber == workNumber && !u.IsDeleted);

        // 更新场景下排除当前实体自身
        if (excludeId.HasValue)
            query = query.Where(u => u.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    /// <summary>
    /// /// <summary>统计未删除用户中指定角色的数量</summary>
    /// <param name="role">角色代码（如 SystemAdmin）</param>
    /// <returns>防止删除或降级最后一个系统管理员，保证系统永远至少有一个最高权限的人能登录</returns>
    public async Task<int> CountActiveByRoleAsync(string role)
    {
        return await _db.Queryable<User>()
            .Where(u => !u.IsDeleted && u.Role == role)
            .CountAsync();
    }
}