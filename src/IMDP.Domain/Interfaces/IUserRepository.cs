// =============================================================================
// 文件名：IUserRepository.cs
// 功能描述：用户仓储接口，定义按工号查询、分页查询与工号唯一性校验契约。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 用户仓储接口
/// <para>继承 <see cref="IRepository{T}"/>，扩展按工号查询与工号唯一性校验等方法。</para>
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>按工号查询单条用户</summary>
    /// <param name="workNumber">工号</param>
    /// <returns>匹配的用户实体；不存在时返回 null</returns>
    Task<User?> GetByWorkNumberAsync(string workNumber);

    /// <summary>按关键字分页查询用户</summary>
    /// <param name="keyword">模糊关键字（匹配工号或姓名）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<User>> GetPagedAsync(string? workNumberKeyword, string? nameKeyword, string? role, int pageIndex, int pageSize);

    /// <summary>判断工号是否已存在（用于唯一性校验）</summary>
    /// <param name="workNumber">待校验的工号</param>
    /// <param name="excludeId">需排除的记录主键（编辑场景传入当前记录 Id，新增场景留空）</param>
    /// <returns>已存在返回 true，否则 false</returns>
    Task<bool> WorkNumberExistsAsync(string workNumber, long? excludeId);

    /// <summary>
    /// /// <summary>统计未删除用户中指定角色的数量</summary>
    /// <param name="role">角色代码（如 SystemAdmin）</param>
    /// <returns>防止删除或降级最后一个系统管理员，保证系统永远至少有一个最高权限的人能登录</returns>
    Task<int> CountActiveByRoleAsync(string role);
}