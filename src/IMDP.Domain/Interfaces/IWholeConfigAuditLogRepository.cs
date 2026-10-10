// =============================================================================
// 文件名：IWholeConfigAuditLogRepository.cs
// 功能描述：整机配置变更审计日志仓储接口（分表合并版）。
//           通过 category 参数路由到 {Category}WholeConfigAuditLog 物理表，
//           1 个仓储管理 4 张物理表（GasDGWholeConfigAuditLog 等）。
//           注意：本接口不继承 IRepository<T>，仅提供审计日志专用方法。
//           AddAsync 通过 entity.Category 路由到物理表；GetPagedAsync 通过 category 参数路由。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 整机配置变更审计日志仓储接口（分表合并版）
/// <para>提供 <see cref="WholeConfigAuditLog"/> 的写入与按多条件分页查询能力。</para>
/// <para>所有方法通过 category 参数（或 entity.Category）路由到 {Category}WholeConfigAuditLog 物理表。</para>
/// </summary>
public interface IWholeConfigAuditLogRepository
{
    /// <summary>新增一条整机配置审计日志</summary>
    /// <param name="entity">审计日志实体（需设置 Category 字段以路由到物理表）</param>
    /// <returns>异步任务</returns>
    Task AddAsync(WholeConfigAuditLog entity);

    /// <summary>按多条件分页查询整机配置审计日志</summary>
    /// <param name="planType">检测计划类型筛选（为 null 时不过滤）</param>
    /// <param name="productCodeKeyword">产品编码模糊关键字</param>
    /// <param name="customerCodeKeyword">客户编码模糊关键字</param>
    /// <param name="startTime">变更时间起始值（null 则不筛选）</param>
    /// <param name="endTime">变更时间结束值（null 则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<WholeConfigAuditLog>> GetPagedAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);
}
