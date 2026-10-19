// =============================================================================
// 文件名：AppUpdateRecordRepository.cs
// 功能描述：应用版本更新记录（AppUpdateRecord）仓储实现
//           继承泛型仓储基类，提供按客户端机器名分页查询升级记录的功能，
//           用于跟踪各客户端的应用升级历史与状态。
// =============================================================================
using IMDP.Domain.Entities;
using IMDP.Domain.Interfaces;
using SqlSugar;

namespace IMDP.Infrastructure.Persistence;

/// <summary>
/// 应用版本更新记录仓储实现
/// <para>继承自 <see cref="Repository{T}"/>，在标准 CRUD 之上扩展按客户端机器名分页查询。</para>
/// </summary>
public class AppUpdateRecordRepository : Repository<AppUpdateRecord>, IAppUpdateRecordRepository
{
    /// <summary>
    /// 初始化 <see cref="AppUpdateRecordRepository"/> 类的新实例。
    /// </summary>
    /// <param name="dbContext">数据库上下文，提供 SqlSugar 客户端。</param>
    public AppUpdateRecordRepository(IDbContext dbContext) : base(dbContext) { }

    /// <summary>
    /// 按客户端机器名关键字分页查询升级记录。
    /// </summary>
    /// <param name="clientMachineName">客户端机器名关键字（可为空，空表示不筛选）。</param>
    /// <param name="pageIndex">页索引（从 1 开始）。</param>
    /// <param name="pageSize">每页记录数。</param>
    /// <returns>包含分页数据及总记录数的分页结果。</returns>
    public async Task<PagedResult<AppUpdateRecord>> GetPagedAsync(string clientMachineName, int pageIndex, int pageSize)
    {
        // SqlSugar 用于承接总记录数的引用参数
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<AppUpdateRecord>();

        // 关键字按客户端机器名模糊匹配
        if (!string.IsNullOrWhiteSpace(clientMachineName))
            query = query.Where(r => r.ClientMachineName.Contains(clientMachineName));

        // 按升级开始时间倒序分页（UpdateTime 为空时回退到 CompletedTime）
        var items = await query.OrderByDescending(r => r.UpdateTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<AppUpdateRecord>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AppUpdateRecord>> GetPagedAsync(string versionKeyword, string releaseNotesKeyword, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
    {
        RefAsync<int> totalCount = 0;
        var query = _db.Queryable<AppUpdateRecord>();

        // 版本号模糊匹配（升级前/后版本号任一命中即可）
        if (!string.IsNullOrWhiteSpace(versionKeyword))
            query = query.Where(r => r.CurrentVersion.Contains(versionKeyword) || r.TargetVersion.Contains(versionKeyword));

        // 更新内容关键词匹配（ReleaseNotes）
        if (!string.IsNullOrWhiteSpace(releaseNotesKeyword))
            query = query.Where(r => r.ReleaseNotes.Contains(releaseNotesKeyword));

        // 升级开始时间范围筛选（UpdateTime）
        if (startTime.HasValue)
            query = query.Where(r => r.UpdateTime >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(r => r.UpdateTime <= endTime.Value);

        // 按升级开始时间倒序分页
        var items = await query.OrderByDescending(r => r.UpdateTime)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new PagedResult<AppUpdateRecord>
        {
            Items = items,
            TotalCount = totalCount.Value,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 批量 UPDATE：将本机器名下所有"完成时间仍为空"的升级记录的 CompletedTime 回填为指定时间。
    /// 条件设计（详见接口注释）：机器名精确匹配 + CompletedTime IS NULL + UpdateTime &lt;= 指定时间，
    /// 已有完成时间的历史记录一律不覆盖。
    /// </remarks>
    public async Task<int> BackfillCompletedTimeAsync(string machineName, DateTime completedTime)
    {
        if (string.IsNullOrWhiteSpace(machineName))
            return 0;

        return await _db.Updateable<AppUpdateRecord>()
            .SetColumns(r => r.CompletedTime == completedTime)
            .Where(r => r.ClientMachineName == machineName
                     && r.CompletedTime == null
                     && r.UpdateTime <= completedTime)
            .ExecuteCommandAsync();
    }
}