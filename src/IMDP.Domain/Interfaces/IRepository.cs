// =============================================================================
// 文件名：IRepository.cs
// 功能描述：泛型仓储接口与分页结果，定义领域实体的标准 CRUD 与分页查询契约。
// =============================================================================
using System.Linq.Expressions;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 分页查询结果
/// <para>包装分页查询返回的数据项列表与分页元信息。</para>
/// </summary>
/// <typeparam name="T">数据项类型</typeparam>
public class PagedResult<T>
{
    /// <summary>当前页数据项列表</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>满足筛选条件的总记录数</summary>
    public int TotalCount { get; set; }

    /// <summary>当前页码（从 1 开始）</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }
}

/// <summary>
/// 泛型仓储接口
/// <para>定义实体的标准 CRUD、分页查询、存在性检查与计数等数据访问契约。</para>
/// <para>所有具体仓储接口均继承自本接口，由 Infrastructure 层的 SqlSugar 仓储实现。</para>
/// </summary>
/// <typeparam name="T">实体类型，需为引用类型且有无参构造函数</typeparam>
public interface IRepository<T> where T : class, new()
{
    /// <summary>按主键 Id 查询单条实体</summary>
    /// <param name="id">主键 Id</param>
    /// <returns>匹配的实体；不存在时返回 null</returns>
    Task<T?> GetByIdAsync(long id);

    /// <summary>查询全部实体（不带筛选，慎用于大数据量表）</summary>
    /// <returns>全部实体集合</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>按条件分页查询</summary>
    /// <param name="predicate">筛选条件表达式</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="orderBy">排序字段表达式（为 null 时默认按主键）</param>
    /// <param name="ascending">是否升序（默认 false 降序）</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<T>> GetPagedAsync(
        Expression<Func<T, bool>> predicate,
        int pageIndex, int pageSize,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = false);

    /// <summary>新增实体</summary>
    /// <param name="entity">待新增实体</param>
    /// <returns>新增后的实体（含生成的主键）</returns>
    Task<T> AddAsync(T entity);

    /// <summary>更新实体</summary>
    /// <param name="entity">待更新实体</param>
    /// <returns>异步任务</returns>
    Task UpdateAsync(T entity);

    /// <summary>按主键 Id 删除实体</summary>
    /// <param name="id">主键 Id</param>
    /// <returns>异步任务</returns>
    Task DeleteAsync(long id);

    /// <summary>判断是否存在满足条件的实体</summary>
    /// <param name="predicate">筛选条件表达式</param>
    /// <returns>存在返回 true，否则 false</returns>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

    /// <summary>统计满足条件的实体数量</summary>
    /// <param name="predicate">筛选条件表达式（为 null 时统计全表）</param>
    /// <returns>满足条件的记录数</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
}
