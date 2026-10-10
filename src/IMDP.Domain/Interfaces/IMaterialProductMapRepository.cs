// =============================================================================
// 文件名：IMaterialProductMapRepository.cs
// 功能描述：物料-产品插件映射仓储接口，定义按物料编号查询、分页查询与唯一性校验契约。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 物料-产品插件映射仓储接口
/// <para>继承 <see cref="IRepository{T}"/>，扩展按物料编号查询与唯一性校验等方法。</para>
/// </summary>
public interface IMaterialProductMapRepository : IRepository<MaterialProductMap>
{
    /// <summary>按物料编号查询单条映射</summary>
    /// <param name="materialNumber">物料编号</param>
    /// <returns>匹配的映射实体；不存在时返回 null</returns>
    Task<MaterialProductMap?> GetByMaterialNumberAsync(string materialNumber);

    /// <summary>按物料编号关键字分页查询映射</summary>
    /// <param name="materialNumberKeyword">物料编号模糊关键字</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<MaterialProductMap>> GetPagedAsync(string materialNumberKeyword, int pageIndex, int pageSize);

    /// <summary>判断物料编号是否已存在（用于唯一性校验）</summary>
    /// <param name="materialNumber">待校验的物料编号</param>
    /// <param name="excludeId">需排除的记录主键（编辑场景传入当前记录 Id，新增场景留空）</param>
    /// <returns>已存在返回 true，否则 false</returns>
    Task<bool> MaterialNumberExistsAsync(string materialNumber, long? excludeId = null);
}
