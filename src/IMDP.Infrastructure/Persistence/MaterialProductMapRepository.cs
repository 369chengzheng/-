// =============================================================================
// 文件名：MaterialProductMapRepository.cs
// 功能描述：物料-产品映射（MaterialProductMap）仓储实现
//           提供按物料编号查询、分页查询、物料编号唯一性校验等功能，
//           用于维护物料编号与产品配置之间的映射关系。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 物料-产品映射仓储实现
/// <para>继承自泛型仓储 <see cref="Repository{T}"/>，提供物料编号维度的查询与唯一性校验。</para>
/// </summary>
public class MaterialProductMapRepository : Repository<MaterialProductMap>, IMaterialProductMapRepository
{
    /// <summary>
    /// 初始化 <see cref="MaterialProductMapRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public MaterialProductMapRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 按物料编号查询未删除的映射记录。
    /// </summary>
    /// <param name="materialNumber">物料编号。</param>
    /// <returns>匹配的映射实体；未找到时返回 null。</returns>
    public async Task<MaterialProductMap?> GetByMaterialNumberAsync(string materialNumber)
    {
        return await _db.Queryable<MaterialProductMap>()
            .Where(m => m.MaterialNumber == materialNumber && !m.IsDeleted)
            .FirstAsync();
    }

    /// <summary>
    /// 按物料编号关键字分页查询未删除的映射记录。
    /// </summary>
    /// <param name="materialNumberKeyword">物料编号关键字（可为空，空表示不筛选）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public async Task<PagedResult<MaterialProductMap>> GetPagedAsync(string materialNumberKeyword, int pageIndex, int pageSize)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        // 默认过滤已软删除的记录
        var query = _db.Queryable<MaterialProductMap>().Where(m => !m.IsDeleted);

        // 关键字按物料编号模糊匹配
        if (!string.IsNullOrWhiteSpace(materialNumberKeyword))
        {
            query = query.Where(m => m.MaterialNumber.Contains(materialNumberKeyword));
        }

        // 按创建时间倒序分页
        var items = await query.OrderByDescending(m => m.CreatedTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<MaterialProductMap>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 检查物料编号是否已存在（用于唯一性校验）。
    /// </summary>
    /// <param name="materialNumber">待校验的物料编号。</param>
    /// <param name="excludeId">需要排除的实体 Id（更新时用于排除自身）。</param>
    /// <returns>若已存在返回 true；否则返回 false。</returns>
    public async Task<bool> MaterialNumberExistsAsync(string materialNumber, long? excludeId = null)
    {
        var query = _db.Queryable<MaterialProductMap>()
            .Where(m => m.MaterialNumber == materialNumber && !m.IsDeleted);

        // 更新场景下排除当前实体自身
        if (excludeId.HasValue)
            query = query.Where(m => m.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}
