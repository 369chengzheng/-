// =============================================================================
// 文件名：IWholeRecordRepository.cs
// 功能描述：整机检测记录仓储接口（分表合并版）。
//           通过 category 参数路由到 {Category}Wholes 物理表，
//           1 个仓储管理 4 张物理表（GasDGWholes/GasOGWholes/WaterDWWholes/WaterOWWholes）。
//           继承 IRepository<Wholes> 获得标准 CRUD 契约；
//           AddAsync 通过 entity.Category 路由到物理表（实现层重写）。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 整机检测记录仓储接口（分表合并版）
/// <para>继承 <see cref="IRepository{T}"/>，扩展按检测类型、订单号、产品序列号、工号、时间范围组合分页查询。</para>
/// <para>分页查询通过 category 参数路由到 {Category}Wholes 物理表。</para>
/// </summary>
public interface IWholeRecordRepository : IRepository<Wholes>
{
    /// <summary>按多条件组合分页查询整机检测记录</summary>
    /// <param name="testType">检测类型筛选（为 null 时不过滤）</param>
    /// <param name="orderNumberKeyword">订单号模糊关键字（为空则不筛选）</param>
    /// <param name="productSnKeyword">产品序列号模糊关键字（为空则不筛选）</param>
    /// <param name="workNumberKeyword">工号模糊关键字（为空则不筛选）</param>
    /// <param name="startTime">检测开始时间下界（为空则不限制）</param>
    /// <param name="endTime">检测结束时间上界（为空则不限制）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>分页结果，按创建时间倒序排列</returns>
    Task<PagedResult<Wholes>> GetPagedAsync(int? testType, string orderNumberKeyword, string productSnKeyword, string workNumberKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);
}
