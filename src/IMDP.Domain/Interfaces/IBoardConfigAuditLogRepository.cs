// =============================================================================
// 文件名：IBoardConfigAuditLogRepository.cs
// 功能描述：板卡配置变更审计日志仓储接口（分表合并版）。
//           通过 category 参数路由到 {Category}BoardConfigAuditLog 物理表，
//           1 个仓储管理 4 张物理表（GasDGBoardConfigAuditLog 等）。
//           注意：本接口不继承 IRepository<T>，仅提供审计日志专用方法。
//           AddAsync 通过 entity.Category 路由到物理表；GetPagedAsync 通过 category 参数路由。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 板卡配置变更审计日志仓储接口（分表合并版）
/// <para>提供 <see cref="BoardConfigAuditLog"/> 的写入与按产品编码关键字分页查询能力。</para>
/// <para>所有方法通过 category 参数（或 entity.Category）路由到 {Category}BoardConfigAuditLog 物理表。</para>
/// </summary>
public interface IBoardConfigAuditLogRepository
{
    /// <summary>新增一条审计日志</summary>
    /// <param name="entity">审计日志实体（需设置 Category 字段以路由到物理表）</param>
    /// <returns>异步任务</returns>
    Task AddAsync(BoardConfigAuditLog entity);

    /// <summary>按产品编码关键字与变更时间范围分页查询审计日志</summary>
    /// <param name="productCodeKeyword">产品编码模糊关键字（同时匹配变更前/后的数据）</param>
    /// <param name="startTime">变更时间起始值（null 则不筛选）</param>
    /// <param name="endTime">变更时间结束值（null 则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW）</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<BoardConfigAuditLog>> GetPagedAsync(string productCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);
}
