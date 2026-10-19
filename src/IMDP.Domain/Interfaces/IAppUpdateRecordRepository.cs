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

    /// <summary>
    /// 按机器名回填升级完成时间为指定时间
    /// <para>升级完成时间语义：升级下载完成后，该机器安装新版并自动重启的本次启动时间。</para>
    /// <para>应用每次启动时调用（升级后 AutoUpdater 重启的新进程会命中）：将该机器名下所有
    /// "完成时间仍为空"的升级记录批量回填为指定时间。</para>
    /// <para>仅更新满足以下全部条件的记录，已有完成时间的历史记录一律不覆盖：</para>
    /// <para>1. ClientMachineName 等于本机机器名（与其他机器的记录互不影响）；</para>
    /// <para>2. CompletedTime IS NULL（下载成功的记录完成时间留空等待回填；失败记录已写入失败时刻自动排除）；</para>
    /// <para>3. UpdateTime &lt;= 指定时间（防止回填晚于指定时间才发起的升级记录，时钟偏移/并发保护）。</para>
    /// </summary>
    /// <param name="machineName">客户端机器名（Environment.MachineName）</param>
    /// <param name="completedTime">回填的完成时间（应用启动时为本次启动时间）</param>
    /// <returns>实际回填的记录条数</returns>
    Task<int> BackfillCompletedTimeAsync(string machineName, DateTime completedTime);
}