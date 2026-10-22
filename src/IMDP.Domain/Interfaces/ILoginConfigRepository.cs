// =============================================================================
// 文件名：ILoginConfigRepository.cs
// 功能描述：用户登录配置日志仓储接口，定义按工号关键字和时间范围分页查询登录日志的契约。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 用户登录配置日志仓储接口
/// <para>继承 <see cref="IRepository{T}"/>，扩展按工号关键字和时间范围分页查询登录日志。</para>
/// </summary>
public interface ILoginConfigRepository : IRepository<LoginConfig>
{
    /// <summary>按工号关键字分页查询登录配置日志</summary>
    /// <param name="workNumberKeyword">工号模糊关键字（为空则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<LoginConfig>> GetPagedAsync(string workNumberKeyword, int pageIndex, int pageSize);

    /// <summary>按工号精确匹配 + 登录时间范围分页查询登录配置日志</summary>
    /// <param name="workNumber">工号（精确匹配，为空则不筛选）</param>
    /// <param name="startTime">登录开始时间（可空）</param>
    /// <param name="endTime">登录结束时间（可空）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果（按登录时间倒序）</returns>
    Task<PagedResult<LoginConfig>> GetPagedAsync(string workNumber, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize);

    /// <summary>
    /// 插入一条登录配置日志，并返回数据库分配的自增主键 Id。
    /// <para>登录成功后将 Id 暂存到上下文，供正常退出登录时回填登出时间。
    /// 不等同于 <see cref="IRepository{T}.AddAsync"/>：后者不回填自增主键。</para>
    /// </summary>
    /// <param name="loginConfig">待插入的登录配置日志实体。</param>
    /// <returns>数据库自增主键 Id（插入失败由调用方捕获异常）。</returns>
    Task<long> InsertReturnIdentityAsync(LoginConfig loginConfig);
}
