// =============================================================================
// 文件名：IAppUpdateRecordRepository.cs
// 功能描述：应用版本更新记录仓储接口，定义按客户端机器名分页查询升级记录的契约。
// =============================================================================
using IMDP.Domain.Entities;

namespace IMDP.Domain.Interfaces;

/// <summary>
/// 应用版本更新记录仓储接口
/// <para>继承 <see cref="IRepository{T}"/>，在标准 CRUD 之上扩展按客户端机器名分页查询。</para>
/// </summary>
public interface IAppUpdateRecordRepository : IRepository<AppUpdateRecord>
{
    /// <summary>按客户端机器名分页查询升级记录</summary>
    /// <param name="clientMachineName">客户端机器名关键字（为 null 时不过滤）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<AppUpdateRecord>> GetPagedAsync(string clientMachineName, int pageIndex, int pageSize);

    /// <summary>按版本号 + 更新内容 + 升级时间范围分页查询升级记录（供 4.16 升级日志查询模块使用）</summary>
    /// <param name="versionKeyword">版本号模糊关键字（匹配 CurrentVersion/TargetVersion，为空则不筛选）</param>
    /// <param name="releaseNotesKeyword">更新内容关键词（匹配 ReleaseNotes，为空则不筛选）</param>
    /// <param name="startTime">升级开始时间范围起（可空，匹配 UpdateTime）</param>
    /// <param name="endTime">升级开始时间范围止（可空，匹配 UpdateTime）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>分页结果（按升级开始时间倒序）</returns>
    Task<PagedResult<AppUpdateRecord>> GetPagedAsync(string versionKeyword, string releaseNotesKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize);
}