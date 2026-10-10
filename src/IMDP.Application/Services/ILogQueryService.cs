// =============================================================================
// 文件名：ILogQueryService.cs
// 功能描述：日志查询服务接口，统一封装登录日志、升级日志、工单切换日志的分页查询契约。
//           供 Presentation 层 4.15/4.16/4.17 三个查询模块的 ViewModel 调用。
// =============================================================================
using IMDP.Application.DTOs;

namespace IMDP.Application.Services;

/// <summary>
/// 日志查询服务接口
/// <para>封装登录记录、应用升级记录、工单切换记录的分页查询方法，供日志查询 ViewModel 调用。</para>
/// <para>实现类为 <see cref="LogQueryService"/>，依赖对应仓储完成数据库查询并映射为 DTO。</para>
/// </summary>
public interface ILogQueryService
{
    /// <summary>分页查询登录日志（4.15 登录日志查询模块）</summary>
    /// <param name="workNumber">工号（精确匹配，为空则不筛选）</param>
    /// <param name="startTime">登录开始时间（可空）</param>
    /// <param name="endTime">登录结束时间（可空）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>登录记录 DTO 分页结果</returns>
    Task<PagedResultDto<LoginRecordDto>> GetLoginRecordsAsync(string workNumber, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize);

    /// <summary>分页查询升级日志（4.16 升级日志查询模块）</summary>
    /// <param name="versionKeyword">版本号模糊关键字（匹配升级前/后版本号）</param>
    /// <param name="releaseNotesKeyword">更新内容关键词（匹配 ReleaseNotes）</param>
    /// <param name="startTime">升级开始时间范围起（可空）</param>
    /// <param name="endTime">升级开始时间范围止（可空）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>升级记录 DTO 分页结果</returns>
    Task<PagedResultDto<AppUpdateRecordDto>> GetUpdateRecordsAsync(string versionKeyword, string releaseNotesKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize);

    /// <summary>分页查询工单切换日志（4.17 工单切换日志查询模块）</summary>
    /// <param name="orderNumberKeyword">制令单号模糊关键字</param>
    /// <param name="operatorKeyword">操作员模糊关键字（匹配 CreatedBy）</param>
    /// <param name="startTime">操作开始时间（可空）</param>
    /// <param name="endTime">操作结束时间（可空）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="ascending">按操作时间升序排列（true=正序，false=倒序）</param>
    /// <returns>工单切换记录 DTO 分页结果</returns>
    Task<PagedResultDto<OrderSwitchRecordDto>> GetOrderSwitchRecordsAsync(string orderNumberKeyword, string operatorKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize, bool ascending = false);
}
