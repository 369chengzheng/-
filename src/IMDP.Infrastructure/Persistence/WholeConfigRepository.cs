// =============================================================================
// 文件名：WholeConfigRepository.cs
// 功能描述：整机检测配置仓储实现（分表合并版）
//           继承 SplitTableRepository<WholeConfig>，通过 AS(tableName) 动态路由到
//           {Category}WholeConfig 物理表（GasDGWholeConfig 等）。
//           提供按计划类型+产品编码+客户编码查询、分页查询、唯一性校验、按 Id 查询与软删除等功能。
//           重写 AddAsync/UpdateAsync 使用 AS 指定物理表；DeleteAsync(id) 抛异常以强制使用带 category 的重载。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 整机检测配置仓储实现（分表合并版）
/// <para>继承 <see cref="SplitTableRepository{T}"/>，通过 QueryByCategory(category) 路由到对应物理表。</para>
/// <para>插入/更新使用 AS(GetTableName(entity.Category)) 指定物理表。</para>
/// </summary>
public class WholeConfigRepository : SplitTableRepository<WholeConfig>, IWholeConfigRepository
{
    /// <summary>
    /// 初始化 <see cref="WholeConfigRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端</param>
    public WholeConfigRepository(IDbContext dbContext) : base(dbContext) { }

    /// <inheritdoc/>
    protected override string BaseTableName => "WholeConfig";

    /// <summary>
    /// 按主键 Id + 产品类别查询单条整机配置（仅返回未软删除的记录）。
    /// </summary>
    /// <param name="id">主键 Id</param>
    /// <param name="category">产品类别</param>
    /// <returns>匹配的整机配置实体；未找到时返回 null</returns>
    public async Task<WholeConfig?> GetByIdAsync(long id, string category)
    {
        return await QueryByCategory(category)
            .Where(w => w.Id == id && !w.IsDeleted)
            .FirstAsync();
    }

    /// <summary>
    /// 按计划类型、产品编码、客户编码联合查询未删除的整机配置。
    /// </summary>
    /// <param name="planType">计划类型</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="category">产品类别</param>
    /// <returns>匹配的整机配置实体；未找到时返回 null</returns>
    public async Task<WholeConfig?> GetByProductAndCustomerAsync(int planType, string productCode, string customerCode, string category)
    {
        return await QueryByCategory(category)
            .Where(w => w.PlanType == planType && w.ProductCode == productCode && w.CustomerCode == customerCode && !w.IsDeleted)
            .FirstAsync();
    }

    /// <summary>
    /// 按计划类型、产品编码、客户编码关键字分页查询未删除的整机配置。
    /// </summary>
    /// <param name="planType">计划类型（可为空，空表示不筛选）</param>
    /// <param name="productCodeKeyword">产品编码关键字（可为空）</param>
    /// <param name="customerCodeKeyword">客户编码关键字（可为空）</param>
    /// <param name="pageIndex">页索引（从 1 开始）</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="category">产品类别</param>
    /// <returns>包含分页数据及总记录数的分页结果</returns>
    public async Task<PagedResult<WholeConfig>> GetPagedAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, int pageIndex, int pageSize, string category)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        // 默认过滤已软删除的记录
        var query = QueryByCategory(category).Where(w => !w.IsDeleted);

        // 按计划类型精确匹配
        if (planType.HasValue)
            query = query.Where(w => w.PlanType == planType.Value);

        // 按产品编码模糊匹配
        if (!string.IsNullOrWhiteSpace(productCodeKeyword))
            query = query.Where(w => w.ProductCode.Contains(productCodeKeyword));

        // 按客户编码模糊匹配
        if (!string.IsNullOrWhiteSpace(customerCodeKeyword))
            query = query.Where(w => w.CustomerCode.Contains(customerCodeKeyword));

        // 按更新时间倒序分页
        var items = await query.OrderByDescending(w => w.UpdatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<WholeConfig>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 检查指定计划类型下产品编码+客户编码组合是否已存在（用于唯一性校验）。
    /// </summary>
    /// <param name="planType">计划类型</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="excludeId">需要排除的实体 Id（更新时用于排除自身）</param>
    /// <param name="category">产品类别</param>
    /// <returns>若已存在返回 true；否则返回 false</returns>
    public async Task<bool> ProductCustomerExistsAsync(int planType, string productCode, string customerCode, long? excludeId, string category)
    {
        var query = QueryByCategory(category)
            .Where(w => w.PlanType == planType && w.ProductCode == productCode && w.CustomerCode == customerCode && !w.IsDeleted);

        // 更新场景下排除当前实体自身
        if (excludeId.HasValue)
            query = query.Where(w => w.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    /// <summary>
    /// 重写新增方法：通过 AS 指定物理表名插入数据。
    /// </summary>
    /// <param name="entity">待新增的整机配置实体（需设置 Category 字段）</param>
    /// <returns>新增后的实体</returns>
    public override async Task<WholeConfig> AddAsync(WholeConfig entity)
    {
        await _db.Insertable(entity).AS(GetTableName(entity.Category)).ExecuteCommandAsync();
        return entity;
    }

    /// <summary>
    /// 重写更新方法：通过 AS 指定物理表名更新数据。
    /// </summary>
    /// <param name="entity">待更新的整机配置实体（需设置 Category 字段）</param>
    public override async Task UpdateAsync(WholeConfig entity)
    {
        await _db.Updateable(entity).AS(GetTableName(entity.Category)).ExecuteCommandAsync();
    }

    /// <summary>
    /// 重写按 Id 删除方法：分表场景下无法仅凭 Id 定位物理表，抛异常强制使用带 category 的重载。
    /// </summary>
    /// <param name="id">待删除实体的 Id</param>
    /// <exception cref="NotSupportedException">始终抛出，提示使用 DeleteAsync(long, string) 重载</exception>
    public override async Task DeleteAsync(long id)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("分表场景下请使用 DeleteAsync(long id, string category) 方法");
    }

    /// <summary>
    /// 按主键 Id + 产品类别软删除整机配置（IsDeleted = true）。
    /// </summary>
    /// <param name="id">待删除实体的 Id</param>
    /// <param name="category">产品类别</param>
    public async Task DeleteAsync(long id, string category)
    {
        var entity = await QueryByCategory(category).Where(w => w.Id == id).FirstAsync();
        if (entity != null)
        {
            // 软删除：仅标记删除状态，不物理删除
            entity.IsDeleted = true;
            await _db.Updateable(entity).AS(GetTableName(category)).ExecuteCommandAsync();
        }
    }
}
