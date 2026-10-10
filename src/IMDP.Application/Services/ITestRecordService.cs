// =============================================================================
// 文件名：ITestRecordService.cs
// 功能描述：检测记录服务接口，定义板卡检测记录与整机检测记录的查询与保存契约。
//           查询支持多条件组合筛选（订单号、产品 SN、工号、时间范围、检测类型）。
// =============================================================================
using IMDP.Application.DTOs;

namespace IMDP.Application.Services;

/// <summary>
/// 检测记录服务接口
/// <para>统一管理板卡检测记录（Board）与整机检测记录（Whole）两类数据。</para>
/// <para>实现类为 <see cref="TestRecordService"/>，依赖按产品分类拆分的 4 套板卡仓储（GasDG/GasOG/WaterDW/WaterOW）与 4 套整机仓储。</para>
/// </summary>
public interface ITestRecordService
{
    /// <summary>分页查询板卡检测记录</summary>
    /// <param name="orderNumberKeyword">订单号模糊关键字（为空则不筛选）</param>
    /// <param name="productSnKeyword">产品序列号模糊关键字（为空则不筛选）</param>
    /// <param name="workNumberKeyword">操作员工号模糊关键字（为空则不筛选）</param>
    /// <param name="startTime">检测开始时间下限（null 则不筛选）</param>
    /// <param name="endTime">检测开始时间上限（null 则不筛选）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储）</param>
    /// <returns>分页结果，按创建时间倒序排列</returns>
    Task<PagedResultDto<TestRecordDto>> GetBoardRecordsAsync(string orderNumberKeyword, string productSnKeyword, string workNumberKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);

    /// <summary>分页查询整机检测记录</summary>
    /// <param name="testType">检测类型筛选（WholeSet/WholeGet 对应整数；null 则不筛选）</param>
    /// <param name="orderNumberKeyword">订单号模糊关键字</param>
    /// <param name="productSnKeyword">产品序列号模糊关键字</param>
    /// <param name="workNumberKeyword">操作员工号模糊关键字</param>
    /// <param name="startTime">检测开始时间下限</param>
    /// <param name="endTime">检测开始时间上限</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="category">产品类别（GasDG/GasOG/WaterDW/WaterOW，用于路由到对应产品仓储）</param>
    /// <returns>分页结果，按创建时间倒序排列</returns>
    Task<PagedResultDto<TestRecordDto>> GetWholeRecordsAsync(int? testType, string orderNumberKeyword, string productSnKeyword, string workNumberKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, string category);

    /// <summary>保存板卡检测记录</summary>
    /// <param name="record">检测记录 DTO</param>
    /// <returns>新建记录的主键 Id</returns>
    Task<long> SaveBoardRecordAsync(TestRecordDto record);

    /// <summary>保存整机检测记录</summary>
    /// <param name="record">检测记录 DTO</param>
    /// <returns>新建记录的主键 Id</returns>
    Task<long> SaveWholeRecordAsync(TestRecordDto record);
}
