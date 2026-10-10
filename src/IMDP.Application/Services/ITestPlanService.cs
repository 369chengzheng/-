// =============================================================================
// 文件名：ITestPlanService.cs
// 功能描述：检测计划服务接口，定义板卡配置与整机配置的增删改查及历史审计记录查询契约。
//           所有写操作（保存/删除）均会同步写入审计日志，保留 DeletedData/InsertedData 快照。
// =============================================================================
using IMDP.Application.DTOs;

namespace IMDP.Application.Services;

/// <summary>
/// 检测计划服务接口
/// <para>统一管理板卡配置（BoardConfig）与整机配置（WholeConfig）两类检测计划。</para>
/// <para>实现类为 <see cref="TestPlanService"/>，依赖 4 个仓储：配置仓储与审计日志仓储各 2 个。</para>
/// <para>保存与删除操作均会向审计日志表 INSERT 一条记录，记录操作前后的数据快照。</para>
/// </summary>
public interface ITestPlanService
{
    /// <summary>按产品编码获取板卡配置</summary>
    /// <param name="productCode">产品编码（精确匹配）</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储）</param>
    /// <returns>板卡配置 DTO；不存在返回 null</returns>
    Task<TestPlanDto?> GetBoardConfigAsync(string productCode, string category);

    /// <summary>分页查询板卡配置</summary>
    /// <param name="productCodeKeyword">产品编码模糊关键字（为空则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储）</param>
    /// <returns>分页结果，按更新时间倒序排列</returns>
    Task<PagedResultDto<TestPlanDto>> GetBoardConfigsAsync(string productCodeKeyword, int pageIndex, int pageSize, string category);

    /// <summary>
    /// 保存板卡配置（新增或更新）
    /// <para>按 ProductCode 查找已有记录：存在则 UPDATE 并写 UPDATE 审计日志；不存在则 INSERT 并写 INSERT 审计日志。</para>
    /// <para>通过 <paramref name="plan"/> 的 Category 字段路由到对应产品仓储。</para>
    /// </summary>
    /// <param name="plan">待保存的板卡配置 DTO（Category 字段需填充对应 ProductCategory 常量）</param>
    /// <param name="operatorWorkNumber">操作员工号（用于审计字段 CreatedBy/UpdatedBy）</param>
    /// <returns>是否保存成功</returns>
    Task<bool> SaveBoardConfigAsync(TestPlanDto plan, string operatorWorkNumber);

    /// <summary>
    /// 删除板卡配置
    /// <para>先写 DELETE 审计日志（含删除数据快照），再执行物理删除。</para>
    /// </summary>
    /// <param name="id">配置 Id</param>
    /// <param name="operatorWorkNumber">操作员工号</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储与审计日志仓储）</param>
    /// <returns>是否删除成功（配置不存在返回 false）</returns>
    Task<bool> DeleteBoardConfigAsync(long id, string operatorWorkNumber, string category);

    /// <summary>按计划类型、产品编码、客户编码获取整机配置</summary>
    /// <param name="planType">计划类型（WholeSet/WholeGet 对应的整数值）</param>
    /// <param name="productCode">产品编码</param>
    /// <param name="customerCode">客户编码</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储）</param>
    /// <returns>整机配置 DTO；不存在返回 null</returns>
    Task<TestPlanDto?> GetWholeConfigAsync(int planType, string productCode, string customerCode, string category);

    /// <summary>分页查询整机配置</summary>
    /// <param name="planType">计划类型筛选（null 则不按类型筛选）</param>
    /// <param name="productCodeKeyword">产品编码模糊关键字</param>
    /// <param name="customerCodeKeyword">客户编码模糊关键字</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储）</param>
    /// <returns>分页结果</returns>
    Task<PagedResultDto<TestPlanDto>> GetWholeConfigsAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, int pageIndex, int pageSize, string category);

    /// <summary>
    /// 保存整机配置（新增或更新）
    /// <para>按 PlanType+ProductCode+CustomerCode 查找已有记录：存在则 UPDATE 并写 UPDATE 审计日志；不存在则 INSERT 并写 INSERT 审计日志。</para>
    /// <para>通过 <paramref name="plan"/> 的 Category 字段路由到对应产品仓储。</para>
    /// </summary>
    /// <param name="plan">待保存的整机配置 DTO（Category 字段需填充对应 ProductCategory 常量）</param>
    /// <param name="operatorWorkNumber">操作员工号</param>
    /// <returns>是否保存成功</returns>
    Task<bool> SaveWholeConfigAsync(TestPlanDto plan, string operatorWorkNumber);

    /// <summary>
    /// 删除整机配置
    /// <para>先写 DELETE 审计日志（含删除数据快照），再执行物理删除。</para>
    /// </summary>
    /// <param name="id">配置 Id</param>
    /// <param name="operatorWorkNumber">操作员工号</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储与审计日志仓储）</param>
    /// <returns>是否删除成功（配置不存在返回 false）</returns>
    Task<bool> DeleteWholeConfigAsync(long id, string operatorWorkNumber, string category);

    /// <summary>分页查询板卡配置变更历史（基于审计日志）</summary>
    /// <param name="productCodeKeyword">产品编码模糊关键字</param>
    /// <param name="startTime">变更时间起始值（null 则不筛选）</param>
    /// <param name="endTime">变更时间结束值（null 则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品审计日志仓储）</param>
    /// <returns>分页结果，DTO 的 OperationType 标识 INSERT/UPDATE/DELETE</returns>
    Task<PagedResultDto<TestPlanDto>> GetBoardConfigHistoryAsync(string productCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);

    /// <summary>分页查询整机配置变更历史（基于审计日志）</summary>
    /// <param name="planType">计划类型筛选（null 则不按类型筛选）</param>
    /// <param name="productCodeKeyword">产品编码模糊关键字</param>
    /// <param name="customerCodeKeyword">客户编码模糊关键字</param>
    /// <param name="startTime">变更时间起始值（null 则不筛选）</param>
    /// <param name="endTime">变更时间结束值（null 则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品审计日志仓储）</param>
    /// <returns>分页结果，DTO 的 OperationType 标识 INSERT/UPDATE/DELETE</returns>
    Task<PagedResultDto<TestPlanDto>> GetWholeConfigHistoryAsync(int? planType, string productCodeKeyword, string customerCodeKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);
}
