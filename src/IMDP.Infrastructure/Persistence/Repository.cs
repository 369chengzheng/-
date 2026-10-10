// =============================================================================
// 文件名：Repository.cs
// 功能描述：泛型仓储基类实现
//           基于 SqlSugar 的 ISqlSugarClient 与 SimpleClient<T> 提供通用的
//           CRUD、存在性判断、计数与分页查询能力，是所有具体仓储的基类。
// =============================================================================
using System.Linq.Expressions;
using SqlSugar;
using IMDP.Domain.Interfaces;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 泛型仓储基类
/// <para>基于 SqlSugar <see cref="ISqlSugarClient"/> 与 <see cref="SimpleClient{T}"/> 实现通用 CRUD；
/// 具体仓储可继承本类并扩展专用查询方法，也可重写虚方法以定制行为（如软删除）。</para>
/// </summary>
/// <typeparam name="T">实体类型，需为引用类型且具有无参构造函数。</typeparam>
public class Repository<T> : IRepository<T> where T : class, new()
{
    /// <summary>数据库上下文（保留供子类访问连接等）。</summary>
    protected readonly IDbContext _dbContext;
    /// <summary>SqlSugar 客户端，用于构造查询表达式。</summary>
    protected readonly ISqlSugarClient _db;
    /// <summary>SimpleClient 包装器，用于便捷的单一实体操作。</summary>
    protected readonly SimpleClient<T> _dbSet;

    /// <summary>
    /// 初始化 <see cref="Repository{T}"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，从中获取 SqlSugar 客户端与 SimpleClient。</param>
    public Repository(IDbContext dbContext)
    {
        _dbContext = dbContext;
        _db = dbContext.Client;
        _dbSet = dbContext.Client.GetSimpleClient<T>();
    }

    /// <summary>
    /// 按主键 Id 查询实体。
    /// </summary>
    /// <param name="id">主键 Id。</param>
    /// <returns>实体实例；未找到时返回 null。</returns>
    public virtual async Task<T?> GetByIdAsync(long id)
    {
        return await _dbSet.GetByIdAsync(id);
    }

    /// <summary>
    /// 查询全部实体。
    /// </summary>
    /// <returns>实体集合。</returns>
    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.GetListAsync();
    }

    /// <summary>
    /// 新增实体。
    /// <para>主键为 BIGINT IDENTITY 自增，由数据库分配，无需应用层赋值。</para>
    /// </summary>
    /// <param name="entity">待新增的实体。</param>
    /// <returns>新增后的实体（便于获取主键等）。</returns>
    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.InsertAsync(entity);
        return entity;
    }

    /// <summary>
    /// 更新实体。
    /// </summary>
    /// <param name="entity">待更新的实体。</param>
    public virtual async Task UpdateAsync(T entity)
    {
        await _dbSet.UpdateAsync(entity);
    }

    /// <summary>
    /// 按主键 Id 删除实体（物理删除）。
    /// <para>如需软删除，请在子类中重写本方法。</para>
    /// </summary>
    /// <param name="id">待删除实体的主键 Id。</param>
    public virtual async Task DeleteAsync(long id)
    {
        await _dbSet.DeleteByIdAsync(id);
    }

    /// <summary>
    /// 判断是否存在满足指定条件的实体。
    /// </summary>
    /// <param name="predicate">查询条件表达式。</param>
    /// <returns>存在返回 true；否则返回 false。</returns>
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.IsAnyAsync(predicate);
    }

    /// <summary>
    /// 统计满足条件的实体数量；未指定条件时统计全部。
    /// </summary>
    /// <param name="predicate">查询条件表达式（可为空）。</param>
    /// <returns>实体数量。</returns>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await _db.Queryable<T>().CountAsync()
            : await _dbSet.CountAsync(predicate);
    }

    /// <summary>
    /// 通用分页查询。
    /// </summary>
    /// <param name="predicate">查询条件（可为空）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <param name="orderBy">排序字段（可为空）。</param>
    /// <param name="ascending">是否升序；默认 false（降序）。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public virtual async Task<PagedResult<T>> GetPagedAsync(
        Expression<Func<T, bool>> predicate, int pageIndex, int pageSize,
        Expression<Func<T, object>>? orderBy = null, bool ascending = false)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<T>();
        // 应用查询条件
        if (predicate != null)
            query = query.Where(predicate);
        // 应用排序：默认降序
        if (orderBy != null)
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        // 执行分页查询
        var items = await query.ToPageListAsync(pageIndex, pageSize, totalCount);
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
