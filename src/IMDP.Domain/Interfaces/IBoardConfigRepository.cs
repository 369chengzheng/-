// =============================================================================
// 文件名：IBoardConfigRepository.cs
// 功能描述：板卡检测配置仓储接口（分表合并版）。
//           通过 category 参数路由到 {Category}BoardConfig 物理表，
//           1 个仓储管理 4 张物理表（GasDGBoardConfig/GasOGBoardConfig/WaterDWBoardConfig/WaterOWBoardConfig）。
//           继承 IRepository<BoardConfig> 获得标准 CRUD 契约；
//           注意：继承的 GetByIdAsync(long)/DeleteAsync(long) 因无法定位物理表而不可用，
//           请使用本接口新增的带 category 参数的重载。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 板卡检测配置仓储接口（分表合并版）
/// <para>继承 <see cref="IRepository{T}"/>，扩展按产品编码查询、唯一性校验、按类别查询与软删除等方法。</para>
/// <para>所有方法通过 category 参数路由到 {Category}BoardConfig 物理表。</para>
/// </summary>
public interface IBoardConfigRepository : IRepository<BoardConfig>
{
    /// <summary>按主键 Id + 产品类别查询单条板卡配置</summary>
    /// <param name="id">主键 Id</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>匹配的配置实体；不存在时返回 null</returns>
    Task<BoardConfig?> GetByIdAsync(long id, string category);

    /// <summary>按产品编码查询单条配置</summary>
    /// <param name="productCode">产品编码</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>匹配的配置实体；不存在时返回 null</returns>
    Task<BoardConfig?> GetByProductCodeAsync(string productCode, string category);

    /// <summary>按产品编码关键字分页查询配置</summary>
    /// <param name="productCodeKeyword">产品编码模糊关键字</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<BoardConfig>> GetPagedAsync(string productCodeKeyword, int pageIndex, int pageSize, string category);

    /// <summary>判断产品编码是否已存在（用于唯一性校验）</summary>
    /// <param name="productCode">待校验的产品编码</param>
    /// <param name="excludeId">需排除的记录主键（编辑场景传入当前记录 Id，新增场景留空）</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>已存在返回 true，否则 false</returns>
    Task<bool> ProductCodeExistsAsync(string productCode, long? excludeId, string category);

    /// <summary>按主键 Id + 产品类别软删除板卡配置（IsDeleted = true）</summary>
    /// <param name="id">待删除实体的 Id</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>异步任务</returns>
    Task DeleteAsync(long id, string category);
}
